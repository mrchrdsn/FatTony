namespace FatTony.Lexing;

/// <summary>
/// A single token. Literal carries the parsed value for Number/String
/// tokens (int / string respectively); null otherwise.
/// </summary>
public sealed record Token(TokenType Type, string Lexeme, object? Literal, int Line)
{
    public override string ToString() =>
        Literal is not null ? $"{Type} '{Lexeme}' ({Literal})" : $"{Type} '{Lexeme}'";
}
