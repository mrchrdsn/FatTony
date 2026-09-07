# The Fat Tony Language — Specification v1

*An esoteric language based on Fat Tony (The Simpsons) and mob speak.*

---

## 0. Design Philosophy

- Programs read like a mobster giving instructions to his crew.
- Keywords stay in-voice wherever practical; a small number of exceptions (comments, boolean sugar) trade voice for convention because they're used too often to justify the friction.
- Comparisons and booleans both collapse to plain integers (`1`/`0`) — there is no separate Boolean type, only Number sugar. This keeps the type system small.
- Destruction has two tiers everywhere it applies: a soft reset (**whack**) and a hard, final removal (**burn** for Ledgers, **destroy** for Things).

---

## 1. Program Structure

- Programs execute top-to-bottom.
- Statements are line-oriented (one statement per line).
- Indentation is **cosmetic only** — ignored by the interpreter, used purely for human readability.
- Blocks are explicitly closed with:
  ```
  stop
  ```
- Blank lines are ignored.

`stop` closes, in this spec: `thing`, `function`, `if` / `otherwise` chains, and `repeat while`. The one exception is the namespace block — see §3.

---

## 2. Lexical Elements

### 2.1 Comments

Single-line only, using `--`. Everything from `--` to the end of the line is ignored.

```
-- this line never happened
let money be 5
```

### 2.2 Case Sensitivity

- **Keywords** (`if`, `function`, `do me a favor`, etc.) are lowercase, fixed.
- **`LEGIT`** and **`SHADY`** (Boolean literals) are reserved, all-caps.
- **Identifiers** (variables, functions, namespaces, ledgers, things) are case-sensitive. `Money` and `money` are distinct variables.
- Style guidance (not enforced): avoid naming an identifier `legit` or `shady` in lowercase — it's legal, but sits confusingly close to the reserved literals.

---

## 3. Namespaces

```
we got a legitimate business called [namespace name]
```

- Exactly **one** namespace declaration per file. A second declaration before EOF is a hard error.
- The namespace block is implicitly closed at **end of file** — no `stop` required. An explicit `stop` is permitted but optional and treated as a no-op.

### Calling into a namespace

```
use [namespace] to do me a favor [function] param1, param2
```

### Persistent import

```
we're connected to [namespace]
```

After this statement, an unqualified `do me a favor [function]` call falls back to checking the connected namespace if the function isn't found locally.

---

## 4. Data Types

| Type | Description | Default value |
|---|---|---|
| Number | Integer only. Used for arithmetic, comparisons, conditions. | `0` |
| String | Enclosed in double quotes. No escape sequences. | `""` |
| Boolean | Sugar over Number. `LEGIT` = `1`, `SHADY` = `0`. | `SHADY` |
| Ledger | A handle to an open file. Exempt from `whack`/`burn` targeting rules below unless closed. | n/a — must be explicitly opened |
| Thing | An instance of a user-defined class (§11). | n/a — must be explicitly created |

### Examples

```
let money be 5
say "Business is good."
let isLoyal be LEGIT
```

Any nonzero Number is truthy in a condition — there is no separate coercion step. `if flag` is valid and checks non-zero-ness directly.

---

## 5. Variables & Assignment

All assignment uses the same shape: `let [variable] be [source]`. The source can be a literal, an expression, or one of several special reads:

```
let money be 5                                        -- literal
let x be da story from ledger1                        -- full contents of an open ledger
let X be what da guy said                             -- a line of user input
let tony be a new Associate                           -- a new Thing instance
let take be do me a favor collectDebt 50              -- captured function return value
let take be use tony to do me a favor collectDebt 50  -- captured method return value
let isFlush be respect more than 3                    -- captured comparison result
```

