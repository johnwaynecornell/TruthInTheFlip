# CLIExpander

`CLIExpander` is a lightweight command-line argument expansion and template substitution library for .NET. It allows generating combinatorial argument lists (Cartesian products), parameter sweeps, and template expansions using simple `.expand.` block syntax.

---

## Table of Contents

1. [Overview](#overview)
2. [Syntax & Semantics](#syntax--semantics)
   - [Basic Expansion Block](#basic-expansion-block)
   - [Cartesian Product (Nested Expansion)](#cartesian-product-nested-expansion)
   - [Sequential Expansion Blocks](#sequential-expansion-blocks)
   - [Variable Scoping & Shadowing](#variable-scoping--shadowing)
   - [Full-Token (Fragment) vs. Inner-Token Substitution](#full-token-fragment-vs-inner-token-substitution)
   - [Empty Alternatives](#empty-alternatives)
3. [Extensibility & Subclassing](#extensibility--subclassing)
   - [Virtual Resolution (`GetByKey`)](#virtual-resolution-getbykey)
   - [Partial Matching Control (`MatchPartials`)](#partial-matching-control-matchpartials)
   - [Custom Expansion Pipeline (`Process`)](#custom-expansion-pipeline-process)
   - [Factory Delegate Support](#factory-delegate-support)
4. [API Reference](#api-reference)
   - [CLIExpander.Process (Static)](#cliexpanderprocess-static)
   - [CLIExpander.Process (Instance)](#cliexpanderprocess-instance)
   - [CLIExpander.GetByKey (Instance)](#cliexpandergetbykey-instance)
   - [MatchPartials (Field)](#matchpartials-field)
   - [CLReturn Struct](#clreturn-struct)
   - [Scope & Watermarks](#scope--watermarks)
5. [Error Handling & Diagnostic Messages](#error-handling--diagnostic-messages)
6. [Usage Examples](#usage-examples)
   - [C# Quickstart](#c-quickstart)
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

### Variable Scoping & Shadowing

- Variables are resolved in a last-in, first-out (LIFO) scoped stack via `GetByKey()`.
- If an inner `.expand.` block declares a variable with the same name as an outer block, the inner variable shadows the outer variable during inner block evaluation.
- When the inner block finishes, the scope watermarks unwind and the outer variable binding is restored.

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

### Full-Token (Fragment) vs. Inner-Token Substitution

`CLIExpander` supports two modes of variable substitution depending on how the variable appears in the token:

1. **Full-Token Match (Fragment Splitting)**:
   - When a token exactly matches a defined variable name (e.g. `_flags`), its bound value is looked up.
   - If the value contains whitespace (e.g. `"--all --verbose"`), it is automatically split into discrete argument tokens: `["--all", "--verbose"]`.
2. **Inner-Token Match (Embedded Identifiers)**:
   - When a token contains variable identifiers surrounded by other characters (e.g. `_item._metric`, `prefix._var.suffix`, `file._var`), `CLIExpander` parses each identifier (`[A-Za-z_][A-Za-z0-9_]*`) and substitutes its value in-place within the token.
   - **Whitespace Safety Rule**: If a variable value used in an inner substitution contains whitespace, `CLIExpander` returns an error:
     ```text
     CLIExpander: Variable '<id>' contains whitespace
     ```
     This prevents creating unintended multi-word compound tokens.

---

### Empty Alternatives

If an `.expand.` block contains zero alternatives (e.g. `.expand. _opt : .expand_end.`), the loop executes 0 times and produces 0 output tokens. If a non-empty body is provided with zero alternatives, the parser returns a syntax error indicating that `.expand_end.` was expected.

---

## Extensibility & Subclassing

`CLIExpander` is designed to be easily extended via subclassing, allowing applications to customize variable lookup, configure partial matching behavior, or intercept token evaluation.

### Virtual Resolution (`GetByKey`)

The `GetByKey(string key)` method is `virtual`. Subclasses can override it to supply dynamic or external variables, such as environment variables, configuration settings, or fallback dictionaries:

```csharp
public class EnvironmentExpander : CLIExpander
{
    public override string? GetByKey(string key)
    {
        // Check scoped block variables first
        var scoped = base.GetByKey(key);
        if (scoped != null) return scoped;

        // Fallback to environment variables
        return Environment.GetEnvironmentVariable(key);
    }
}
```

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
| `CLIExpander: Unexpected .expand_end.` | Encountered an un-matched `.expand_end.` token at the root level. |
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
