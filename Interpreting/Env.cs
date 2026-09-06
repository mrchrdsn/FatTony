namespace FatTony.Interpreting;

/// <summary>
/// Variable scope. Function/initiation/method calls each get one fresh
/// Environment chained to the global scope, so a function can both read
/// AND mutate a global it hasn't shadowed — `let`, `increase`, and
/// `decrease` all resolve a name by walking up the chain to wherever it
/// already exists, mutating it there, and only create a brand-new local
/// binding if the name isn't found anywhere. This exact scoping model
/// isn't specified in the design conversation (no `global`/`nonlocal`
/// concept was ever introduced), so it's a deliberate default, flagged
/// when the interpreter is introduced.
///
/// `if`/`repeat while` bodies do NOT get their own Environment — they
/// share whichever scope they're already in, so a `let` inside an `if`
/// remains visible after the block ends (no block-level scoping).
/// </summary>
public sealed class Env(Env? parent)
{
    private readonly Dictionary<string, RuntimeValue> _values = [];
    public Env? Parent { get; } = parent;

    public void Define(string name, RuntimeValue value) => _values[name] = value;

    public bool TryGet(string name, out RuntimeValue value)
    {
        if (_values.TryGetValue(name, out value!)) return true;
        return Parent is not null && Parent.TryGet(name, out value!);
    }

    /// <summary>Assigns to an existing binding wherever it lives in the chain; if it doesn't exist anywhere, defines it locally (covers a bare `let` on a brand-new name).</summary>
    public void Set(string name, RuntimeValue value)
    {
        if (_values.ContainsKey(name)) { _values[name] = value; return; }
        if (Parent is not null && Parent.TryGet(name, out _)) { Parent.Set(name, value); return; }
        _values[name] = value;
    }
}
