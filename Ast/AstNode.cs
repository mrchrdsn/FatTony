namespace FatTony.Ast;

public abstract record AstNode(int Line);

/// <summary>
/// The whole program: the (mandatory, singular) namespace name, plus
/// every top-level item in source order — connected-imports, function
/// declarations, thing declarations, and ordinary statements can all
/// appear interleaved at the top level.
/// </summary>
public sealed record ProgramNode(int Line, string NamespaceName, List<AstNode> Items) : AstNode(Line);
