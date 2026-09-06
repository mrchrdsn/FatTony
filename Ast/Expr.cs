namespace FatTony.Ast;

public abstract record Expr(int Line) : AstNode(Line);

public sealed record NumberLiteral(int Line, int Value) : Expr(Line);
public sealed record StringLiteral(int Line, string Value) : Expr(Line);
public sealed record BoolLiteral(int Line, bool Value) : Expr(Line);   // LEGIT / SHADY

/// <summary>
/// A reference to a variable, optionally its field: `tony` or `tony's respect`.
/// Also covers self-reference: `yours truly` / `yours truly's respect`.
/// This is both an expression (a valid operand) and an lvalue — used
/// directly as the target of let/increase/decrease/whack statements.
/// </summary>
public sealed record Assignable(int Line, bool IsYoursTruly, string? Name, string? Field) : Expr(Line);

public enum BinaryOp
{
    Add, Subtract, Multiply, Divide,
    MoreThan, LessThan, AtLeast, AtMost, Equals, NotEquals,
    And, Or
}

public sealed record BinaryExpr(int Line, Expr Left, BinaryOp Op, Expr Right) : Expr(Line);

public sealed record UnaryMinusExpr(int Line, Expr Operand) : Expr(Line);

/// <summary>"do me a favor foo 1, 2" — a plain function call.</summary>
public sealed record FunctionCallExpr(int Line, string FunctionName, List<Expr> Args) : Expr(Line);

/// <summary>
/// "use X to do me a favor foo 1, 2" — Target may resolve to either a
/// namespace or an instance variable (or "yours truly"); the parser
/// doesn't know or care which, that's resolved later at interpretation
/// time.
/// </summary>
public sealed record MethodCallExpr(int Line, bool IsYoursTruly, string? Target, string FunctionName, List<Expr> Args) : Expr(Line);

/// <summary>"da story from ledger1" — full-slurp ledger read.</summary>
public sealed record LedgerReadExpr(int Line, string LedgerName) : Expr(Line);

/// <summary>"what da guy said" — reads a line of user input.</summary>
public sealed record InputReadExpr(int Line) : Expr(Line);

/// <summary>"a new Associate 1, 2" — Thing instantiation.</summary>
public sealed record InstantiationExpr(int Line, string ThingName, List<Expr> Args) : Expr(Line);
