# CLIExpander

`CLIExpander` is a lightweight command-line argument expansion and template substitution library for .NET. It allows generating combinatorial argument lists (Cartesian products), parameter sweeps, and template expansions using simple `.expand.` block syntax.

---

## Table of Contents

1. [Overview](#overview)
2. [Syntax & Semantics](#syntax--semantics)
   - [Basic Expansion Block](#basic-expansion-block)
   - [Cartesian Product (Nested Expansion)](#cartesian-product-nested-expansion)
   - [Sequential Expansion Blocks](#sequential-expansion-blocks)
   - [Token Concatenation (`.join.` ... `.join_end.`)](#token-concatenation-join--join_end)
   - [Explicit Known Values (`.get.` and `.split_get.`)](#explicit-known-values-get-and-split_get)
   - [Scope vs. MacroStore vs. ValueStore Distinction](#scope-vs-macrostore-vs-valuestore-distinction)
   - [Variable Scoping & Shadowing](#variable-scoping--shadowing)
   - [Baseline Macros (`MacroStore`)](#baseline-macros-macrostore)
   - [Full-Token (Fragment) vs. Inner-Token Substitution](#full-token-fragment-vs-inner-token-substitution)
   - [Empty Alternatives](#empty-alternatives)
3. [Extensibility & Subclassing](#extensibility--subclassing)
   - [Virtual Resolution (`GetByKey` vs `GetKnownValue`)](#virtual-resolution-getbykey-vs-getknownvalue)
   - [MacroStore Configuration](#macrostore-configuration)
   - [ValueStore Configuration & Seeding](#valuestore-configuration--seeding)
   - [Custom Token Splitting (`Split`)](#custom-token-splitting-split)
   - [Custom Identifier Characters (`IsIdentifierStart` & `IsIdentifierPart`)](#custom-identifier-characters-isidentifierstart--isidentifierpart)
   - [Configurable Syntax Tokens (`ExpandStartToken`, `ExpandEndToken`, `DelimiterToken`, `JoinStartToken`, `JoinEndToken`, `GetToken`, `SplitGetToken`)](#configurable-syntax-tokens)
   - [Token Splitting Control (`SplitAfterGetByKey`)](#token-splitting-control-splitaftergetbykey)
   - [Partial Matching Control (`MatchPartials`)](#partial-matching-control-matchpartials)
   - [Custom Expansion Pipeline (`Process`)](#custom-expansion-pipeline-process)
   - [Factory Delegate Support](#factory-delegate-support)
4. [API Reference](#api-reference)
   - [CLIExpander.Process (Static)](#cliexpanderprocess-static)
   - [CLIExpander.Process (Instance)](#cliexpanderprocess-instance)
   - [CLIExpander.GetByKey (Instance)](#cliexpandergetbykey-instance)
   - [CLIExpander.GetKnownValue (Instance)](#cliexpandergetknownvalue-instance)
   - [CLIExpander.InitializeValueStore (Instance)](#cliexpanderinitializevaluestore-instance)
   - [CLIExpander.Split (Instance)](#cliexpandersplit-instance)
   - [CLIExpander.IsIdentifierStart (Instance)](#cliexpanderisidentifierstart-instance)
   - [CLIExpander.IsIdentifierPart (Instance)](#cliexpanderisidentifierpart-instance)
   - [StartedAt (Property)](#startedat-property)
   - [MacroStore (Property)](#macrostore-property)
   - [ValueStore (Property)](#valuestore-property)
   - [ExpandStartToken (Field)](#expandstarttoken-field)
   - [ExpandEndToken (Field)](#expandendtoken-field)
   - [DelimiterToken (Field)](#delimitertoken-field)
   - [JoinStartToken (Field)](#joinstarttoken-field)
   - [JoinEndToken (Field)](#joinendtoken-field)
   - [GetToken (Field)](#gettoken-field)
   - [SplitGetToken (Field)](#splitgettoken-field)
   - [SplitAfterGetByKey (Field)](#splitaftergetbykey-field)
   - [MatchPartials (Field)](#matchpartials-field)
   - [CLReturn Struct](#clreturn-struct)
   - [Scope & Watermarks](#scope--watermarks)
5. [Error Handling & Diagnostic Messages](#error-handling--diagnostic-messages)
6. [Usage Examples](#usage-examples)
   - [C# Quickstart](#c-quickstart)
   - [Known Values & Join Composition Example](#known-values--join-composition-example)
   - [Custom Subclass Example (External Fallbacks)](#custom-subclass-example-external-fallbacks)
   - [Advanced CLI Pipeline Example](#advanced-cli-pipeline-example)

---

## Overview

`CLIExpander` accepts a tokenized list of strings (typically parsed via whitespace splitting from a command string) and evaluates `.expand.` blocks to generate expanded command-line argument lists.

### Key Capabilities

- **Combinatorial Expansion**: Easily generate Cartesian products and parameter sweeps across nested expansion blocks.
- **Sequential Expansion Blocks**: Multiple `.expand.` blocks can appear throughout a command line to expand independent sections.
- **Embedded Inner-Token Substitution**: Variables embedded inside compound tokens (e.g., `_item._metric` or `prefix._var.suffix`) are seamlessly resolved.
- **Lexical Scoping & Shadowing**: Push and pop variable bindings cleanly with support for variable shadowing in nested blocks.
- **Fragment Splitting on Exact Match**: Full-token variable values containing whitespace are automatically split into separate argument tokens.
- **Strict Whitespace Validation on Inner Substitutions**: Embedded substitutions validate that variable values do not contain whitespace to prevent creating malformed arguments.
- **Extensible via Subclassing**: Override `GetByKey()` or `Process()` and configure `MatchPartials` to inject custom variable resolution, external lookups, or output filters.
- **Informative Diagnostics**: Clear, actionable error messages for syntax mistakes such as missing delimiters or unclosed blocks.

---

## Syntax & Semantics

### Basic Expansion Block

An expansion block begins with `.expand.`, followed by the variable name, a whitespace-separated list of alternatives, a colon `:` delimiter, the template body, and closes with `.expand_end.`:

```text
.expand. <var_name> <alt1> <alt2> ... : <body_tokens> .expand_end.
```

#### Example

```text
.expand. _file Quant.tkr Quant2.tkr :
    process file _file
.expand_end.
```

**Output Tokens:**
```text
process file Quant.tkr process file Quant2.tkr
```

---

### Cartesian Product (Nested Expansion)

Nesting `.expand.` blocks computes the Cartesian product over all combinations in outer-to-inner order:

```text
.expand. _file Quant.tkr Quant2.tkr :
    .expand. _size 10B 100B :
        tracker window by_total _size file _file
    .expand_end.
.expand_end.
```

**Output Tokens:**
```text
tracker window by_total 10B file Quant.tkr
tracker window by_total 100B file Quant.tkr
tracker window by_total 10B file Quant2.tkr
tracker window by_total 100B file Quant2.tkr
```

---

### Sequential Expansion Blocks

Multiple `.expand.` blocks can be placed in sequence across a command line, allowing different segments or arguments before and after delimiters (like `.END.`) to be generated independently:

```text
.expand. _file Quant.tkr Quant2.tkr :
    file _file
.expand_end.
.END.
.expand. _metric mean#AnticipatedPercentage mean#ZScore :
    item_0._metric
.expand_end.
```

---

### Token Concatenation (`.join.` ... `.join_end.`)

A token concatenation block begins with `.join.` and closes with `.join_end.`. It evaluates all body tokens according to standard `CLIExpander` expansion and substitution rules, and concatenates all emitted tokens into **one output token with no separator** ($N\text{ tokens} \to 1\text{ token}$):

```text
.join. <body_tokens...> .join_end.
```

#### Whitespace Semantics (Preserving Internal Spaces)

`.join.` removes **token boundaries**, not characters inside tokens. Any whitespace that is part of a single token (such as `"my file"`) is preserved verbatim in the resulting joined token:

```text
.join. /home/jwc/ "my file" .join_end.
```

**Output Tokens:**
```text
["/home/jwc/my file"]  (1 token)
```

#### Empty Join Semantics

An empty join block `.join. .join_end.` emits exactly one empty string token (`""`), providing a deliberate mechanism to manufacture empty argument values.

#### Filename and Path Construction

```text
.join. Quant . tkr .join_end.
.join. /tmp/ report . csv .join_end.
```

**Output Tokens:**
```text
["Quant.tkr", "/tmp/report.csv"]
```

#### Cartesian Expansion with Join (`.join.` inside `.expand.`)

Nesting `.join.` inside `.expand.` blocks allows dynamic assembly of filenames, extensions, and paths across parameter sweeps:

```text
.expand. _base Quant Quant2 :
    .expand. _ext tkr csv :
        .join. _base . _ext .join_end.
    .expand_end.
.expand_end.
```

**Output Tokens:**
```text
Quant.tkr Quant.csv Quant2.tkr Quant2.csv
```

#### Multi-Branch Concatenation (`.expand.` inside `.join.`)

When an `.expand.` block is nested inside a `.join.` block, all emissions from every branch of the expansion are evaluated into the join stream and assembled into a single continuous token:

```text
.join. prefix- .expand. _x a b : _x .expand_end. suffix .join_end.
```

**Output Tokens:**
```text
["prefix-absuffix"]
```

#### Nested Joins

Nested `.join.` blocks evaluate recursively from inner to outer:

```text
.join. root/ .join. child / leaf .join_end. .txt .join_end.
```

**Output Tokens:**
```text
["root/child/leaf.txt"]
```

---

### Explicit Known Values (`.get.` and `.split_get.`)

`CLIExpander` provides explicit known-value resolution via two dedicated tokens:
- **`.get. <key>`**: Retrieves the known value mapped to `<key>` and emits it as **one exact token**, without splitting even if the value contains whitespace.
- **`.split_get. <key>`**: Retrieves the known value mapped to `<key>` and splits it by whitespace into individual tokens using the virtual `Split()` method.

```text
.get. <key>
.split_get. <key>
```

#### Example

```text
echo .get. newline .get. space hello
```

**Output Tokens:**
```text
["echo", "\n", " ", "hello"]
```

#### Built-In Known Values

Every `CLIExpander` instance captures an invariant creation timestamp `StartedAt` and seeds the following default known values:

| Key | Description | Value |
|---|---|---|
| `newline` | Platform-specific newline string | `Environment.NewLine` |
| `space` | Single space string | `" "` |
| `tab` | Single horizontal tab character | `"\t"` |
| `empty` | Empty string | `""` |
| `now` | Local instance timestamp in ISO-8601 round-trip format | `StartedAt.ToString("O")` |
| `utc_now` | UTC instance timestamp in ISO-8601 round-trip format | `StartedAt.ToUniversalTime().ToString("O")` |
| `timestamp` | Local timestamp in identifier/file format (`_yyyyMMdd_HHmmss_ff`) | `StartedAt.ToString("_yyyyMMdd_HHmmss_ff")` |
| `utimestamp` | UTC timestamp in identifier/file format (`_yyyyMMdd_HHmmss_ff`) | `StartedAt.ToUniversalTime().ToString("_yyyyMMdd_HHmmss_ff")` |
| `dir_sep` | Platform native directory separator character | `Path.DirectorySeparatorChar.ToString()` |
| `path_sep` | Platform native path separator character | `Path.PathSeparator.ToString()` |

#### Instance Timestamp Stability

The `StartedAt` timestamp is captured **once** when the `CLIExpander` instance is created. All time-based lookups (`now`, `utc_now`, `timestamp`, `utimestamp`) in that expander instance resolve to the exact same captured instant:

```text
.expand. x a b c :
    .get. now
.expand_end.
```
All branches receive identical timestamp strings.

#### Composition with `.join.`

`.get.` integrates directly within `.join. ... .join_end.` concatenation blocks:

```text
.join. /var/log/app- .get. timestamp .log .join_end.
```

**Output Token:**
```text
["/var/log/app-_20261003_090929_00.log"]
```

---

### Scope vs. MacroStore vs. ValueStore Distinction

`CLIExpander` enforces a clean architectural separation across three tiers of binding and resolution:

| Characteristic | Transient Scope (`Scope`) | Baseline Macros (`MacroStore`) | Known Values Store (`ValueStore`) |
|---|---|---|---|
| **Syntax** | `<name>` or embedded identifier | `<name>` or embedded identifier | `.get. <key>` / `.split_get. <key>` |
| **Origin** | Created dynamically by `.expand.` blocks | Populated programmatically or via configuration | Stored in `ValueStore` or resolved dynamically |
| **Resolution Method** | `GetByKey(string key)` | `GetByKey(string key)` (fallback) | `GetKnownValue(string key)` |
| **Emission Mode** | Subject to `SplitAfterGetByKey` and `MatchPartials` | Subject to `SplitAfterGetByKey` and `MatchPartials` | `.get.` is strictly one token; `.split_get.` splits whitespace |
| **Precedence** | Highest priority (shadows `MacroStore`) | Active across entire template when unbound in `Scope` | Independent of `.expand.` variable bindings |

Bare words such as `now`, `newline`, or `project` appearing in the argument list remain untouched literal words unless they are bound in an `.expand.` block or defined in `MacroStore`. Conversely, `.get. now` explicitly looks up the known value in `ValueStore`.

Even if a scope binding shares the name of a known value:
```text
.expand. now yesterday tomorrow :
    now .get. now
.expand_end.
```
`now` resolves to `yesterday` / `tomorrow` via lexical `GetByKey()`, while `.get. now` resolves to the captured `StartedAt` timestamp via `GetKnownValue()`.

---

### Variable Scoping & Shadowing

- Variables are resolved in a last-in, first-out (LIFO) scoped stack via `GetByKey()`.
- If an inner `.expand.` block declares a variable with the same name as an outer block (or a `MacroStore` baseline entry), the inner variable shadows the outer/baseline binding during inner block evaluation.
- When the inner block finishes, the scope watermarks unwind and the outer/baseline binding is restored.

#### Example

```text
.expand. _val outer1 outer2 :
    .expand. _val inner1 inner2 :
        _val
    .expand_end.
.expand_end.
```

**Output Tokens:**
```text
inner1 inner2 inner1 inner2
```

---

### Baseline Macros (`MacroStore`)

`CLIExpander` provides a `MacroStore` dictionary (`IDictionary<string, string>`) for baseline macro bindings that apply implicitly throughout the template without requiring outer `.expand.` boilerplate:

```csharp
var expander = new CLIExpander();
expander.MacroStore["CONFIG"] = "Release";
expander.MacroStore["ARCH"] = "x64";
```

When evaluating a token `CONFIG`, `GetByKey("CONFIG")` first checks the active `Scope` stack. If `CONFIG` is not currently bound in an active `.expand.` block, it retrieves `"Release"` from `MacroStore`.

Full-token splitting (`SplitAfterGetByKey`) and embedded identifier substitutions (`MatchPartials`) apply to `MacroStore` values identically to `Scope` variables.

---

### Full-Token (Fragment) vs. Inner-Token Substitution

`CLIExpander` supports two modes of variable substitution depending on how the variable appears in the token:

1. **Full-Token Match (Fragment Splitting)**:
   - When a token exactly matches a defined variable name (e.g. `_flags`), its bound value is looked up.
   - If the value contains whitespace (e.g. `"--all --verbose"`), it is automatically split into discrete argument tokens: `["--all", "--verbose"]`.
2. **Inner-Ton Match (Embedded Identifiers)**:
   - When a token contains variable identifiers surrounded by other characters (e.g. `_item._metric`, `prefix._var.suffix`, `file._var`), `CLIExpander` parses each identifier (`[A-Za-z_][A-Za-z0-9_]*`) and substitutes its value in-place within the token.
     ke  - **Whitespace Safety Rule**: If a variable value used in an inner substitution contains whitespace, `CLIExpander` returns an error:
     ```text
     CLIExpander: Variable '<id>' contains whitespace
     ```
     This prevents creating unintended multi-word compound tokens.

---

### Empty Alternatives

If an `.expand.` block contains zero alternatives (e.g. `.expand. _opt : .expand_end.`), the loop executes 0 times and produces 0 output tokens. If a non-empty body is provided with zero alternatives, the parser returns a syntax error indicating that `.expand_end.` was expected.

---

## Extensibility & Subclassing

`CLIExpander` is designed to be easily extended via subclassing and configuration, allowing applications to customize variable lookup, configure known values, control partial matching behavior, or intercept token evaluation.

### Virtual Resolution (`GetByKey` vs `GetKnownValue`)

`CLIExpander` provides two authoritative virtual lookup methods:

1. **`GetByKey(string key)` (Lexical Macro Lookup)**:
   - Consulted during `.expand.` macro and embedded identifier substitution.
   - Default implementation queries the `Scope` stack in LIFO order.
   - Subclasses can override it to supply dynamic fallback macro variables (e.g. environment variables).
2. **`GetKnownValue(string key)` (Explicit Known-Value Lookup)**:
   - Consulted exclusively during `.get. <key>` and `.split_get. <key>` evaluations.
   - Default implementation queries the `ValueStore` dictionary.
   - Subclasses can override it to provide dynamic, computed, or external known-value resolution with final behavioral authority.

```csharp
public class CustomExpander : CLIExpander
{
    public override string? GetKnownValue(string key)
    {
        if (key == "machine")
            return Environment.MachineName;

        return base.GetKnownValue(key);
    }
}
```

### MacroStore Configuration

The `MacroStore` property (`IDictionary<string, string>`) holds baseline macro variable bindings that apply across the entire template whenever a variable is not bound in an active `.expand.` block.

Callers can configure baseline macros directly without subclassing:

```csharp
var expander = new CLIExpander();
expander.MacroStore["CONFIG"] = "Release";
expander.MacroStore["ARCH"] = "x64";
```

### ValueStore Configuration & Seeding

The `ValueStore` property (`IDictionary<string, string>`) holds explicit known values. It is seeded in `InitializeValueStore()` with default built-in values (`newline`, `space`, `tab`, `empty`, `now`, `utc_now`, `timestamp`, `utimestamp`, `dir_sep`, `path_sep`).

Callers can configure or populate `ValueStore` directly without subclassing:

```csharp
var expander = new CLIExpander();
expander.ValueStore["project"] = "TruthInTheFlip";
expander.ValueStore["version"] = "1.0.0";
```

### Custom Token Splitting (`Split`)

The `Split(string text)` method is `virtual` and returns an `IEnumerable<string>`. Subclasses can override it to customize how multi-value variable substitutions and `.split_get.` tokens are partitioned into distinct tokens (for example, splitting by commas, custom delimiters, or implementing quote-preserving tokenization):

```csharp
public class CommaDelimitedExpander : CLIExpander
{
    public override IEnumerable<string> Split(string text)
    {
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
```

### Custom Identifier Characters (`IsIdentifierStart` & `IsIdentifierPart`)

The `IsIdentifierStart(char c)` and `IsIdentifierPart(char c)` methods are `virtual`. Subclasses can override them to customize what characters are treated as valid variable identifiers during embedded inner-token substitution (for instance, allowing `$` prefixes or kebab-case identifiers):

```csharp
public class CustomIdentifierExpander : CLIExpander
{
    public override bool IsIdentifierStart(char c) => base.IsIdentifierStart(c) || c == '$';
    public override bool IsIdentifierPart(char c) => base.IsIdentifierPart(c) || c == '$' || c == '-';
}
```

### Configurable Syntax Tokens

All block and operation syntax delimiters are public assignable fields on `CLIExpander`, allowing callers and derived classes to reconfigure template syntax conventions without modifying parser logic:

```csharp
var expander = new CLIExpander
{
    ExpandStartToken = "@expand",
    ExpandEndToken = "@end",
    DelimiterToken = "in",
    JoinStartToken = "@join",
    JoinEndToken = "@endjoin",
    GetToken = "@get",
    SplitGetToken = "@split_get"
};
```

### Token Splitting Control (`SplitAfterGetByKey`)

The public field `SplitAfterGetByKey` (default `true`) controls whether full-token variable matches containing whitespace are automatically split into separate output tokens via whitespace delimiters. Setting `SplitAfterGetByKey = false` causes the resolved value string to be emitted directly as a single argument token.

### Partial Matching Control (`MatchPartials`)

The public field `MatchPartials` (default `true`) controls whether `CLIExpander` resolves inner-token variable expressions (such as `prefix._var.suffix`). Setting `MatchPartials = false` disables inner token substitution so that only exact full-token matches are substituted.

### Custom Expansion Pipeline (`Process`)

The instance `Process(List<string> input, ref int index, List<string> output)` method is `virtual`. Subclasses can override it to preprocess, postprocess, or intercept tokens during parsing.

### Factory Delegate Support

The static `CLIExpander.Process` entry point accepts an optional factory delegate `Func<CLIExpander>? newExpander = null`, making it straightforward to invoke static expansion using a derived class:

```csharp
var status = CLIExpander.Process(input, out var output, () => new EnvironmentExpander());
```

---

## API Reference

Namespace: `CLIExpanderNs`

### `CLIExpander.Process (Static)`

```csharp
public static CLReturn Process(
    List<string> input, 
    out List<string> output, 
    Func<CLIExpander>? newExpander = null)
```

- **`input`**: The input list of tokens.
- **`output`**: Receives the resulting expanded list of string tokens.
- **`newExpander`**: Optional factory delegate returning a custom `CLIExpander` instance or subclass.
- **Returns**: A `CLReturn` structure indicating status code (0 on success, non-zero on failure) and an optional error message.

---

### `CLIExpander.Process (Instance)`

```csharp
public virtual CLReturn Process(List<string> input, ref int index, List<string> output)
```

- Recursive worker method that processes input starting at `index` and appends expanded tokens to `output`. Can be overridden by subclasses.

---

### `CLIExpander.GetByKey (Instance)`

```csharp
public virtual string? GetByKey(string key)
```

- Resolves a variable value from the scoped variable stack in LIFO order. Returns `null` if the variable is not found in active scopes. Can be overridden by subclasses to provide custom variable sources or fallbacks.

---

### `CLIExpander.GetKnownValue (Instance)`

```csharp
public virtual string? GetKnownValue(string key)
```

- Authoritative resolution seam for explicit known values requested via `.get. <key>` or `.split_get. <key>`.
- Default implementation checks the `ValueStore` dictionary.
- Subclasses can override this method to provide custom, external, or dynamic known-value resolution (e.g. system properties, machine names, runtime lookups).

---

### `CLIExpander.InitializeValueStore (Instance)`

```csharp
protected virtual void InitializeValueStore()
```

- Seeds the default known values into `ValueStore` upon instance instantiation. Subclasses can override this method to customize or seed initial known values.

---

### `CLIExpander.Split (Instance)`

```csharp
public virtual IEnumerable<string> Split(string text)
```

- Splits a string value into an enumerable sequence of individual tokens. By default, splits on whitespace (`StringSplitOptions.RemoveEmptyEntries`).
- Subclasses can override this method to provide custom token splitting semantics (e.g., custom delimiters, regex splits, or quote-aware tokenization).

---

### `CLIExpander.IsIdentifierStart (Instance)`

```csharp
public virtual bool IsIdentifierStart(char c)
```

- Determines whether the character `c` is valid as the initial character of an identifier for inner-token substitution.
- Default implementation accepts ASCII letters (`a-z`, `A-Z`) and underscore (`_`).

---

### `CLIExpander.IsIdentifierPart (Instance)`

```csharp
public virtual bool IsIdentifierPart(char c)
```

- Determines whether the character `c` is valid as a continuation character in an identifier for inner-token substitution.
- Default implementation accepts ASCII letters (`a-z`, `A-Z`), digits (`0-9`), and underscore (`_`).

---

### `StartedAt (Property)`

```csharp
public DateTimeOffset StartedAt { get; }
```

- Captures the exact creation timestamp of the `CLIExpander` instance. All time-based built-ins (`now`, `utc_now`, `timestamp`, `utimestamp`) derive immutably from this instant.

---

### `MacroStore (Property)`

```csharp
public IDictionary<string, string> MacroStore { get; set; }
```

- Backing dictionary of baseline lexical macros consulted by `GetByKey()` when a variable is not found in the active lexical `Scope`. Case-sensitive (`StringComparer.Ordinal`).

---

### `ValueStore (Property)`

```csharp
public IDictionary<string, string> ValueStore { get; set; }
```

- Backing dictionary of known values consulted by the default `GetKnownValue()` implementation. Case-sensitive (`StringComparer.Ordinal`).

---

### `ExpandStartToken (Field)`

```csharp
public string ExpandStartToken = ".expand.";
```

- Token identifying the start of an expansion block. Defaults to `".expand."`.

---

### `ExpandEndToken (Field)`

```csharp
public string ExpandEndToken = ".expand_end.";
```

- Token identifying the termination of an expansion block. Defaults to `".expand_end."`.

---

### `DelimiterToken (Field)`

```csharp
public string DelimiterToken = ":";
```

- Token separating the expansion variable alternative values from the block template body. Defaults to `":"`.

---

### `JoinStartToken (Field)`

```csharp
public string JoinStartToken = ".join.";
```

- Token identifying the beginning of a token concatenation block. Defaults to `".join."`.

---

### `JoinEndToken (Field)`

```csharp
public string JoinEndToken = ".join_end.";
```

- Token identifying the termination of a token concatenation block. Defaults to `".join_end."`.

---

### `GetToken (Field)`

```csharp
public string GetToken = ".get.";
```

- Token identifying an explicit known-value retrieval operation emitting a single exact token. Defaults to `".get."`.

---

### `SplitGetToken (Field)`

```csharp
public string SplitGetToken = ".split_get.";
```

- Token identifying an explicit known-value retrieval operation splitting the value on whitespace into multiple tokens. Defaults to `".split_get."`.

---

### `SplitAfterGetByKey (Field)`

```csharp
public bool SplitAfterGetByKey = true;
```

- When `true`, whitespace-separated values from full-token variable matches are split into individual tokens.
- When `false`, the resolved variable string is emitted directly as a single argument token.

---

### `MatchPartials (Field)`

```csharp
public bool MatchPartials = true;
```

- When `true`, enables embedded inner identifier substitution (`prefix._var.suffix`).
- When `false`, only full-token exact matches (`_var`) are substituted.

---

### `CLReturn` Struct

```csharp
public struct CLReturn
{
    public int Status;       // 0 for success, non-zero for error
    public string? Message;  // Descriptive error message, or null on success
}
```

---

### Scope & Watermarks

- **`Scope`** (`List<(string, string)>`): Active variable bindings stack.
- **`ScopeWaterMarks`** (`Stack<int>`): High-water marks for restoring scope when exiting blocks.

---

## Error Handling & Diagnostic Messages

`CLIExpander` provides descriptive, meaningful error messages for syntax and parsing issues:

| Error Message | Cause |
|---|---|
| `CLIExpander: Unexpected end of input` | The input list is empty or processing began past the end of the input list. |
| `CLIExpander: Expected variable name after .expand.` | Encountered `.expand.` at the end of input with no variable name following it. |
| `CLIExpander: Missing ':' after variable '<name>'` | Variable declared after `.expand.` but the input ended before any colon `:` or values. |
| `CLIExpander: Missing ':' delimiter for variable '<name>'` | Values provided after variable name, but reached end of input without a `:` delimiter. |
| `CLIExpander: Expected .expand_end.` | An `.expand.` block body reached end of input or was not properly closed with `.expand_end.`. |
| `CLIExpander: Unexpected .expand_end.` | Encountered an un-matched `.expand_end.` token at the root level or inside a join block. |
| `CLIExpander: Expected .join_end.` | A `.join.` block reached end of input without being closed by `.join_end.`. |
| `CLIExpander: Unexpected .join_end.` | Encountered an un-matched `.join_end.` token at the root level. |
| `CLIExpander: Expected key after .get.` | Encountered `.get.` at the end of input with no following key token. |
| `CLIExpander: Expected key after .split_get.` | Encountered `.split_get.` at the end of input with no following key token. |
| `CLIExpander: Unknown value '<key>'` | Explicit known-value lookup via `.get.` or `.split_get.` failed to resolve the specified key. |
| `CLIExpander: Incomplete parsing of input at '<token>'` | Unparsed trailing tokens remaining after top-level block processing. |
| `CLIExpander: Variable '<name>' contains whitespace` | Inner-token substitution variable value contained whitespace characters. |

---

## Usage Examples

### C# Quickstart

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using CLIExpanderNs;

string command = @".expand. _file Quant.tkr Quant2.tkr :
    .expand. _size 10B 100B :
        tracker window by_total _size file _file
    .expand_end.
.expand_end.";

List<string> input = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();

var status = CLIExpander.Process(input, out var output);
if (status.Status != 0)
{
    Console.Error.WriteLine(status.Message);
    return;
}

Console.WriteLine(string.Join(" ", output));
```

### Known Values & Join Composition Example

```csharp
using System;
using System.Collections.Generic;
using CLIExpanderNs;

var expander = new CLIExpander();
expander.ValueStore["output_dir"] = "/tmp/reports";
expander.ValueStore["dataset"] = "quant_data";

var input = new List<string>
{
    ".expand.", "fmt", "csv", "json", ":",
        ".join.", ".get.", "output_dir", "/", ".get.", "dataset", "-", ".get.", "timestamp", ".", "fmt", ".join_end.",
        ".get.", "newline",
    ".expand_end."
};

var status = CLIExpander.Process(input, out var output, () => expander);
if (status.Status == 0)
{
    Console.Write(string.Concat(output));
}
```

### Custom Subclass Example (External Fallbacks)

```csharp
using System;
using System.Collections.Generic;
using CLIExpanderNs;

public class CustomConfigExpander : CLIExpander
{
    private readonly Dictionary<string, string> _globals;

    public CustomConfigExpander(Dictionary<string, string> globals)
    {
        _globals = globals;
    }

    public override string? GetByKey(string key)
    {
        var scoped = base.GetByKey(key);
        if (scoped != null) return scoped;

        return _globals.TryGetValue(key, out var val) ? val : null;
    }
}

// Usage with static factory overload:
var globals = new Dictionary<string, string>
{
    { "_env", "production" },
    { "_ext", "json" }
};

var input = new List<string> { ".expand.", "_svc", "auth", "api", ":", "deploy", "_svc", "_env", "config._ext", ".expand_end." };
var result = CLIExpander.Process(input, out var output, () => new CustomConfigExpander(globals));
// Output: ["deploy", "auth", "production", "config.json", "deploy", "api", "production", "config.json"]
```

### Advanced CLI Pipeline Example

Using nested Cartesian expansions for input streams combined with sequential inner-token expansions for metric selection:

```bash
TruthInTheFlip_Farm pretty zip \
  .expand. _file Quant.tkr Quant2.tkr : \
    .expand. _size 10B 100B : \
      segment window by_total _size file _file by_total 100B \
    .expand_end. \
  .expand_end. \
  .END. item_0.Index \
  .expand. _item item_0 item_1 item_2 item_3 : \
    .expand. _metric mean#AnticipatedPercentage mean#ZScore : \
      _item._metric \
    .expand_end. \
  .expand_end.
```

**Expanded Output:**
```text
TruthInTheFlip_Farm pretty zip
  segment window by_total 10B file Quant.tkr by_total 100B
  segment window by_total 100B file Quant.tkr by_total 100B
  segment window by_total 10B file Quant2.tkr by_total 100B
  segment window by_total 100B file Quant2.tkr by_total 100B
  .END. item_0.Index
  item_0.mean#AnticipatedPercentage item_0.mean#ZScore
  item_1.mean#AnticipatedPercentage item_1.mean#ZScore
  item_2.mean#AnticipatedPercentage item_2.mean#ZScore
  item_3.mean#AnticipatedPercentage item_3.mean#ZScore
```
