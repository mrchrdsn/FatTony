namespace FatTony.Interpreting;

public enum ErrorKind
{
    LedgerNotFound, LedgerAlreadyOpen, LedgerNotOpen, WriteToReadOnlyLedger,
    WhackOrBurnOpenLedger, UseOfBurnedLedger, WhackLineOutOfRange,
    ComparisonTypeError, EqualsTypeMismatch, ArithmeticNonNumber, DivisionByZero,
    IncreaseDecreaseNonNumber, UndefinedVariable,
    FunctionNotFound, WrongArgCountFunction, NamespaceNotFound,
    ThingNotFound, WrongArgCountInitiation, FieldNotFound, MethodNotFound,
    UseOfDestroyedInstance, YoursTrulyOutsideMethod, WhackOrDestroyLiveLedgerField,
    CallReturnedNothing,
}

/// <summary>
/// A user-facing runtime error — always fatal (spec §14: v1 has no
/// try/catch). Carries a default-mode (mob-speak) message built at the
/// throw site from the catalog below; johnny-tightlips/frankie-the-
/// squealer formatting is a presentation-layer concern for the CLI
/// shell, not the interpreter, so it isn't done here yet.
/// </summary>
public sealed class RuntimeError(ErrorKind kind, int line, string message) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;
    public int Line { get; } = line;

    // ----- Ledgers -----
    public static RuntimeError LedgerNotFound(string name, int line) =>
        new(ErrorKind.LedgerNotFound, line, $"Hey, I don't know nothin' 'bout no file {name}.");

    public static RuntimeError LedgerAlreadyOpen(string name, int line) =>
        new(ErrorKind.LedgerAlreadyOpen, line, $"{name}'s already sittin' on the table. You can't grab it twice.");

    public static RuntimeError LedgerNotOpen(string name, int line) =>
        new(ErrorKind.LedgerNotOpen, line, $"{name}'s still in the safe. Go get it first.");

    public static RuntimeError WriteToReadOnlyLedger(int line) =>
        new(ErrorKind.WriteToReadOnlyLedger, line, "I told you, don't touch nothin'.");

    public static RuntimeError WhackOrBurnOpenLedger(int line) =>
        new(ErrorKind.WhackOrBurnOpenLedger, line, "This ledger's still open — stash it before you go whackin' or burnin' it.");

    public static RuntimeError UseOfBurnedLedger(int line) =>
        new(ErrorKind.UseOfBurnedLedger, line, "That ledger's gone. Ashes don't talk.");

    public static RuntimeError WhackLineOutOfRange(int lineRef, string ledgerName, int line) =>
        new(ErrorKind.WhackLineOutOfRange, line, $"There's no line {lineRef} in {ledgerName}. You're whackin' nobody.");

    // ----- Types & values -----
    public static RuntimeError ComparisonTypeError(int line) =>
        new(ErrorKind.ComparisonTypeError, line, "That ain't how the numbers work, pal.");

    public static RuntimeError EqualsTypeMismatch(int line) =>
        new(ErrorKind.EqualsTypeMismatch, line, "You're comparin' apples to hand grenades. Cut it out.");

    public static RuntimeError ArithmeticNonNumber(int line) =>
        new(ErrorKind.ArithmeticNonNumber, line, "You can't do the math on that. It ain't a number.");

    public static RuntimeError DivisionByZero(int line) =>
        new(ErrorKind.DivisionByZero, line, "You can't split nothin' zero ways.");

    public static RuntimeError IncreaseDecreaseNonNumber(int line) =>
        new(ErrorKind.IncreaseDecreaseNonNumber, line, "You can't run the count on that. It ain't a number.");

    public static RuntimeError UndefinedVariable(string name, int line) =>
        new(ErrorKind.UndefinedVariable, line, $"Never heard of 'im. Who's {name}?");

    // ----- Functions & namespaces -----
    public static RuntimeError FunctionNotFound(string name, int line) =>
        new(ErrorKind.FunctionNotFound, line, "Nobody by that name works for us.");

    public static RuntimeError WrongArgCountFunction(int line) =>
        new(ErrorKind.WrongArgCountFunction, line, "You're short on the count. Bring the right numbers next time.");

    public static RuntimeError NamespaceNotFound(string name, int line) =>
        new(ErrorKind.NamespaceNotFound, line, "Never heard of that business. You sure you got the right address?");

    // ----- Things -----
    public static RuntimeError ThingNotFound(string name, int line) =>
        new(ErrorKind.ThingNotFound, line, "We don't make those. Check your order.");

    public static RuntimeError WrongArgCountInitiation(int line) =>
        new(ErrorKind.WrongArgCountInitiation, line, "You can't induct a guy without the right paperwork.");

    public static RuntimeError FieldNotFound(string instanceName, string fieldName, int line) =>
        new(ErrorKind.FieldNotFound, line, $"{instanceName}'s got no such thing on him. Frisk somebody else.");

    public static RuntimeError MethodNotFound(string instanceName, string methodName, int line) =>
        new(ErrorKind.MethodNotFound, line, $"{instanceName} don't know how to do that. Wrong guy for the job.");

    public static RuntimeError UseOfDestroyedInstance(string name, int line) =>
        new(ErrorKind.UseOfDestroyedInstance, line, $"{name}'s been taken care of. Don't ask about him no more.");

    public static RuntimeError YoursTrulyOutsideMethod(int line) =>
        new(ErrorKind.YoursTrulyOutsideMethod, line, "Yours truly? You ain't nobody's crew right now.");

    public static RuntimeError WhackOrDestroyLiveLedgerField(int line) =>
        new(ErrorKind.WhackOrDestroyLiveLedgerField, line, "He's still holdin' the books — you can't touch him yet.");

    // Not in the original §14.2 catalog transcription — §5 established this
    // must be a hard error ("if a function's return is used as a `let`
    // source but returns nothing") without pinning exact wording. Added
    // here to complete the interpreter; worth folding into the spec doc.
    public static RuntimeError CallReturnedNothing(int line) =>
        new(ErrorKind.CallReturnedNothing, line, "That didn't pay out nothin'. Can't spend what ain't there.");
}
