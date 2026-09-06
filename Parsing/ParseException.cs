namespace FatTony.Parsing;

/// <summary>
/// Raised for source that tokenizes fine but doesn't form a valid
/// program — wrong token where the grammar expected something else.
/// Distinct from LexException (malformed source) and eventual runtime
/// errors (§14's three verbosity modes apply once the interpreter runs).
/// </summary>
public sealed class ParseException(string message, int line) : Exception(message)
{
    public int Line { get; } = line;

    public override string ToString() => $"Parse error, line {Line}: {Message}";
}
