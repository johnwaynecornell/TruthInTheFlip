# CLIExpand

`CLIExpand` is a lightweight, standalone command-line host for the `CLIExpander` library. It provides fast, script-friendly argument expansion, Cartesian sweeps, and template substitution with shell-safe command serialization.

---

## Table of Contents

1. [Overview & Purpose](#overview--purpose)
2. [Command-Line Syntax](#command-line-syntax)
   - [The Hard `--` Boundary](#the-hard----boundary)
3. [Options Reference](#options-reference)
4. [Shell Modes & Quoting Semantics](#shell-modes--quoting-semantics)
   - [Bash Mode (`bash`)](#bash-mode-bash)
   - [PowerShell Mode (`ps`)](#powershell-mode-ps)
   - [Windows Command Prompt Mode (`cmd`)](#windows-command-prompt-mode-cmd)
   - [Raw Mode (`raw`)](#raw-mode-raw)
   - [Argument Composition vs. Command Composition](#argument-composition-vs-command-composition)
5. [Settings File & Configuration Precedence](#settings-file--configuration-precedence)
   - [Precedence Hierarchy](#precedence-hierarchy)
   - [Default Settings Locations](#default-settings-locations)
   - [JSON Settings Schema](#json-settings-schema)
6. [Examples & Pipeline Workflows](#examples--pipeline-workflows)
   - [Basic Expansion](#basic-expansion)
   - [Path and Filename Construction with Join](#path-and-filename-construction-with-join)
   - [Nested Cartesian Sweeps](#nested-cartesian-sweeps)
   - [PowerShell Script Generation](#powershell-script-generation)
   - [Custom Syntax Tokens](#custom-syntax-tokens)
   - [Shell Evaluation Pipelines](#shell-evaluation-pipelines)
   - [Multi-Command Script Composition](#multi-command-script-composition)
7. [Limitations & Shell Quirks](#limitations--shell-quirks)

---

## Overview & Purpose

`CLIExpand` acts as a pure transformation bridge:
1. Accepts ordinary command-line options and payload arguments.
2. Isolates host configuration from payload tokens using a hard `--` boundary.
3. Executes combinatorial expansion and variable substitution using the `CLIExpander` engine.
4. Serializes the resulting token array using a designated shell argument renderer.
5. Emits rendered text cleanly to `stdout` with zero status banners, directing diagnostics and error messages strictly to `stderr`.

```text
CLIExpand [options] -- <payload tokens...>
       │
       ▼
┌─────────────────────────┐
│     CLIOptionParser     │
└────────────┬────────────┘
             │ (before --)                   (after --)
             ▼                                   ▼
┌─────────────────────────┐             ┌─────────────────┐
│   CLIExpandSettings     │             │ Payload Tokens  │
│ (Merged via Precedence) │             └────────┬────────┘
└────────────┬────────────┘                      │
             │                                   │
             ▼                                   ▼
┌─────────────────────────────────────────────────────────┐
│                       CLIExpander                       │
│              (Expansion & Scoping Engine)               │
└────────────────────────────┬────────────────────────────┘
                             │
                             ▼ Expanded Token Array
┌─────────────────────────────────────────────────────────┐
│                  ICLIArgumentRenderer                   │
│             (Bash | PowerShell | Cmd | Raw)             │
└────────────────────────────┬────────────────────────────┘
                             │
                             ▼
                    stdout (Exit Code 0)
```

---

## Command-Line Syntax

```text
CLIExpand [options] -- <payload tokens...>
```

### The Hard `--` Boundary

The double-dash `--` token serves as an immutable boundary:
- **Tokens before `--`**: Parsed as `CLIExpand` configuration flags (e.g. `-mode`, `-begin`, `-delim`, `-no-partials`, `-settings`).
- **Tokens after `--`**: Owned entirely by `CLIExpander` and downstream tool semantics. Switch-like tokens (e.g. `-mode`, `--verbose`, `-f`) appearing after `--` are treated strictly as raw payload arguments and never parsed as host options.

---

## Options Reference

| Option | Aliases | Description | Default |
| :--- | :--- | :--- | :--- |
| `-mode <mode>` | `--mode` | Target shell rendering mode (`bash`, `ps`, `cmd`, `raw`). | `raw` |
| `-begin <token>` | `--begin`, `-start`, `--start` | Token marking the start of an expansion block. | `.expand.` |
| `-end <token>` | `--end` | Token marking the end of an expansion block. | `.expand_end.` |
| `-delim <token>` | `--delim`, `-delimiter`, `--delimiter` | Delimiter separating variable alternatives from template body. | `:` |
| `-join <token>` | `--join`, `-join-start`, `--join-start` | Token marking the start of a token concatenation block. | `.join.` |
| `-join-end <token>` | `--join-end`, `-joinend`, `--joinend` | Token marking the end of a token concatenation block. | `.join_end.` |
| `-get <token>` | `--get`, `-get-token`, `--get-token` | Token identifying explicit known-value retrieval. | `.get.` |
| `-split-get <token>` | `--split-get`, `-splitget`, `--splitget` | Token identifying explicit known-value retrieval with whitespace splitting. | `.split_get.` |
| `-values <path>` | `--values`, `-value`, `--value` | Path to a flat JSON value-map file (can be repeated to layer maps). | `none` |
| `-list-values` | `--list-values`, `-list`, `--list` | Lists all active known values (built-ins + loaded maps) to stdout and exits. | |
| `-macros <path>` | `--macros` | Path to a flat JSON macro-map file (can be repeated to layer macros). | `none` |
| `-macro <key>=<value>` | `--macro` | Inline baseline macro definition (can be repeated). | `none` |
| `-list-macros` | `--list-macros`, `-list-macro`, `--list-macro` | Lists all active baseline macros to stdout and exits. | |
| `-no-partials` | `--no-partials` | Disables inner-token substitutions (e.g. `prefix._var.suffix`). | Enabled (`true`) |
| `-partials` | `--partials` | Explicitly enables inner-token substitutions. | Enabled (`true`) |
| `-no-split` | `--no-split` | Preserves whitespace in full-token variable matches without splitting. | Splitting enabled (`true`) |
| `-split` | `--split` | Enables splitting full-token variable matches by whitespace. | Enabled (`true`) |
| `-settings <path>` | `--settings` | Path to a custom JSON configuration file. | `null` |
| `-h`, `-help`, `--help` | `-?`, `/?`, `help` | Prints usage information to stderr and exits with code 0. | |

---

## Shell Modes & Quoting Semantics

`CLIExpand` provides dedicated serializers designed to produce shell-safe command lines for downstream execution.

### Bash Mode (`bash`)
- **Safe Characters**: Tokens consisting purely of `[a-zA-Z0-9_./@:=+-]` remain unquoted.
- **Quoting Strategy**: All other tokens (containing spaces, tabs, newlines, `$`, `"`, `'`, `;`, `&`, `|`, `<`, `>`, `*`, `?`, `~`, `!`, `(`, `)`, `{`, `}`, `[`, `]`, `\`, etc.) are wrapped in POSIX single quotes `'...'`.
- **Single Quote Escaping**: Internal single quotes `'` are safely escaped via `'\''`.
- **Empty String**: Serialized as `''`.

### PowerShell Mode (`ps` / `powershell`)
- **Safe Characters**: Safe alphanumeric and path identifiers matching `[a-zA-Z0-9_./\\:+-]` remain unquoted.
- **Quoting Strategy**: Tokens containing whitespace or PowerShell special symbols (`$`, `@`, `` ` ``, `"`, `'`, `;`, `|`, `&`, `(`, `)`, `{`, `}`, `[`, `]`, `#`, etc.) are wrapped in single quotes `'...'`.
- **Single Quote Escaping**: Internal single quotes `'` are escaped by doubling: `''`.
- **Empty String**: Serialized as `''`.

### Windows Command Prompt Mode (`cmd` / `bat`)
- **Safe Characters**: Safe tokens matching `[a-zA-Z0-9_./\\:+-]` remain unquoted.
- **Quoting Strategy**: Tokens containing whitespace or cmd metacharacters (`&`, `<`, `>`, `|`, `^`, `%`, `"`, `(`, `)`) are wrapped in double quotes `"..."`.
- **CRT Escaping**: Conforms to standard Windows `CommandLineToArgvW` rules: internal double quotes are escaped as `\"`, and preceding backslashes are doubled.
- **Empty String**: Serialized as `""`.

### Raw Mode (`raw`)
- Renders tokens verbatim and normally inserts one space between adjacent tokens. If either side of a token boundary already contains whitespace (such as newlines `$'\n'`, tabs, or leading/trailing spaces), no additional separator is inserted.
- Essential for **command and script composition** (allowing shell operators like `;`, `&&`, `|`, and line breaks to pass through unquoted).
- Provides shell-neutral textual rendering, but is not lossless token transport when an argument itself contains whitespace or is empty. Shell-specific modes (`bash`, `ps`, `cmd`) preserve `argv` boundaries through quoting; `raw` intentionally does not.

### Argument Composition vs. Command Composition

Understanding whether you are composing **arguments for a single command** or **an entire multi-command script** determines which mode to select:

- **Argument Composition (`-mode bash`, `-mode ps`, `-mode cmd`)**:
  Guarantees literal data safety. Shell metacharacters (`;`, `&`, `|`, `$`, quotes, spaces) are automatically escaped/quoted so they are delivered verbatim to the target program as data arguments without triggering unintended shell control flow or subshells.
- **Command & Script Composition (`-mode raw`)**:
  Emits unquoted tokens directly, allowing shell control operators (`;`, `&&`, `||`, `|`, `>`) to retain their syntactic meaning so downstream evaluators (`eval`, `Invoke-Expression`, `cmd /c`, or shell subshells) interpret them as distinct sequential commands and pipelines.

---

## Settings File, Value Maps, Macros & Configuration Precedence

`CLIExpand` supports persistent configuration through JSON settings files, external JSON value maps, external JSON macro maps, and inline macro definitions.

### Precedence Hierarchy

Settings, known values, and baseline macros are resolved through a deterministic 5-layer hierarchy (later layers override earlier layers):

1. **Built-in Known Values & Defaults**: Default engine settings (`mode: raw`, `begin: .expand.`, `end: .expand_end.`, `delim: :`, `join: .join.`, `joinEnd: .join_end.`, `get: .get.`, `splitGet: .split_get.`) and built-in known values (`newline`, `space`, `tab`, `empty`, `now`, `utc_now`, `timestamp`, `utimestamp`, `dir_sep`, `path_sep`).
2. **Default User Settings File**: Value maps (`values`, `valueStore`), macro maps (`macros`, `macroStore`), and configuration loaded from the user's standard `settings.json`.
3. **Explicit Settings File (`-settings <path>`)**: Value maps (`values`, `valueStore`), macro maps (`macros`, `macroStore`), and configuration loaded from an explicitly designated settings JSON file.
4. **Command-Line Maps & Inline Definitions**:
   - Flat JSON value maps supplied on the CLI via repeated `-values <path>` options, evaluated in left-to-right order.
   - Flat JSON macro maps supplied on the CLI via repeated `-macros <path>` options, evaluated in left-to-right order.
   - Inline macro definitions supplied on the CLI via repeated `-macro <key>=<value>` options, evaluated in left-to-right order.
5. **Command-Line Syntax & Mode Overrides**: Explicit CLI switches (`-mode`, `-begin`, `-end`, `-delim`, `-join`, `-join-end`, `-get`, `-split-get`, `-no-partials`, `-no-split`).

### Relative Path Resolution Rules

- **Settings Files (`"values": [ ... ]`, `"macros": [ ... ]`)**: File paths listed inside a JSON settings file resolve **relative to that settings file's directory**.
- **CLI Options (`-values <path>`, `-macros <path>`)**: File paths supplied directly on the command line resolve **relative to the current working directory**.

### Flat JSON Value Maps vs. Macro Maps

| Feature | Known Value Maps (`-values` / `"values"`) | Baseline Macro Maps (`-macros` / `-macro` / `"macros"`) |
|---|---|---|
| **Syntax** | Explicit retrieval: `.get. <key>` or `.split_get. <key>` | Implicit lexical substitution: `<key>` or embedded `prefix.<key>.suffix` |
| **Emission** | `.get.` is strictly one exact token; `.split_get.` splits whitespace | Splits whitespace into multiple tokens (subject to `SplitAfterGetByKey`) |
| **Scope Interaction** | Independent of `.expand.` blocks | Shadowed dynamically by `.expand. <key> ...` blocks; restored after block ends |
| **Use Case** | Shared data constants, platform separators, stable timestamps | Template parameterization without repetitive `.expand. VAR val : ...` boilerplate |

### Flat JSON Macro Maps & Inline Macros

Load external macro maps from flat JSON files containing string key-value mappings:

```json
{
  "CONFIG": "Release",
  "ARCH": "x64",
  "OUT_DIR": "/var/builds"
}
```

```bash
# Load from macro files and provide ad-hoc inline macro overrides:
CLIExpand \
  -macros base-build.json \
  -macro ARCH=arm64 \
  -- dotnet publish -c CONFIG -a ARCH -o OUT_DIR
```

### Inspecting Active Known Values (`-list-values`)

To inspect all active known values (including built-ins, settings maps, and CLI `-values` overrides) without executing an expansion payload:

```bash
# List default built-in values
CLIExpand -list-values

# List active values with layered settings and value maps
CLIExpand -settings project.json -values overrides.json -list-values
```

### Inspecting Active Baseline Macros (`-list-macros`)

To inspect all active baseline macros (including settings macros, `-macros` files, and inline `-macro` definitions) without executing an expansion payload:

```bash
# List active macros
CLIExpand -settings project.json -macros env.json -macro REGION=us-east-1 -list-macros
```

**Example Output:**
```text
Active Macros (3 keys):
  ARCH    = x64
  CONFIG  = Release
  REGION  = us-east-1
```

### Default Settings Locations

When no `-settings <path>` is supplied, `CLIExpand` searches standard per-user configuration paths:

- **Linux / Unix**: `$XDG_CONFIG_HOME/CLIExpand/settings.json` (or `~/.config/CLIExpand/settings.json`).
- **Windows**: `%APPDATA%\CLIExpand\settings.json` (e.g. `C:\Users\<User>\AppData\Roaming\CLIExpand\settings.json`).
- **macOS**: `~/Library/Application Support/CLIExpand/settings.json` (with fallback to `~/.config/CLIExpand/settings.json`).

### JSON Settings Schema

```json
{
  "mode": "raw",
  "begin": ".expand.",
  "end": ".expand_end.",
  "delimiter": ":",
  "join": ".join.",
  "joinEnd": ".join_end.",
  "get": ".get.",
  "splitGet": ".split_get.",
  "matchPartials": true,
  "splitAfterGetByKey": true,
  "values": [
    "common.json",
    "project.json"
  ],
  "valueStore": {
    "custom_root": "/opt/app"
  },
  "macros": [
    "common-macros.json"
  ],
  "macroStore": {
    "CONFIG": "Release"
  }
}
```

*Note: Alternate property names such as `expandStartToken`, `expandEndToken`, `delimiterToken`, `joinStartToken`, `joinEndToken`, `getToken`, and `splitGetToken` are also supported.*

---

## Examples & Pipeline Workflows

### Basic Expansion

```bash
CLIExpand -- .expand. _text a b : echo _text .expand_end.
```

**Output:**
```text
echo a echo b
```

### Portable Multi-Line Parameter Sweeps (`.get. newline`)

Using `.get. newline` provides a portable, cross-shell method to generate multi-line output without relying on shell-specific escape syntax like Bash `$'\n'`:

```bash
CLIExpand -- \
  .expand. Fruit apple orange grape : \
    .expand. Size small medium large : \
      .join. Fruit _ Size .jpeg .join_end. \
      .get. newline \
    .expand_end. \
  .expand_end.
```

**Output:**
```text
apple_small.jpeg
apple_medium.jpeg
apple_large.jpeg
orange_small.jpeg
orange_medium.jpeg
orange_large.jpeg
grape_small.jpeg
grape_medium.jpeg
grape_large.jpeg
```

### Path and Filename Construction with Join & Known Values

Use `.join.` with `.get.` to assemble platform-neutral paths and captured timestamps:

```bash
CLIExpand -values config.json -- \
  .join. .get. output_root .get. dir_sep report- .get. timestamp .get. extension .join_end.
```

**Output:**
```text
/tmp/results/report-_20261003_090929_00.tkr
```

#### Cartesian Expansion with Join
```bash
CLIExpand -- .expand. _base Quant Quant2 : .expand. _ext tkr csv : .join. _base . _ext .join_end. .expand_end. .expand_end.
```
**Output:**
```text
Quant.tkr Quant.csv Quant2.tkr Quant2.csv
```

#### Preserving Spaced Arguments Inside Joined Paths
When generating shell-safe command lines (e.g. `-mode bash`), `.join.` and `.get.` preserve internal spaces within tokens while producing exactly one quoted argument:
```bash
CLIExpand -mode bash -- .join. /home/jwc/ "my file" .join_end.
```
**Output:**
```text
'/home/jwc/my file'
```

### Nested Cartesian Sweeps

```bash
CLIExpand -- .expand. _env dev prod : .expand. _region us-east us-west : deploy --env _env --region _region .expand_end. .expand_end.
```

**Output:**
```text
deploy --env dev --region us-east deploy --env dev --region us-west deploy --env prod --region us-east deploy --env prod --region us-west
```

### PowerShell Script Generation

Target PowerShell syntax regardless of the operating system hosting `CLIExpand`:

```bash
CLIExpand -mode ps -- .expand. _file app1 app2 : Compress-Archive -Path _file -DestinationPath _file.zip .expand_end.
```

**Output:**
```text
Compress-Archive -Path app1 -DestinationPath app1.zip Compress-Archive -Path app2 -DestinationPath app2.zip
```

### Custom Syntax Tokens

```bash
CLIExpand -begin @expand -end @end -delim in -- @expand _tier standard premium in create-tier _tier @end
```

**Output:**
```text
create-tier standard create-tier premium
```

### Shell Evaluation Pipelines

Because `CLIExpand` writes exclusively to `stdout` with no banners, it integrates directly with shell evaluation constructs:

#### Bash / Zsh
```bash
eval "$(CLIExpand -mode bash -- .expand. _dir src bin docs : ls -la _dir .expand_end.)"
```

#### PowerShell
```powershell
Invoke-Expression (CLIExpand -mode ps -- .expand. _service auth payment : Start-Service _service .expand_end.)
```

### Multi-Command Script Composition

When generating sequences of commands separated by shell operators (`;`, `&&`, `&`), use `-mode raw` so operators are not quote-wrapped:

#### Bash / POSIX Multi-Command
```bash
CLIExpand -mode raw -- .expand. _dir src bin tests : mkdir -p _dir ';' touch _dir/.gitkeep ';' .expand_end.
```
**Output:**
```text
mkdir -p src ; touch src/.gitkeep ; mkdir -p bin ; touch bin/.gitkeep ; mkdir -p tests ; touch tests/.gitkeep ;
```
**Execution:**
```bash
eval "$(CLIExpand -mode raw -- .expand. _dir src bin tests : mkdir -p _dir ';' touch _dir/.gitkeep ';' .expand_end.)"
```

#### PowerShell Multi-Command
```powershell
CLIExpand -mode raw -- .expand. _svc auth billing : Write-Host "Restarting _svc" ';' Restart-Service _svc ';' .expand_end.
```
**Output:**
```text
Write-Host "Restarting auth" ; Restart-Service auth ; Write-Host "Restarting billing" ; Restart-Service billing ;
```
**Execution:**
```powershell
Invoke-Expression (CLIExpand -mode raw -- .expand. _svc auth billing : Write-Host "Restarting _svc" ';' Restart-Service _svc ';' .expand_end.)
```

#### Windows Command Prompt (cmd.exe) Batch
```cmd
CLIExpand -mode raw -- .expand. _dir logs backups : mkdir _dir "&" echo Initialized > _dir\init.txt "&" .expand_end.
```
**Output:**
```text
mkdir logs & echo Initialized > logs\init.txt & mkdir backups & echo Initialized > backups\init.txt &
```

---

## Limitations & Shell Quirks

- **cmd.exe Environment Variable Expansion**: In Windows `cmd.exe`, percent signs (`%VAR%`) may trigger variable expansion if evaluated in a batch file or command prompt, as cmd parses environment variables before quote interpretation.
- **Control Characters**: Non-printable control characters or embedded raw null bytes (`\0`) cannot be reliably transported through standard shell command-line strings.
- **Pipeline Execution**: `CLIExpand` is strictly a transformer and serializer; it does not invoke or execute commands automatically.
