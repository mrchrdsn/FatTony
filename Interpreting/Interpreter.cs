using FatTony.Ast;

namespace FatTony.Interpreting;

/// <summary>
/// Tree-walking interpreter. Executes a ProgramNode statement by
/// statement, top to bottom. Declarations are registered at the point
/// they're encountered (not hoisted) — matching the "must appear before
/// first use" assumption logged in spec §15.
/// </summary>
public sealed class Interpreter
{
    private readonly Dictionary<string, FunctionDecl> _functions = [];
    private readonly Dictionary<string, ThingDef> _things = [];
    private readonly HashSet<string> _connectedNamespaces = []; // recorded only — no multi-file module system yet
    private readonly Env _globals = new(null);
    private readonly TextWriter _out;
    private readonly TextReader _in;

    public string NamespaceName { get; private set; } = "";

    public Interpreter(TextWriter? output = null, TextReader? input = null)
    {
        _out = output ?? Console.Out;
        _in = input ?? Console.In;
    }

    public void Run(ProgramNode program)
    {
        NamespaceName = program.NamespaceName;
        foreach (var item in program.Items)
            ExecuteTopLevelItem(item);
    }

    private void ExecuteTopLevelItem(AstNode item)
    {
        switch (item)
        {
            case ConnectedDecl cd:
                _connectedNamespaces.Add(cd.NamespaceName);
                break;
            case FunctionDecl fd:
                _functions[fd.Name] = fd;
                break;
            case ThingDecl td:
                RegisterThing(td);
                break;
            case Stmt s:
                Execute(s, _globals, self: null);
                break;
            default:
                throw new InvalidOperationException($"Unexpected top-level node {item.GetType().Name}");
        }
    }

    private void RegisterThing(ThingDecl td)
    {
        ThingDef? parent = null;
        if (td.ParentName is not null && !_things.TryGetValue(td.ParentName, out parent))
            throw RuntimeError.ThingNotFound(td.ParentName, td.Line);

        var def = new ThingDef(td.Name, parent);
        def.OwnFields.AddRange(td.Fields);
        def.OwnInitiation = td.Initiation;
        foreach (var m in td.Methods) def.OwnMethods[m.Name] = m;
        _things[td.Name] = def;
    }

    // ================= Statements =================

    private void Execute(Stmt stmt, Env env, ThingInstance? self)
    {
        switch (stmt)
        {
            case LetStmt let:
                AssignTo(let.Target, Evaluate(let.Value, env, self), env, self);
                break;

            case SayStmt say:
                _out.WriteLine(Evaluate(say.Value, env, self).Display());
                break;

            case IfStmt ifs:
                ExecuteIf(ifs, env, self);
                break;

            case RepeatWhileStmt rw:
                ExecuteRepeatWhile(rw, env, self);
                break;

            case CallStmt cs:
                EvaluateCallRaw(cs.Call, env, self);
                break;

            case ReturnStmt ret:
                throw new ReturnSignal(ret.Value is null ? null : Evaluate(ret.Value, env, self));

            case IncreaseStmt inc:
                ExecuteIncreaseDecrease(inc.Target, inc.Amount, +1, env, self, inc.Line);
                break;

            case DecreaseStmt dec:
                ExecuteIncreaseDecrease(dec.Target, dec.Amount, -1, env, self, dec.Line);
                break;

            case BreakStmt:
                throw new BreakSignal();

            case ContinueStmt:
                throw new ContinueSignal();

            case TrashStmt:
                // Explicit GC (spec §9): no-op here. The host (.NET) GC does
                // the real memory reclamation; the language's observable
                // contract — you can never use a destroyed instance again,
                // regardless of when trash is taken out — is already
                // enforced via ThingInstance.IsDestroyed. There's nothing
                // left for this statement to actually do.
                break;

            case GetLedgerStmt g:
                ExecuteGetLedger(g, env);
                break;

            case WriteStmt w:
                ExecuteWrite(w, env, self);
                break;

            case StashStmt st:
                ExecuteStash(st, env);
                break;

            case WhackVarStmt wv:
                ExecuteWhackVar(wv, env, self);
                break;

            case WhackLedgerEntryStmt wl:
                ExecuteWhackLedgerEntry(wl, env, self);
                break;

            case BurnStmt b:
                ExecuteBurn(b, env);
                break;

            case DestroyStmt d:
                ExecuteDestroy(d, env);
                break;

            default:
                throw new InvalidOperationException($"Unexpected statement {stmt.GetType().Name}");
        }
    }

