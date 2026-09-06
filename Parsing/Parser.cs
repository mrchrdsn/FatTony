using FatTony.Ast;
using FatTony.Lexing;

namespace FatTony.Parsing;

public sealed class Parser(List<Token> tokens)
{
    private readonly List<Token> _tokens = tokens;
    private int _pos;

    private Token Current => _tokens[_pos];
    private Token PeekAhead(int offset = 1) => _tokens[Math.Min(_pos + offset, _tokens.Count - 1)];
    private bool Check(TokenType t) => Current.Type == t;
    private bool AtEnd => Current.Type == TokenType.Eof;

    private Token Advance()
    {
        var t = Current;
        if (!AtEnd) _pos++;
        return t;
    }

    private Token Expect(TokenType type, string what)
    {
        if (!Check(type)) throw Error($"Expected {what}, but found {Describe(Current)}.");
        return Advance();
    }

    private static string Describe(Token t) =>
        t.Type == TokenType.Eof ? "end of file" :
        t.Type == TokenType.Newline ? "end of line" :
        $"'{t.Lexeme}'";

    private ParseException Error(string msg) => new(msg, Current.Line);

    private void ExpectNewline()
    {
        if (Check(TokenType.Newline)) { Advance(); return; }
        if (AtEnd) return;
        throw Error($"Expected end of line, but found {Describe(Current)}.");
    }

    // ----- Program --------------------------------------------------

    public ProgramNode ParseProgram()
    {
        int startLine = Current.Line;
        Expect(TokenType.WeGotALegitimateBusinessCalled, "\"we got a legitimate business called\"");
        string nsName = Expect(TokenType.Identifier, "a business name").Lexeme;
        ExpectNewline();

        var items = new List<AstNode>();
        while (!AtEnd)
        {
            if (Check(TokenType.Stop))
            {
                // Optional, no-op explicit close of the namespace block.
                Advance();
                ExpectNewline();
                if (!AtEnd)
                    throw Error("Nothing goes after the business is closed out with 'stop'.");
                break;
            }
            items.Add(ParseTopLevelItem());
        }

        return new ProgramNode(startLine, nsName, items);
    }

    private AstNode ParseTopLevelItem()
    {
        if (Check(TokenType.WereConnectedTo)) return ParseConnectedDecl();
        if (Check(TokenType.Function)) return ParseFunctionDecl();
        if (Check(TokenType.Thing)) return ParseThingDecl();
        return ParseStatement();
    }

    private ConnectedDecl ParseConnectedDecl()
    {
        int line = Current.Line;
        Advance();
        string name = Expect(TokenType.Identifier, "a business name").Lexeme;
        ExpectNewline();
        return new ConnectedDecl(line, name);
    }

    // ----- Declarations ----------------------------------------------

    private FunctionDecl ParseFunctionDecl()
    {
        int line = Current.Line;
        Advance(); // function
        string name = Expect(TokenType.Identifier, "a function name").Lexeme;
        var parms = ParseParamListOptional();
        ExpectNewline();
        var body = ParseStatementsUntil(TokenType.Stop);
        Expect(TokenType.Stop, "'stop' to close the function");
        ExpectNewline();
        return new FunctionDecl(line, name, parms, body);
    }

    private List<string> ParseParamListOptional()
    {
        var parms = new List<string>();
        if (!Check(TokenType.Identifier)) return parms;
        parms.Add(Advance().Lexeme);
        while (Check(TokenType.Comma))
        {
            Advance();
            parms.Add(Expect(TokenType.Identifier, "a parameter name").Lexeme);
        }
        return parms;
    }

    private ThingDecl ParseThingDecl()
    {
        int line = Current.Line;
        Advance(); // thing
        string name = Expect(TokenType.Identifier, "a thing name").Lexeme;
        string? parent = null;
        if (Check(TokenType.AnswersTo))
        {
            Advance();
            parent = Expect(TokenType.Identifier, "the parent thing's name").Lexeme;
        }
        ExpectNewline();

        var fields = new List<FieldDecl>();
        InitiationDecl? initiation = null;
        var methods = new List<FunctionDecl>();

        while (!Check(TokenType.Stop) && !AtEnd)
        {
            if (Check(TokenType.Let))
            {
                fields.Add(ParseFieldDecl());
            }
            else if (Check(TokenType.Initiation))
            {
                if (initiation is not null)
                    throw Error("This thing already has an initiation — only one is allowed.");
                initiation = ParseInitiationDecl();
            }
            else if (Check(TokenType.Function))
            {
                methods.Add(ParseFunctionDecl());
            }
            else
            {
                throw Error($"Expected a field, initiation, or method inside 'thing {name}', but found {Describe(Current)}.");
            }
        }

        Expect(TokenType.Stop, "'stop' to close the thing");
        ExpectNewline();
        return new ThingDecl(line, name, parent, fields, initiation, methods);
    }

