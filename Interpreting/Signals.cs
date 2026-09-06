namespace FatTony.Interpreting;

/// <summary>
/// Internal control-flow signals — never surfaced to the user. Used to
/// unwind the C# call stack for `walk away` / `keep it movin'` / `here's
/// your cut`, the standard way a tree-walking interpreter implements
/// these without threading explicit signal values through every call.
/// </summary>
internal sealed class BreakSignal : Exception;

internal sealed class ContinueSignal : Exception;

internal sealed class ReturnSignal(RuntimeValue? value) : Exception
{
    public RuntimeValue? Value { get; } = value;
}