    private void ExecuteBlock(List<Stmt> stmts, Env env, ThingInstance? self)
    {
        foreach (var s in stmts) Execute(s, env, self);
    }

    private void ExecuteIf(IfStmt ifs, Env env, ThingInstance? self)
    {
        foreach (var (cond, body) in ifs.Branches)
        {
            if (Truthy(Evaluate(cond, env, self), cond.Line))
            {
                ExecuteBlock(body, env, self);
                return;
            }
        }
        if (ifs.ElseBody is not null)
            ExecuteBlock(ifs.ElseBody, env, self);
    }

    private void ExecuteRepeatWhile(RepeatWhileStmt rw, Env env, ThingInstance? self)
    {
        while (Truthy(Evaluate(rw.Condition, env, self), rw.Condition.Line))
        {
            try
            {
                ExecuteBlock(rw.Body, env, self);
            }
            catch (BreakSignal) { break; }
            catch (ContinueSignal) { /* fall through to re-check condition */ }
        }
    }

    private void ExecuteIncreaseDecrease(Assignable target, Expr? amountExpr, int sign, Env env, ThingInstance? self, int line)
    {
        if (EvaluateAssignable(target, env, self) is not NumberValue current)
            throw RuntimeError.IncreaseDecreaseNonNumber(line);

        int amount = 1;
        if (amountExpr is not null)
        {
            if (Evaluate(amountExpr, env, self) is not NumberValue a)
                throw RuntimeError.IncreaseDecreaseNonNumber(line);
            amount = a.Value;
        }

        AssignTo(target, new NumberValue(current.Value + sign * amount), env, self);
    }

    // ----- Assignment / lvalue resolution -----

    private void AssignTo(Assignable target, RuntimeValue value, Env env, ThingInstance? self)
    {
        if (target.Field is null)
        {
            if (target.IsYoursTruly)
                throw new RuntimeError(ErrorKind.UndefinedVariable, target.Line, "You can't reassign yourself. That ain't how this works.");
            env.Set(target.Name!, value);
            return;
        }

        var inst = ResolveInstanceForFieldAccess(target, env, self);
        if (!inst.Fields.ContainsKey(target.Field))
            throw RuntimeError.FieldNotFound(DisplayName(target), target.Field, target.Line);
        inst.Fields[target.Field] = value;
    }

    private RuntimeValue EvaluateAssignable(Assignable a, Env env, ThingInstance? self)
    {
        if (a.Field is null)
        {
            if (a.IsYoursTruly)
            {
                if (self is null) throw RuntimeError.YoursTrulyOutsideMethod(a.Line);
                return self;
            }
            if (!env.TryGet(a.Name!, out var v)) throw RuntimeError.UndefinedVariable(a.Name!, a.Line);
            return v;
        }

        var inst = ResolveInstanceForFieldAccess(a, env, self);
        if (!inst.Fields.TryGetValue(a.Field, out var fv))
            throw RuntimeError.FieldNotFound(DisplayName(a), a.Field, a.Line);
        return fv;
    }

    private ThingInstance ResolveInstanceForFieldAccess(Assignable a, Env env, ThingInstance? self)
    {
        RuntimeValue baseVal;
        if (a.IsYoursTruly)
        {
            if (self is null) throw RuntimeError.YoursTrulyOutsideMethod(a.Line);
            baseVal = self;
        }
        else
        {
            if (!env.TryGet(a.Name!, out baseVal)) throw RuntimeError.UndefinedVariable(a.Name!, a.Line);
        }

        if (baseVal is not ThingInstance inst)
            throw RuntimeError.FieldNotFound(DisplayName(a), a.Field!, a.Line);
        if (inst.IsDestroyed)
            throw RuntimeError.UseOfDestroyedInstance(DisplayName(a), a.Line);
        return inst;
    }