    private FieldDecl ParseFieldDecl()
    {
        int line = Current.Line;
        Advance(); // let
        string name = Expect(TokenType.Identifier, "a field name").Lexeme;
        Expect(TokenType.Be, "'be'");
        var value = ParseExpression();
        ExpectNewline();
        return new FieldDecl(line, name, value);
    }

    private InitiationDecl ParseInitiationDecl()
    {
        int line = Current.Line;
        Advance(); // initiation
        var parms = ParseParamListOptional();
        ExpectNewline();
        var body = ParseStatementsUntil(TokenType.Stop);
        Expect(TokenType.Stop, "'stop' to close the initiation");
        ExpectNewline();
        return new InitiationDecl(line, parms, body);
    }

    // ----- Statements --------------------------------------------------

    /// <summary>Parses statements until one of the given tokens is seen (not consumed).</summary>
    private List<Stmt> ParseStatementsUntil(params TokenType[] terminators)
    {
        var stmts = new List<Stmt>();
        while (!AtEnd && Array.IndexOf(terminators, Current.Type) < 0)
            stmts.Add(ParseStatement());
        return stmts;
    }

    private Stmt ParseStatement()
    {
        return Current.Type switch
        {
            TokenType.Let => ParseLet(),
            TokenType.Say => ParseSay(),
            TokenType.If => ParseIf(),
            TokenType.RepeatWhile => ParseRepeatWhile(),
            TokenType.DoMeAFavor or TokenType.Use => ParseCallStmt(),
            TokenType.HeresYourCut => ParseReturn(),
            TokenType.Increase => ParseIncrease(),
            TokenType.Decrease => ParseDecrease(),
            TokenType.WalkAway => ParseSimple(TokenType.WalkAway, l => new BreakStmt(l)),
            TokenType.KeepItMovin => ParseSimple(TokenType.KeepItMovin, l => new ContinueStmt(l)),
            TokenType.TakeOutDaTrash => ParseSimple(TokenType.TakeOutDaTrash, l => new TrashStmt(l)),
            TokenType.Get => ParseGetLedger(),
            TokenType.Write => ParseWrite(),
            TokenType.Stash => ParseStash(),
            TokenType.Whack => ParseWhack(),
            TokenType.Burn => ParseBurn(),
            TokenType.Destroy => ParseDestroy(),
            _ => throw Error($"Expected a statement, but found {Describe(Current)}.")
        };
    }

    private Stmt ParseSimple(TokenType keyword, Func<int, Stmt> build)
    {
        int line = Current.Line;
        Advance();
        ExpectNewline();
        return build(line);
    }

    private LetStmt ParseLet()
    {
        int line = Current.Line;
        Advance(); // let
        var target = ParseAssignable();
        Expect(TokenType.Be, "'be'");
        var value = ParseExpression();
        ExpectNewline();
        return new LetStmt(line, target, value);
    }

    private SayStmt ParseSay()
    {
        int line = Current.Line;
        Advance(); // say
        var value = ParseExpression();
        ExpectNewline();
        return new SayStmt(line, value);
    }

    private IfStmt ParseIf()
    {
        int line = Current.Line;
        Advance(); // if
        var branches = new List<(Expr, List<Stmt>)>();

        var cond = ParseExpression();
        ExpectNewline();
        var body = ParseStatementsUntil(TokenType.Otherwise, TokenType.Stop);
        branches.Add((cond, body));

        while (Check(TokenType.Otherwise) && PeekAhead().Type == TokenType.If)
        {
            Advance(); // otherwise
            Advance(); // if
            var c = ParseExpression();
            ExpectNewline();
            var b = ParseStatementsUntil(TokenType.Otherwise, TokenType.Stop);
            branches.Add((c, b));
        }

        List<Stmt>? elseBody = null;
        if (Check(TokenType.Otherwise))
        {
            Advance(); // otherwise (final else — we already know it's not "otherwise if")
            ExpectNewline();
            elseBody = ParseStatementsUntil(TokenType.Stop);
        }

        Expect(TokenType.Stop, "'stop' to close the if");
        ExpectNewline();
        return new IfStmt(line, branches, elseBody);
    }

    private RepeatWhileStmt ParseRepeatWhile()
    {
        int line = Current.Line;
        Advance(); // repeat while
        var cond = ParseExpression();
        ExpectNewline();
        var body = ParseStatementsUntil(TokenType.Stop);
        Expect(TokenType.Stop, "'stop' to close the loop");
        ExpectNewline();
        return new RepeatWhileStmt(line, cond, body);
    }