`do me a favor` and `use ... to do me a favor` calls may appear either as their own statement (return value discarded, if any) or on the right-hand side of `let` (return value captured). If a function's `here's your cut` returns nothing but its call is used as a `let` source, that's a hard error. Comparisons follow the same rule as any other expression — they don't have to appear directly inside `if` / `repeat while`, and can be stored for reuse, consistent with `LEGIT` / `SHADY` just being Number sugar underneath.

### Reset and Destroy

```
whack [variable]
```

- On a Number → resets to `0`.
- On a String → resets to `""`.
- On a Thing instance → resets **every field** on the instance to its type's default. The instance itself remains valid.
- On a variable holding an **open, not-yet-stashed** Ledger reference → **hard error**. Stash (close) it first.

```
burn [ledger]
```
Deletes a Ledger file entirely (see §10). Not valid for Numbers, Strings, or Things.

```
destroy [thing instance]
```
Marks a Thing instance for destruction (see §11). Not valid for any other type.

**Propagating rule:** `whack`-ing or `destroy`-ing a Thing instance that holds a field referencing an **open** Ledger is a hard error, for the same reason whacking an open ledger directly is — it would silently orphan a live file handle.

---

## 6. Operators

### 6.1 Arithmetic

Operates on Number operands only (hard error otherwise). Each operator has a symbol form and a word form — true synonyms, interchangeable, matching the same pattern already used for `ain't` / `don't equal`.

| Operation | Symbol | Word alias |
|---|---|---|
| Addition | `+` | `plus` |
| Subtraction | `-` | `minus` |
| Multiplication | `*` | `times` |
| Division | `/` | `split by` |

```
let total be money + 5
let total be money plus 5      -- equivalent
```

- **Integer division**: `/` / `split by` truncates toward zero — there is no floating-point type in this language.
- **Division by zero** is a hard error.
- **Precedence**: `*` / `times` and `/` / `split by` bind tighter than `+` / `plus` and `-` / `minus`. Both arithmetic tiers bind tighter than comparisons (§6.2), which in turn bind tighter than `and` (§6.3), which binds tighter than `or`. No parentheses/grouping in v1 (same limitation as §6.3) — use an intermediate `let` to force an evaluation order if needed.
- **Unary minus**: a `-` immediately preceding a Number with no operand before it (start of expression, or after `(`-equivalent contexts like `,` or `be`) is a negative literal, not subtraction. This ambiguity only applies to the symbol form — `minus` as a word is never mistaken for a negative literal.

### 6.2 Comparisons

Comparisons evaluate to a Number: `1` for true, `0` for false.

| Phrase | Operator |
|---|---|
| `more than` | `>` |
| `less than` | `<` |
| `at least` | `>=` |
| `at most` | `<=` |
| `equals` | `==` |
| `ain't` / `don't equal` | `!=` |

`more than` / `less than` / `at least` / `at most` require Number operands (hard error otherwise). `equals` / `ain't` work across matching types (Number-to-Number, String-to-String); comparing mismatched types is a hard error.

### 6.3 Logical Operators

| Phrase | Operator | Precedence |
|---|---|---|
| `and` | logical AND | binds tighter |
| `or` | logical OR | binds looser |

Standard precedence (`and` before `or`), matching conventional language behavior. No parentheses/grouping in v1 — use nested `if` statements if explicit grouping is needed.

```
if money more than 0 and respect more than 3
    say "We're solid."
stop
```

---

## 7. Control Flow

### 7.1 Conditionals

```
if <condition>
    <statements>
otherwise if <condition>
    <statements>
otherwise
    <statements>
stop
```

- `otherwise if` may repeat any number of times.
- `otherwise` is optional, and must be last.
- A single `stop` closes the entire chain.

```
if respect more than 3
    say "People are showin' respect."
otherwise if respect more than 1
    say "It's a start."
otherwise
    say "We gotta crack some heads."
stop
```

### 7.2 Loops

```
repeat while <condition>
    <statements>
stop
```

```
repeat while money more than 0
    decrease money
stop
```

### 7.3 Break / Continue

| Keyword | Meaning |
|---|---|
| `walk away` | break — exit the loop entirely |
| `keep it movin'` | continue — skip to the next iteration |

### 7.4 Increment / Decrement

```
increase <variable>            -- +1
increase <variable> by <N>     -- +N
decrease <variable>            -- -1
decrease <variable> by <N>     -- -N
```

Valid only on Number variables (including a Thing's Number field, e.g. `increase tony's respect`). Hard error on String, Ledger, or Thing targets.

---

## 8. Functions

### Declaration

```
function [function name] param1, param2...paramN
    <statements>
stop
```

### Call

```
do me a favor [function name] param1, param2...paramN
```

### Return

```
here's your cut [value]     -- returns a value
here's your cut             -- bare return, no value
```

---

## 9. Garbage Collection

```
take out da trash
```

- Explicit, invoked by the programmer — not automatic.
- Reclaims memory for local bindings from returned function calls that are no longer reachable.
- Reclaims any Thing instances previously marked via `destroy` (see §11).
- **Open Ledgers are exempt** — a not-yet-stashed ledger is never swept, regardless of variable reachability.
- Cleanup is quiet: no failure mode, no penalty for neglecting to call it.

---

## 10. Ledgers (File Handling)

A **Ledger** is the language's file abstraction.

### Opening

```
get [ledger name] from da safe                       -- read-only (default)
get [ledger name] from da safe and don't touch nothin' -- read-only (explicit)
get [ledger name] from da safe and cook da books       -- overwrite
get [ledger name] from da safe and keep writin'        -- append
```

### Writing

```
write [data] in [ledger name]
```

Hard error if the ledger was opened read-only (`don't touch nothin'`, or the default mode).

### Reading

```
let x be da story from [ledger name]
```

Reads the **entire contents** of the ledger into a String, all at once. This is a **point-in-time snapshot**, not a live view — later changes to the ledger (via `write` or `whack ... from`) do not retroactively update `x`. Re-run the read to refresh it.

### Removing a single entry

```
whack [line reference] from [ledger name]
```

Removes one line from the ledger, referenced by line number.

### Deleting the whole file

```
burn [ledger name]
```

### Closing

```
stash [ledger name] in da safe
```

### Hard error cases

- `whack [ledger variable]` or `burn [ledger name]` while the ledger is still **open** (not yet stashed) — must `stash` first.
- Writing to a read-only ledger.
- Using a ledger variable after it has been `burn`-ed.

---

## 11. Things (Classes)

A **Thing** is the language's class/object construct.

### Declaration

```
thing [Name]
    let [field] be [default]
    ...

    initiation param1, param2...
        <statements>
    stop

    function [method name] params...
        <statements>
    stop
stop
```

- Fields are declared with `let`, same as ordinary variables, and take their type's default if not otherwise specified.
- `initiation` is the constructor. Optional — if omitted, `new` takes no arguments and fields keep their declared defaults. If present, argument count/order must match at call time.
- Methods are declared with the same `function` keyword used for free functions, just scoped inside the `thing` block.

### Instantiation

```
let [variable] be a new [Thing]
let [variable] be a new [Thing] arg1, arg2...   -- if an initiation block is declared
```

### Field access

```
[variable]'s [field]
```

Works as both an rvalue and an lvalue:

```
let tony's respect be 5
say tony's cash
increase tony's respect
```

### Method calls

```
use [variable] to do me a favor [method name] arg1, arg2...
```

Same phrasing as a namespaced call (§3) — the interpreter resolves whether the target is a namespace or an instance at lookup time.

### Self-reference

Inside a method body, an instance refers to itself as:

```
yours truly
```

Used exactly like any other instance reference: `yours truly's cash`, `increase yours truly's respect`.

### Inheritance

```
thing [Child] answers to [Parent]
    ...
stop
```

- `[Child]` inherits all fields and methods of `[Parent]`, plus its own additions.
- **Method overriding is allowed** — a child may redefine a method it inherits.
- **Calling the parent's overridden implementation from inside a child's override (a "super call") is not supported in v1.** Flagged as a future addition.

### Whack / Destroy

```
whack [instance]      -- resets every field on the instance to its type's default; instance remains valid
destroy [instance]     -- reference becomes invalid immediately; underlying memory reclaimed at the next `take out da trash`
```

Hard error if the instance holds a field referencing an open Ledger (§5, §10).

---

## 12. Input / Output

```
say [expression]                    -- print
let [variable] be what da guy said   -- read a line of input into a String
```

---

## 13. Reserved Keywords (Reference)

| Keyword / Phrase | Purpose |
|---|---|
| `we got a legitimate business called` | namespace declaration |
| `we're connected to` | persistent namespace import |
| `use ... to do me a favor` | namespaced or instance method call |
| `function` | function/method declaration |
| `do me a favor` | free function call |
| `here's your cut` | return |
| `stop` | block terminator |
| `let ... be` | assignment |
| `say` | print |
| `what da guy said` | read input |
| `if` / `otherwise if` / `otherwise` | conditional |
| `more than` / `less than` / `at least` / `at most` / `equals` / `ain't` / `don't equal` | comparisons |
| `and` / `or` | logical operators |
| `repeat while` | loop |
| `walk away` | break |
| `keep it movin'` | continue |
| `increase` / `decrease` / `by` | increment/decrement |
| `take out da trash` | garbage collection |
| `get ... from da safe` | open ledger |
| `don't touch nothin'` / `cook da books` / `keep writin'` | ledger open modes |
| `write ... in` | ledger write |
| `da story from` | full-slurp ledger read |
| `whack` | reset variable / remove ledger line / reset Thing |
| `burn` | delete ledger file |
| `stash ... in da safe` | close ledger |
| `thing` | class declaration |
| `initiation` | constructor |
| `a new` | instantiation |
| `'s` | field access |
| `yours truly` | self-reference |
| `answers to` | inheritance |
| `destroy` | destroy Thing instance |
| `LEGIT` / `SHADY` | boolean literals (1 / 0) |
| `--` | comment |

---

## 14. Error Handling

All errors in v1 are **fatal** — there is no try/catch equivalent. On any hard error, the program halts immediately after printing an error message and exit sequence appropriate to the active mode below. Regardless of mode, the **process exit code is always a standard nonzero failure code** — the verbosity mode changes only what's printed, never whether execution continues or how the exit status is reported to the calling shell/script.

Every error message, in every mode where detail is shown, must remain identifiable as its specific error type — flavor should never come at the cost of a message being genuinely useless for debugging.

Before actually terminating, the interpreter always waits for a keypress (or Enter) — this guarantees the message is seen even if the interpreter was launched from a window that would otherwise close immediately on exit.

### 14.1 Modes

Selected via CLI flag. Default mode requires no flag.

```
fattony run script.ft                        -- default (mob speak)
fattony run script.ft --johnny-tightlips
fattony run script.ft --frankie-the-squealer
```

| Mode | Behavior |
|---|---|
| **Default** | Every error prints a mob-speak message specific to its error type (see catalog, §14.2). |
| **`johnny-tightlips`** | Every error — regardless of type — prints the exact same fixed, uninformative line. No error-specific detail is ever shown. |
| **`frankie-the-squealer`** | Every error prints an in-voice header naming the real error type, followed by a full technical stack trace. |

### 14.2 Error Catalog (Default Mode)

**Ledgers**

| Condition | Message |
|---|---|
| Ledger not found (opening read/append) | "Hey, I don't know nothin' 'bout no file [name]." |
| Ledger already open (double `get`, no `stash`) | "[name]'s already sittin' on the table. You can't grab it twice." |
| Operation on an unopened ledger | "[name]'s still in the safe. Go get it first." |
| Write to a read-only ledger | "I told you, don't touch nothin'." |
| `whack`/`burn` on a still-open ledger | "This ledger's still open — stash it before you go whackin' or burnin' it." |
| Use of a burned ledger | "That ledger's gone. Ashes don't talk." |
| `whack [line]` out of range | "There's no line [N] in [name]. You're whackin' nobody." |

**Types & Values**

| Condition | Message |
|---|---|
| Comparison type error (`more than`/`less than`/`at least`/`at most` on non-Number) | "That ain't how the numbers work, pal." |
| `equals`/`ain't` type mismatch | "You're comparin' apples to hand grenades. Cut it out." |
| Arithmetic on non-Number operand | "You can't do the math on that. It ain't a number." |
| Division by zero (`/` / `split by`) | "You can't split nothin' zero ways." |
| `increase`/`decrease` on non-Number | "You can't run the count on that. It ain't a number." |
| Undefined variable reference | "Never heard of 'im. Who's [name]?" |

**Functions & Namespaces**

| Condition | Message |
|---|---|
| Function not found | "Nobody by that name works for us." |
| Wrong argument count (function call) | "You're short on the count. Bring the right numbers next time." |
| Namespace not found | "Never heard of that business. You sure you got the right address?" |
| Second namespace declared | "Two businesses, one crew — pick one." |

**Things**

| Condition | Message |
|---|---|
| Thing type not found (`a new [Thing]`) | "We don't make those. Check your order." |
| Wrong argument count (`initiation`) | "You can't induct a guy without the right paperwork." |
| Field not found on instance | "[name]'s got no such thing on him. Frisk somebody else." |
| Method not found on instance | "[name] don't know how to do that. Wrong guy for the job." |
| Use of a destroyed instance | "[name]'s been taken care of. Don't ask about him no more." |
| `yours truly` outside a method | "Yours truly? You ain't nobody's crew right now." |
| `whack`/`destroy` a Thing holding a live ledger field | "He's still holdin' the books — you can't touch him yet." |

### 14.3 johnny-tightlips Mode

Every error, regardless of type, prints exactly:

```
Hey, maybe somethin' happened, maybe it didn't. I ain't sayin'.
Any key. That's all I'm sayin'.
```

No further detail — that second line doubles as both the (only) exit prompt and an extension of the bit. No separate prompt is printed in this mode.

### 14.4 frankie-the-squealer Mode

Every error prints a one-line in-voice header identifying the real error type, followed by a full technical stack trace:

```
Frankie's singin': LedgerNotFoundException — line 14, get ledger1 from da safe
  at Interpreter.OpenLedger(string name)
  at Interpreter.ExecuteStatement(Statement s)
  ...

Frankie's singin': DestroyedInstanceReferenceException — line 22, use tony to do me a favor collectDebt 50
  at Interpreter.ResolveInstance(string name)
  ...
```

### 14.5 Exit Sequence

After the error output (all modes except johnny-tightlips, which handles its own exit line as part of §14.3), the interpreter prints:

```
This meeting's over. Hit any key to walk out.
```

...and waits for a keypress/Enter before terminating with a nonzero exit code.

| Mode | Exit prompt |
|---|---|
| Default | "This meeting's over. Hit any key to walk out." |
| johnny-tightlips | (handled inline — "Any key. That's all I'm sayin'.") |
| frankie-the-squealer | "This meeting's over. Hit any key to walk out." *(same as default — frankie is already maximally verbose in the body)* |

---

## 15. Deferred to Future Versions

These were discussed and deliberately scoped out of v1:

- Block comments (only single-line `--` supported now).
- Line-by-line ledger reading with a cursor / EOF detection (only full-slurp `da story from` supported now).
- Parenthetical grouping in compound conditions (including in arithmetic expressions).
- Super calls (invoking a parent's overridden method from within a child override).
- Declaration hoisting / forward references — v1 assumes `function` and `thing` declarations must appear before their first use, top-to-bottom, matching the language's general top-to-bottom execution model. Not yet stress-tested against real programs.

---

## 16. Formal Grammar (EBNF)

### 16.1 Notation

- `::=` defines a rule.
- `|` separates alternatives.
- `[ x ]` — `x` is optional.
- `{ x }` — `x` repeats zero or more times.
- `"literal"` — a literal keyword or symbol, matched exactly.
- `UPPERCASE` — a lexical token (defined in §16.2).
- `<lowercase>` — a grammar rule (nonterminal).
- Multi-word keyword phrases (e.g. `"do me a favor"`) are treated as single atomic terminals here for readability; the lexer is responsible for recognizing them as fixed phrases.

Comments (`-- ...`) are stripped entirely at the lexer stage and never appear in this grammar — they are not tokens the parser sees.

### 16.2 Lexical Tokens

```
IDENTIFIER  ::= LETTER { LETTER | DIGIT }
NUMBER      ::= [ "-" ] DIGIT { DIGIT }
STRING      ::= '"' { ANY_CHAR_EXCEPT_QUOTE } '"'
```

`LEGIT` and `SHADY` are reserved identifiers (Boolean literals), not general-purpose `IDENTIFIER`s.

### 16.3 Program Structure

```
<program>        ::= <namespace-decl> { <top-level-item> }

<namespace-decl>  ::= "we got a legitimate business called" IDENTIFIER

<top-level-item>  ::= <connected-decl>
                     | <function-decl>
                     | <thing-decl>
                     | <statement>

<connected-decl>  ::= "we're connected to" IDENTIFIER
```

Only one `<namespace-decl>` is permitted per program (hard error otherwise — see §14.2). It is not closed by `stop`; the block runs implicitly to end of file. An explicit trailing `stop` is permitted and treated as a no-op.

### 16.4 Statements

```
<statement> ::= <let-stmt>
              | <say-stmt>
              | <if-stmt>
              | <repeat-while-stmt>
              | <call-stmt>
              | <return-stmt>
              | <increase-stmt>
              | <decrease-stmt>
              | <break-stmt>
              | <continue-stmt>
              | <trash-stmt>
              | <get-ledger-stmt>
              | <write-stmt>
              | <stash-stmt>
              | <whack-stmt>
              | <burn-stmt>
              | <destroy-stmt>

<let-stmt>          ::= "let" <assignable> "be" <expression>

<say-stmt>           ::= "say" <expression>

<if-stmt>            ::= "if" <expression> { <statement> }
                          { "otherwise" "if" <expression> { <statement> } }
                          [ "otherwise" { <statement> } ]
                          "stop"

<repeat-while-stmt>  ::= "repeat while" <expression> { <statement> } "stop"

<call-stmt>          ::= <call-expr>

<return-stmt>        ::= "here's your cut" [ <expression> ]

<increase-stmt>      ::= "increase" <assignable> [ "by" <expression> ]

<decrease-stmt>      ::= "decrease" <assignable> [ "by" <expression> ]

<break-stmt>         ::= "walk away"

<continue-stmt>      ::= "keep it movin'"

<trash-stmt>         ::= "take out da trash"

<get-ledger-stmt>    ::= "get" IDENTIFIER "from da safe" [ <ledger-mode> ]

<ledger-mode>        ::= "and don't touch nothin'"
                        | "and cook da books"
                        | "and keep writin'"

<write-stmt>         ::= "write" <expression> "in" IDENTIFIER

<stash-stmt>         ::= "stash" IDENTIFIER "in da safe"

<whack-stmt>         ::= "whack" <assignable>
                        | "whack" <expression> "from" IDENTIFIER

<burn-stmt>          ::= "burn" IDENTIFIER

<destroy-stmt>       ::= "destroy" IDENTIFIER
```

A `<call-stmt>` is simply a `<call-expr>` (§16.5) used on its own line, with any return value discarded.

### 16.5 Expressions

```
<expression> ::= <logical-or>

<logical-or>  ::= <logical-and> { "or" <logical-and> }

<logical-and> ::= <comparison> { "and" <comparison> }

<comparison>  ::= <arithmetic> [ <comparison-op> <arithmetic> ]

<comparison-op> ::= "more than" | "less than" | "at least" | "at most"
                   | "equals" | "ain't" | "don't equal"

<arithmetic>  ::= <term> { ( "+" | "plus" | "-" | "minus" ) <term> }

<term>        ::= <factor> { ( "*" | "times" | "/" | "split by" ) <factor> }

<factor>      ::= <operand>

<operand>     ::= <literal>
                 | <assignable>
                 | <call-expr>
                 | <ledger-read-expr>
                 | <input-read-expr>
                 | <instantiation-expr>

<call-expr>          ::= "do me a favor" IDENTIFIER [ <arg-list> ]
                        | "use" IDENTIFIER "to do me a favor" IDENTIFIER [ <arg-list> ]

<ledger-read-expr>    ::= "da story from" IDENTIFIER

<input-read-expr>     ::= "what da guy said"

<instantiation-expr>  ::= "a new" IDENTIFIER [ <arg-list> ]

<arg-list>    ::= <expression> { "," <expression> }

<literal>     ::= [ "-" ] NUMBER | STRING | "LEGIT" | "SHADY"

<assignable>  ::= ( IDENTIFIER | "yours truly" ) [ "'s" IDENTIFIER ]
```

`<comparison-op>` binds a single `<arithmetic>` expression on each side — comparisons in v1 are not chainable (`a more than b more than c` is not valid; use `and` to combine separate comparisons instead).

Precedence, tightest to loosest: `<term>` (`*`/`times`, `/`/`split by`) → `<arithmetic>` (`+`/`plus`, `-`/`minus`) → `<comparison>` → `<logical-and>` (`and`) → `<logical-or>` (`or`). No parentheses/grouping in v1 at any level (§6.1, §6.3) — use an intermediate `let` or a nested `if` to force evaluation order.

The leading `[ "-" ]` on `NUMBER` inside `<literal>` is the negative-literal case; a `-` appearing between two `<term>`s is instead consumed by `<arithmetic>` as binary subtraction. The lexer/parser resolves this the standard way: `-` is binary if the previous token completed an operand, unary otherwise.

### 16.6 Declarations

```
<function-decl>  ::= "function" IDENTIFIER [ <param-list> ]
                        { <statement> }
                      "stop"

<param-list>     ::= IDENTIFIER { "," IDENTIFIER }

<thing-decl>     ::= "thing" IDENTIFIER [ "answers to" IDENTIFIER ]
                        { <field-decl> | <initiation-decl> | <method-decl> }
                      "stop"

<field-decl>     ::= "let" IDENTIFIER "be" <expression>

<initiation-decl> ::= "initiation" [ <param-list> ]
                         { <statement> }
                       "stop"

<method-decl>    ::= "function" IDENTIFIER [ <param-list> ]
                        { <statement> }
                      "stop"
```

### 16.7 Known Simplifications

This grammar keeps most non-arithmetic expression forms shallow rather than allowing arbitrary nesting everywhere a fully general expression grammar might — e.g. a `<call-expr>`'s arguments are themselves full `<expression>`s (so calls can be passed values, comparisons, arithmetic, or the results of other calls as arguments), but `<operand>`/`<factor>` itself stays a flat list of alternatives rather than something like a parenthesized sub-expression, since there's no grouping construct in v1 (§6.1, §6.3). Arithmetic (`<arithmetic>`/`<term>`) is the one place real recursive precedence exists, since `*`/`/` needs to bind tighter than `+`/`-` for the operators to behave the way anyone typing them would expect.