    private static string DisplayName(Assignable a) => a.IsYoursTruly ? "yours truly" : a.Name!;

    // ----- Ledgers -----

    private void ExecuteGetLedger(GetLedgerStmt g, Env env)
    {
        if (env.TryGet(g.LedgerName, out var existing) && existing is LedgerValue { IsOpen: true })
            throw RuntimeError.LedgerAlreadyOpen(g.LedgerName, g.Line);

        var mode = g.Mode switch
        {
            LedgerOpenMode.ReadOnly => LedgerMode.ReadOnly,
            LedgerOpenMode.Overwrite => LedgerMode.Overwrite,
            LedgerOpenMode.Append => LedgerMode.Append,
            _ => LedgerMode.ReadOnly
        };

        var ledger = new LedgerValue(g.LedgerName) { IsOpen = true, Mode = mode };

        if (mode == LedgerMode.Overwrite)
        {
            ledger.Lines = [];
        }
        else if (File.Exists(ledger.FilePath))
        {
            ledger.Lines = File.ReadAllLines(ledger.FilePath).ToList();
        }
        else if (mode == LedgerMode.ReadOnly)
        {
            throw RuntimeError.LedgerNotFound(g.LedgerName, g.Line);
        }
        else
        {
            ledger.Lines = []; // append mode, file doesn't exist yet — starts empty, created on stash
        }

        env.Set(g.LedgerName, ledger);
    }

    private LedgerValue ResolveOpenLedger(string name, Env env, int line)
    {
        if (!env.TryGet(name, out var v) || v is not LedgerValue ledger || !ledger.IsOpen)
            throw RuntimeError.LedgerNotOpen(name, line);
        return ledger;
    }

    private void ExecuteWrite(WriteStmt w, Env env, ThingInstance? self)
    {
        var ledger = ResolveOpenLedger(w.LedgerName, env, w.Line);
        if (ledger.Mode == LedgerMode.ReadOnly) throw RuntimeError.WriteToReadOnlyLedger(w.Line);
        ledger.Lines.Add(Evaluate(w.Data, env, self).Display());
    }

    private void ExecuteStash(StashStmt st, Env env)
    {
        var ledger = ResolveOpenLedger(st.LedgerName, env, st.Line);
        File.WriteAllLines(ledger.FilePath, ledger.Lines);
        ledger.IsOpen = false;
    }

    private void ExecuteWhackLedgerEntry(WhackLedgerEntryStmt wl, Env env, ThingInstance? self)
    {
        var ledger = ResolveOpenLedger(wl.LedgerName, env, wl.Line);
        if (Evaluate(wl.LineRef, env, self) is not NumberValue n)
            throw RuntimeError.ComparisonTypeError(wl.Line); // closest catalog fit for "this needs to be a number"
        if (n.Value < 1 || n.Value > ledger.Lines.Count)
            throw RuntimeError.WhackLineOutOfRange(n.Value, wl.LedgerName, wl.Line);
        ledger.Lines.RemoveAt(n.Value - 1);
    }

    private void ExecuteBurn(BurnStmt b, Env env)
    {
        if (env.TryGet(b.LedgerName, out var v) && v is LedgerValue ledger)
        {
            if (ledger.IsOpen) throw RuntimeError.WhackOrBurnOpenLedger(b.Line);
            if (File.Exists(ledger.FilePath)) File.Delete(ledger.FilePath);
            ledger.IsBurned = true;
            return;
        }

        // Never opened this run — still a legal burn target if the file exists on disk.
        string path = b.LedgerName + ".ledger";
        if (!File.Exists(path)) throw RuntimeError.LedgerNotFound(b.LedgerName, b.Line);
        File.Delete(path);
    }

    // ----- Whack / Destroy on variables and Things -----