    private CallStmt ParseCallStmt()
    {
        int line = Current.Line;
        var call = ParseCallExpr();
        ExpectNewline();
        return new CallStmt(line, call);
    }

    private ReturnStmt ParseReturn()
    {
        int line = Current.Line;
        Advance(); // here's your cut
        Expr? value = Check(TokenType.Newline) ? null : ParseExpression();
        ExpectNewline();
        return new ReturnStmt(line, value);
    }

    private IncreaseStmt ParseIncrease()
    {
        int line = Current.Line;
        Advance();
        var target = ParseAssignable();
        Expr? amount = null;
        if (Check(TokenType.By)) { Advance(); amount = ParseExpression(); }
        ExpectNewline();
        return new IncreaseStmt(line, target, amount);
    }

    private DecreaseStmt ParseDecrease()
    {
        int line = Current.Line;
        Advance();
        var target = ParseAssignable();
        Expr? amount = null;
        if (Check(TokenType.By)) { Advance(); amount = ParseExpression(); }
        ExpectNewline();
        return new DecreaseStmt(line, target, amount);
    }

    private GetLedgerStmt ParseGetLedger()
    {
        int line = Current.Line;
        Advance(); // get
        string name = Expect(TokenType.Identifier, "a ledger name").Lexeme;
        Expect(TokenType.FromDaSafe, "'from da safe'");
        var mode = LedgerOpenMode.ReadOnly;
        if (Check(TokenType.AndDontTouchNothin)) { Advance(); mode = LedgerOpenMode.ReadOnly; }
        else if (Check(TokenType.AndCookDaBooks)) { Advance(); mode = LedgerOpenMode.Overwrite; }
        else if (Check(TokenType.AndKeepWritin)) { Advance(); mode = LedgerOpenMode.Append; }
        ExpectNewline();
        return new GetLedgerStmt(line, name, mode);
    }

    private WriteStmt ParseWrite()
    {
        int line = Current.Line;
        Advance(); // write
        var data = ParseExpression();
        Expect(TokenType.In, "'in'");
        string name = Expect(TokenType.Identifier, "a ledger name").Lexeme;
        ExpectNewline();
        return new WriteStmt(line, data, name);
    }

    private StashStmt ParseStash()
    {
        int line = Current.Line;
        Advance(); // stash
        string name = Expect(TokenType.Identifier, "a ledger name").Lexeme;
        Expect(TokenType.InDaSafe, "'in da safe'");
        ExpectNewline();
        return new StashStmt(line, name);
    }

    private Stmt ParseWhack()
    {
        int line = Current.Line;
        Advance(); // whack
        var expr = ParseExpression();
        if (Check(TokenType.From))
        {
            Advance();
            string ledger = Expect(TokenType.Identifier, "a ledger name").Lexeme;
            ExpectNewline();
            return new WhackLedgerEntryStmt(line, expr, ledger);
        }

        if (expr is not Assignable target)
            throw new ParseException("'whack' needs a variable or thing to reset (or 'whack ... from LEDGER' to remove an entry).", line);

        ExpectNewline();
        return new WhackVarStmt(line, target);
    }

    private BurnStmt ParseBurn()
    {
        int line = Current.Line;
        Advance();
        string name = Expect(TokenType.Identifier, "a ledger name").Lexeme;
        ExpectNewline();
        return new BurnStmt(line, name);
    }

    private DestroyStmt ParseDestroy()
    {
        int line = Current.Line;
        Advance();
        string name = Expect(TokenType.Identifier, "a thing instance").Lexeme;
        ExpectNewline();
        return new DestroyStmt(line, name);
    }

    // ----- Expressions (§16.5, precedence layers) -----------------------

    private Expr ParseExpression() => ParseLogicalOr();

    private Expr ParseLogicalOr()
    {
        var left = ParseLogicalAnd();
        while (Check(TokenType.Or))
        {
            int line = Current.Line;
            Advance();
            var right = ParseLogicalAnd();
            left = new BinaryExpr(line, left, BinaryOp.Or, right);
        }
        return left;
    }

    private Expr ParseLogicalAnd()
    {
        var left = ParseComparison();
        while (Check(TokenType.And))
        {
            int line = Current.Line;
            Advance();
            var right = ParseComparison();
            left = new BinaryExpr(line, left, BinaryOp.And, right);
        }
        return left;
    }

    private static readonly Dictionary<TokenType, BinaryOp> ComparisonOps = new()
    {
        [TokenType.MoreThan] = BinaryOp.MoreThan,
        [TokenType.LessThan] = BinaryOp.LessThan,
        [TokenType.AtLeast] = BinaryOp.AtLeast,
        [TokenType.AtMost] = BinaryOp.AtMost,
        [TokenType.Equals] = BinaryOp.Equals,
        [TokenType.Aint] = BinaryOp.NotEquals,
        [TokenType.DontEqual] = BinaryOp.NotEquals,
    };

