namespace FatTony.Interpreting;

/// <summary>
/// Runtime values. Note there is deliberately no separate Boolean kind —
/// LEGIT/SHADY are pure Number sugar per spec §4, so BoolLiteral in the
/// AST evaluates straight to a NumberValue(1) or NumberValue(0).
/// </summary>
public abstract class RuntimeValue
{
    public abstract string TypeName { get; }
    public abstract string Display();
}

public sealed class NumberValue(int value) : RuntimeValue
{
    public int Value { get; } = value;
    public override string TypeName => "Number";
    public override string Display() => Value.ToString();
    public bool IsTruthy => Value != 0;
    public static readonly NumberValue Default = new(0);
}

public sealed class StringValue(string value) : RuntimeValue
{
    public string Value { get; } = value;
    public override string TypeName => "String";
    public override string Display() => Value;
    public static readonly StringValue Default = new("");
}

public enum LedgerMode { ReadOnly, Overwrite, Append }

/// <summary>
/// A ledger's live state. Ledger files live in the current working
/// directory, named exactly after the ledger identifier with a
/// ".ledger" extension — this convention isn't specified anywhere in
/// the design conversation, so it's a deliberate default rather than
/// something dictated by the spec; flagged when the interpreter is
/// introduced.
/// </summary>
public sealed class LedgerValue(string name) : RuntimeValue
{
    public string Name { get; } = name;
    public bool IsOpen { get; set; }
    public LedgerMode Mode { get; set; }
    public List<string> Lines { get; set; } = [];
    public bool IsBurned { get; set; }

    public override string TypeName => "Ledger";
    public override string Display() => $"<ledger {Name}>";

    public string FilePath => Name + ".ledger";
}

/// <summary>The declaration-time shape of a `thing` — fields, initiation, methods, parent link.</summary>
public sealed class ThingDef(string name, ThingDef? parent)
{
    public string Name { get; } = name;
    public ThingDef? Parent { get; } = parent;
    public List<Ast.FieldDecl> OwnFields { get; } = [];
    public Ast.InitiationDecl? OwnInitiation { get; set; }
    public Dictionary<string, Ast.FunctionDecl> OwnMethods { get; } = [];

    /// <summary>All field declarations, parent-first, so child field order comes last (and children never shadow — field names are just merged; duplicate names are last-write-wins by declaration order).</summary>
    public IEnumerable<Ast.FieldDecl> AllFields()
    {
        if (Parent is not null)
            foreach (var f in Parent.AllFields())
                yield return f;
        foreach (var f in OwnFields)
            yield return f;
    }

    /// <summary>Resolves a method by name, checking this type then walking up to parents (supports overriding — a child's own method wins).</summary>
    public Ast.FunctionDecl? ResolveMethod(string methodName)
    {
        if (OwnMethods.TryGetValue(methodName, out var m)) return m;
        return Parent?.ResolveMethod(methodName);
    }

    /// <summary>The declared default expression for a field, honoring last-write-wins across the inheritance chain (matches AllFields' ordering).</summary>
    public Ast.Expr? FindFieldDefault(string fieldName) =>
        AllFields().LastOrDefault(f => f.Name == fieldName)?.Default;

    /// <summary>The nearest initiation in the chain, own first then parent's (no automatic parent-initiation chaining beyond "use whichever is nearest" — super-calls aren't implemented, per spec §15).</summary>
    public Ast.InitiationDecl? ResolveInitiation() => OwnInitiation ?? Parent?.ResolveInitiation();

    public bool IsOrInherits(ThingDef other) =>
        this == other || (Parent?.IsOrInherits(other) ?? false);
}

/// <summary>A live instance of a `thing`. Reference-type semantics — assigning it to another variable aliases the same instance, matching standard object semantics.</summary>
public sealed class ThingInstance(ThingDef def) : RuntimeValue
{
    public ThingDef Def { get; } = def;
    public Dictionary<string, RuntimeValue> Fields { get; } = [];
    public bool IsDestroyed { get; set; }

    public override string TypeName => Def.Name;
    public override string Display() => $"<{Def.Name}>";
}