    private void ExecuteWhackVar(WhackVarStmt wv, Env env, ThingInstance? self)
    {
        var target = wv.Target;

        if (target.Field is not null)
        {
            var inst = ResolveInstanceForFieldAccess(target, env, self);
            inst.Fields[target.Field] = EvaluateFieldDefault(inst, target.Field, target.Line);
            return;
        }

        if (target.IsYoursTruly)
        {
            if (self is null) throw RuntimeError.YoursTrulyOutsideMethod(target.Line);
            ResetThingInstance(self, target.Line);
            return;
        }

        if (!env.TryGet(target.Name!, out var v)) throw RuntimeError.UndefinedVariable(target.Name!, target.Line);

        switch (v)
        {
            case NumberValue:
                env.Set(target.Name!, NumberValue.Default);
                break;
            case StringValue:
                env.Set(target.Name!, StringValue.Default);
                break;
            case LedgerValue:
                // No defined "reset" state for a ledger (spec never
                // discusses one) — every whack-on-ledger-variable path
                // is treated as the same restriction as whack-while-open.
                throw RuntimeError.WhackOrBurnOpenLedger(target.Line);
            case ThingInstance inst:
                if (inst.IsDestroyed) throw RuntimeError.UseOfDestroyedInstance(target.Name!, target.Line);
                ResetThingInstance(inst, target.Line);
                break;
        }
    }

    private void ExecuteDestroy(DestroyStmt d, Env env)
    {
        if (!env.TryGet(d.InstanceName, out var v) || v is not ThingInstance inst)
            throw RuntimeError.UndefinedVariable(d.InstanceName, d.Line);
        if (inst.IsDestroyed) throw RuntimeError.UseOfDestroyedInstance(d.InstanceName, d.Line);
        CheckNoLiveLedgerFields(inst, d.Line);
        inst.IsDestroyed = true;
    }

    private void CheckNoLiveLedgerFields(ThingInstance inst, int line)
    {
        foreach (var v in inst.Fields.Values)
            if (v is LedgerValue { IsOpen: true })
                throw RuntimeError.WhackOrDestroyLiveLedgerField(line);
    }

    private void ResetThingInstance(ThingInstance inst, int line)
    {
        CheckNoLiveLedgerFields(inst, line);
        foreach (var fieldName in inst.Fields.Keys.ToList())
            inst.Fields[fieldName] = EvaluateFieldDefault(inst, fieldName, line);
    }

    /// <summary>
    /// A field's true "default" is re-evaluating its declared default
    /// expression from the `thing` definition — not a generic type
    /// default inferred from the current runtime value. This correctly
    /// handles a field whose default is itself e.g. `a new Something`.
    /// </summary>
    private RuntimeValue EvaluateFieldDefault(ThingInstance inst, string fieldName, int line)
    {
        var defaultExpr = inst.Def.FindFieldDefault(fieldName);
        if (defaultExpr is null) throw RuntimeError.FieldNotFound(inst.Def.Name, fieldName, line);
        return Evaluate(defaultExpr, new Env(_globals), inst);
    }

    // ================= Expressions =================

    private RuntimeValue Evaluate(Expr expr, Env env, ThingInstance? self)
    {
        switch (expr)
        {
            case NumberLiteral n: return new NumberValue(n.Value);
            case StringLiteral s: return new StringValue(s.Value);
            case BoolLiteral b: return new NumberValue(b.Value ? 1 : 0);
            case Assignable a: return EvaluateAssignable(a, env, self);
            case BinaryExpr bin: return EvaluateBinary(bin, env, self);

            case UnaryMinusExpr u:
                if (Evaluate(u.Operand, env, self) is not NumberValue un)
                    throw RuntimeError.ArithmeticNonNumber(u.Line);
                return new NumberValue(-un.Value);

            case FunctionCallExpr or MethodCallExpr:
                var raw = EvaluateCallRaw(expr, env, self);
                if (raw is null) throw RuntimeError.CallReturnedNothing(expr.Line);
                return raw;

            case LedgerReadExpr lr:
                return new StringValue(string.Join("\n", ResolveOpenLedger(lr.LedgerName, env, lr.Line).Lines));

            case InputReadExpr:
                return new StringValue(_in.ReadLine() ?? "");

            case InstantiationExpr inst:
                return Instantiate(inst, env, self);

            default:
                throw new InvalidOperationException($"Unexpected expression {expr.GetType().Name}");
        }
    }