    private Expr ParseComparison()
    {
        var left = ParseArithmetic();
        if (ComparisonOps.TryGetValue(Current.Type, out var op))
        {
            int line = Current.Line;
            Advance();
            var right = ParseArithmetic();
            return new BinaryExpr(line, left, op, right);
        }
        return left;
    }

    private Expr ParseArithmetic()
    {
        var left = ParseTerm();
        while (Check(TokenType.Plus) || Check(TokenType.Minus))
        {
            int line = Current.Line;
            var op = Advance().Type == TokenType.Plus ? BinaryOp.Add : BinaryOp.Subtract;
            var right = ParseTerm();
            left = new BinaryExpr(line, left, op, right);
        }
        return left;
    }

    private Expr ParseTerm()
    {
        var left = ParseFactor();
        while (Check(TokenType.Star) || Check(TokenType.Slash))
        {
            int line = Current.Line;
            var op = Advance().Type == TokenType.Star ? BinaryOp.Multiply : BinaryOp.Divide;
            var right = ParseFactor();
            left = new BinaryExpr(line, left, op, right);
        }
        return left;
    }

    private Expr ParseFactor()
    {
        if (Check(TokenType.Minus))
        {
            int line = Current.Line;
            Advance();
            var operand = ParseFactor();
            return new UnaryMinusExpr(line, operand);
        }
        return ParseOperand();
    }

    private Expr ParseOperand()
    {
        int line = Current.Line;
        switch (Current.Type)
        {
            case TokenType.Number:
                return new NumberLiteral(line, (int)Advance().Literal!);
            case TokenType.String:
                return new StringLiteral(line, (string)Advance().Literal!);
            case TokenType.Legit:
                Advance();
                return new BoolLiteral(line, true);
            case TokenType.Shady:
                Advance();
                return new BoolLiteral(line, false);
            case TokenType.Identifier:
            case TokenType.YoursTruly:
                return ParseAssignable();
            case TokenType.DoMeAFavor:
            case TokenType.Use:
                return ParseCallExpr();
            case TokenType.DaStoryFrom:
                Advance();
                return new LedgerReadExpr(line, Expect(TokenType.Identifier, "a ledger name").Lexeme);
            case TokenType.WhatDaGuySaid:
                Advance();
                return new InputReadExpr(line);
            case TokenType.ANew:
                Advance();
                string thingName = Expect(TokenType.Identifier, "a thing name").Lexeme;
                return new InstantiationExpr(line, thingName, ParseArgListOptional());
            default:
                throw Error($"Expected an expression, but found {Describe(Current)}.");
        }
    }

    private Assignable ParseAssignable()
    {
        int line = Current.Line;
        bool isYoursTruly = Check(TokenType.YoursTruly);
        string? name = null;
        if (isYoursTruly) Advance();
        else name = Expect(TokenType.Identifier, "a variable name").Lexeme;

        string? field = null;
        if (Check(TokenType.Possessive))
        {
            Advance();
            field = Expect(TokenType.Identifier, "a field name").Lexeme;
        }
        return new Assignable(line, isYoursTruly, name, field);
    }

    private Expr ParseCallExpr()
    {
        int line = Current.Line;
        if (Check(TokenType.DoMeAFavor))
        {
            Advance();
            string fn = Expect(TokenType.Identifier, "a function name").Lexeme;
            return new FunctionCallExpr(line, fn, ParseArgListOptional());
        }

        Expect(TokenType.Use, "'use'");
        bool isYoursTruly = Check(TokenType.YoursTruly);
        string? target = null;
        if (isYoursTruly) Advance();
        else target = Expect(TokenType.Identifier, "a namespace, instance name, or 'yours truly'").Lexeme;
        Expect(TokenType.ToDoMeAFavor, "'to do me a favor'");
        string method = Expect(TokenType.Identifier, "a function name").Lexeme;
        return new MethodCallExpr(line, isYoursTruly, target, method, ParseArgListOptional());
    }

    private static bool CanStartExpression(TokenType t) => t is
        TokenType.Number or TokenType.String or TokenType.Legit or TokenType.Shady or
        TokenType.Identifier or TokenType.YoursTruly or
        TokenType.DoMeAFavor or TokenType.Use or
        TokenType.DaStoryFrom or TokenType.WhatDaGuySaid or TokenType.ANew or
        TokenType.Minus;

    private List<Expr> ParseArgListOptional()
    {
        var args = new List<Expr>();
        if (!CanStartExpression(Current.Type)) return args;
        args.Add(ParseExpression());
        while (Check(TokenType.Comma))
        {
            Advance();
            args.Add(ParseExpression());
        }
        return args;
    }
}
