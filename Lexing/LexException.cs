namespace FatTony.Lexing;

/// <summary>
/// Raised for source text that can't be tokenized at all — malformed
/// input below the level of "valid word, just not a recognized one."
/// Distinct from parser/runtime errors, which get the full mob-speak /
/// johnny-tightlips / frankie-the-squealer treatment (spec §14). For now
/// this carries a plain diagnostic; wiring it into the three verbosity
/// modes happens once the interpreter/CLI shell exists.
/// </summary>
public sealed class LexException(string message, int line) : Exception(message)
{
    public int Line { get; } = line;

    public override string ToString() => $"Lex error, line {Line}: {Message}";
}