    private static bool Truthy(RuntimeValue v, int line) =>
        v is NumberValue n ? n.IsTruthy : throw RuntimeError.ComparisonTypeError(line);

    private RuntimeValue EvaluateBinary(BinaryExpr b, Env env, ThingInstance? self)
    {
        if (b.Op == BinaryOp.And)
        {
            if (!Truthy(Evaluate(b.Left, env, self), b.Line)) return new NumberValue(0);
            return new NumberValue(Truthy(Evaluate(b.Right, env, self), b.Line) ? 1 : 0);
        }
        if (b.Op == BinaryOp.Or)
        {
            if (Truthy(Evaluate(b.Left, env, self), b.Line)) return new NumberValue(1);
            return new NumberValue(Truthy(Evaluate(b.Right, env, self), b.Line) ? 1 : 0);
        }

        var left = Evaluate(b.Left, env, self);
        var right = Evaluate(b.Right, env, self);

        return b.Op switch
        {
            //BinaryOp.Add => NumOp(left, right, b.Line, static (x, y) => x + y),
            BinaryOp.Add => (left is StringValue || right is StringValue) 
                ? new StringValue((left is StringValue ls ? ls.Value : GetStringRepresentation(left)) + (right is StringValue rs ? rs.Value : GetStringRepresentation(right)))
                : NumOp(left, right, b.Line, static (x, y) => x + y),
            BinaryOp.Subtract => NumOp(left, right, b.Line, static (x, y) => x - y),
            BinaryOp.Multiply => NumOp(left, right, b.Line, static (x, y) => x * y),
            BinaryOp.Divide => Divide(left, right, b.Line),
            BinaryOp.MoreThan => CompareNum(left, right, b.Line, static (x, y) => x > y),
            BinaryOp.LessThan => CompareNum(left, right, b.Line, static (x, y) => x < y),
            BinaryOp.AtLeast => CompareNum(left, right, b.Line, static (x, y) => x >= y),
            BinaryOp.AtMost => CompareNum(left, right, b.Line, static (x, y) => x <= y),
            BinaryOp.Equals => new NumberValue(ValuesEqual(left, right, b.Line) ? 1 : 0),
            BinaryOp.NotEquals => new NumberValue(ValuesEqual(left, right, b.Line) ? 0 : 1),
            _ => throw new InvalidOperationException($"Unexpected operator {b.Op}")
        };
    }

    private static string GetStringRepresentation(RuntimeValue val)
    {
        if (val is StringValue s) return s.Value;
        if (val is NumberValue n) return n.Value.ToString();
        return val?.ToString() ?? "";
    }

    private static NumberValue NumOp(RuntimeValue l, RuntimeValue r, int line, Func<int, int, int> op)
    {
        if (l is not NumberValue ln || r is not NumberValue rn) throw RuntimeError.ArithmeticNonNumber(line);
        return new NumberValue(op(ln.Value, rn.Value));
    }

    private static NumberValue Divide(RuntimeValue l, RuntimeValue r, int line)
    {
        if (l is not NumberValue ln || r is not NumberValue rn) throw RuntimeError.ArithmeticNonNumber(line);
        if (rn.Value == 0) throw RuntimeError.DivisionByZero(line);
        return new NumberValue(ln.Value / rn.Value); // C# int division already truncates toward zero
    }

    private static NumberValue CompareNum(RuntimeValue l, RuntimeValue r, int line, Func<int, int, bool> cmp)
    {
        if (l is not NumberValue ln || r is not NumberValue rn) throw RuntimeError.ComparisonTypeError(line);
        return new NumberValue(cmp(ln.Value, rn.Value) ? 1 : 0);
    }

    private static bool ValuesEqual(RuntimeValue l, RuntimeValue r, int line)
    {
        if (l is NumberValue ln && r is NumberValue rn) return ln.Value == rn.Value;
        if (l is StringValue ls && r is StringValue rs) return ls.Value == rs.Value;
        throw RuntimeError.EqualsTypeMismatch(line);
    }

