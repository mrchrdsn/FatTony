namespace FatTony.Ast;

public abstract record Stmt(int Line) : AstNode(Line);

public enum LedgerOpenMode { ReadOnly, Overwrite, Append }

public sealed record LetStmt(int Line, Assignable Target, Expr Value) : Stmt(Line);
public sealed record SayStmt(int Line, Expr Value) : Stmt(Line);

/// <summary>
/// One `if` plus any number of `otherwise if` branches, tried in order,
/// plus an optional trailing `otherwise`. A single `stop` (parsed by the
/// caller, not stored here) closes the whole chain.
/// </summary>
public sealed record IfStmt(
    int Line,
    List<(Expr Condition, List<Stmt> Body)> Branches,
    List<Stmt>? ElseBody) : Stmt(Line);

public sealed record RepeatWhileStmt(int Line, Expr Condition, List<Stmt> Body) : Stmt(Line);

/// <summary>A call used as its own statement — any return value is discarded.</summary>
public sealed record CallStmt(int Line, Expr Call) : Stmt(Line);

public sealed record ReturnStmt(int Line, Expr? Value) : Stmt(Line);

public sealed record IncreaseStmt(int Line, Assignable Target, Expr? Amount) : Stmt(Line);
public sealed record DecreaseStmt(int Line, Assignable Target, Expr? Amount) : Stmt(Line);

public sealed record BreakStmt(int Line) : Stmt(Line);       // walk away
public sealed record ContinueStmt(int Line) : Stmt(Line);    // keep it movin'
public sealed record TrashStmt(int Line) : Stmt(Line);       // take out da trash

public sealed record GetLedgerStmt(int Line, string LedgerName, LedgerOpenMode Mode) : Stmt(Line);
public sealed record WriteStmt(int Line, Expr Data, string LedgerName) : Stmt(Line);
public sealed record StashStmt(int Line, string LedgerName) : Stmt(Line);

/// <summary>"whack tony" / "whack tony's respect" — reset to default.</summary>
public sealed record WhackVarStmt(int Line, Assignable Target) : Stmt(Line);

/// <summary>"whack 2 from ledger1" — remove one ledger entry.</summary>
public sealed record WhackLedgerEntryStmt(int Line, Expr LineRef, string LedgerName) : Stmt(Line);

public sealed record BurnStmt(int Line, string LedgerName) : Stmt(Line);
public sealed record DestroyStmt(int Line, string InstanceName) : Stmt(Line);

/// <summary>Top-level only: "we're connected to X".</summary>
public sealed record ConnectedDecl(int Line, string NamespaceName) : AstNode(Line);
