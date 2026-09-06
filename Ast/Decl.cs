namespace FatTony.Ast;

/// <summary>Also used for methods declared inside a `thing` block — identical shape.</summary>
public sealed record FunctionDecl(int Line, string Name, List<string> Params, List<Stmt> Body) : AstNode(Line);

public sealed record FieldDecl(int Line, string Name, Expr Default) : AstNode(Line);

public sealed record InitiationDecl(int Line, List<string> Params, List<Stmt> Body) : AstNode(Line);

public sealed record ThingDecl(
    int Line,
    string Name,
    string? ParentName,
    List<FieldDecl> Fields,
    InitiationDecl? Initiation,
    List<FunctionDecl> Methods) : AstNode(Line);