    // ----- Calls -----

    private RuntimeValue? EvaluateCallRaw(Expr callExpr, Env env, ThingInstance? self) => callExpr switch
    {
        FunctionCallExpr fc => CallFunction(fc, env, self),
        MethodCallExpr mc => CallMethod(mc, env, self),
        _ => throw new InvalidOperationException($"Not a call: {callExpr.GetType().Name}")
    };

    private RuntimeValue? CallFunction(FunctionCallExpr fc, Env env, ThingInstance? self)
    {
        if (!_functions.TryGetValue(fc.FunctionName, out var decl))
            throw RuntimeError.FunctionNotFound(fc.FunctionName, fc.Line);

        var args = fc.Args.Select(a => Evaluate(a, env, self)).ToList();
        if (args.Count != decl.Params.Count) throw RuntimeError.WrongArgCountFunction(fc.Line);

        var localEnv = new Env(_globals);
        for (int i = 0; i < decl.Params.Count; i++) localEnv.Define(decl.Params[i], args[i]);

        try
        {
            ExecuteBlock(decl.Body, localEnv, self: null); // plain functions never have a "self"
            return null; // fell off the end — treated as an implicit bare return
        }
        catch (ReturnSignal rs)
        {
            return rs.Value;
        }
    }

    private RuntimeValue? CallMethod(MethodCallExpr mc, Env env, ThingInstance? self)
    {
        ThingInstance target;
        string displayName;

        if (mc.IsYoursTruly)
        {
            if (self is null) throw RuntimeError.YoursTrulyOutsideMethod(mc.Line);
            target = self;
            displayName = "yours truly";
        }
        else
        {
            // No multi-file namespace/module system yet (spec's imports
            // assume separate businesses that this single-file interpreter
            // can't load) — so an unresolvable target is reported as a
            // missing namespace, the closest honest fit given the gap.
            if (!env.TryGet(mc.Target!, out var tv) || tv is not ThingInstance inst)
                throw RuntimeError.NamespaceNotFound(mc.Target!, mc.Line);
            target = inst;
            displayName = mc.Target!;
        }

        if (target.IsDestroyed) throw RuntimeError.UseOfDestroyedInstance(displayName, mc.Line);

        var method = target.Def.ResolveMethod(mc.FunctionName);
        if (method is null) throw RuntimeError.MethodNotFound(displayName, mc.FunctionName, mc.Line);

        var args = mc.Args.Select(a => Evaluate(a, env, self)).ToList();
        if (args.Count != method.Params.Count) throw RuntimeError.WrongArgCountFunction(mc.Line);

        var localEnv = new Env(_globals);
        for (int i = 0; i < method.Params.Count; i++) localEnv.Define(method.Params[i], args[i]);

        try
        {
            ExecuteBlock(method.Body, localEnv, target);
            return null;
        }
        catch (ReturnSignal rs)
        {
            return rs.Value;
        }
    }

    private ThingInstance Instantiate(InstantiationExpr inst, Env env, ThingInstance? self)
    {
        if (!_things.TryGetValue(inst.ThingName, out var def))
            throw RuntimeError.ThingNotFound(inst.ThingName, inst.Line);

        var instance = new ThingInstance(def);
        foreach (var field in def.AllFields())
            instance.Fields[field.Name] = Evaluate(field.Default, new Env(_globals), instance);

        var args = inst.Args.Select(a => Evaluate(a, env, self)).ToList();
        var initiation = def.ResolveInitiation();

        if (initiation is null)
        {
            if (args.Count != 0) throw RuntimeError.WrongArgCountInitiation(inst.Line);
        }
        else
        {
            if (args.Count != initiation.Params.Count) throw RuntimeError.WrongArgCountInitiation(inst.Line);
            var localEnv = new Env(_globals);
            for (int i = 0; i < initiation.Params.Count; i++) localEnv.Define(initiation.Params[i], args[i]);
            try { ExecuteBlock(initiation.Body, localEnv, instance); }
            catch (ReturnSignal) { /* a constructor's return value, if any, is discarded */ }
        }

        return instance;
    }
}
