using FatTony.Lexing;
using FatTony.Parsing;
using FatTony.Interpreting;

namespace FatTony.Cli;

public enum Verbosity { Default, JohnnyTightlips, FrankieTheSquealer }

/// <summary>
/// Implements spec §14: `fattony run script.ft [--johnny-tightlips |
/// --frankie-the-squealer]`. All errors are fatal (no try/catch in v1);
/// the three flags only change what gets printed on the way out, never
/// whether execution continues. Exit code is always a standard nonzero
/// failure on error, 0 on success, regardless of verbosity mode.
/// </summary>
public static class CliRunner
{
    public static int Run(string[] args)
    {
        if (args.Length < 2 || args[0] != "run")
        {
            Console.WriteLine("Usage: fattony run <script.ft> [--johnny-tightlips | --frankie-the-squealer]");
            return 1;
        }

        string path = args[1];
        var mode = Verbosity.Default;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--johnny-tightlips") mode = Verbosity.JohnnyTightlips;
            else if (args[i] == "--frankie-the-squealer") mode = Verbosity.FrankieTheSquealer;
        }

        if (!File.Exists(path))
        {
            // Deliberately distinct wording from RuntimeError.LedgerNotFound —
            // this is the .ft script itself missing, not an in-program ledger.
            HandleError(mode, "ScriptNotFound", $"Hey, I don't know nothin' about no script called {path}.", line: 0);
            return 1;
        }

        string source = File.ReadAllText(path);

        try
        {
            var tokens = new Lexer(source).Tokenize();
            var program = new Parser(tokens).ParseProgram();
            new Interpreter().Run(program);
            return 0;
        }
        catch (LexException ex)
        {
            HandleError(mode, nameof(LexException), $"Somethin' ain't right: {ex.Message}", ex.Line, ex);
            return 1;
        }
        catch (ParseException ex)
        {
            HandleError(mode, nameof(ParseException), $"Somethin' ain't right: {ex.Message}", ex.Line, ex);
            return 1;
        }
        catch (RuntimeError ex)
        {
            HandleError(mode, ex.Kind.ToString(), ex.Message, ex.Line, ex);
            return 1;
        }
    }

    private static void HandleError(Verbosity mode, string kindLabel, string defaultMessage, int line, Exception? underlying = null)
    {
        switch (mode)
        {
            case Verbosity.JohnnyTightlips:
                // Spec §14.3: fixed line regardless of error type, no
                // error-specific detail ever shown. Johnny's second line
                // doubles as the exit prompt — no separate "This meeting's
                // over" line follows in this mode.
                Console.WriteLine("Hey, maybe somethin' happened, maybe it didn't. I ain't sayin'.");
                Console.WriteLine("Any key. That's all I'm sayin'.");
                WaitForKeypress();
                return;

            case Verbosity.FrankieTheSquealer:
                // Spec §14.4: in-voice header naming the real error type,
                // then a full technical trace. This is the underlying C#
                // implementation's stack trace (which interpreter methods
                // led to the throw), not a reconstructed FatTony-script-
                // level call stack — the interpreter doesn't maintain its
                // own script-level call stack yet, so this is the honest
                // "full detail" available today rather than a perfect
                // script backtrace.
                Console.WriteLine(line > 0
                    ? $"Frankie's singin': {kindLabel} — line {line}"
                    : $"Frankie's singin': {kindLabel}");
                Console.WriteLine(underlying?.StackTrace ?? "  (no further trace available)");
                break;

            default:
                Console.WriteLine(defaultMessage);
                if (line > 0) Console.WriteLine($"(line {line})");
                break;
        }

        Console.WriteLine();
        Console.WriteLine("This meeting's over. Hit any key to walk out.");
        WaitForKeypress();
    }

    /// <summary>
    /// Skips waiting when input isn't an interactive terminal (redirected,
    /// piped, or running under a host with no console) — waiting for a
    /// keypress that can never come would just hang a scripted/automated
    /// invocation. Genuinely correct behavior, not just a testing
    /// convenience: real CLI tools commonly skip "press any key" prompts
    /// the same way when running non-interactively.
    /// </summary>
    private static void WaitForKeypress()
    {
        if (Console.IsInputRedirected) return;
        try { Console.ReadKey(intercept: true); }
        catch (InvalidOperationException) { /* no console available */ }
    }
}
