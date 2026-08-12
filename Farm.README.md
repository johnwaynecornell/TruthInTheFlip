# TruthInTheFlip Farm

**TruthInTheFlip Farm** is a compositional command-line analysis tool for TruthInTheFlip tracker data. It turns tracker files into typed processing pipelines and can project selected metrics as CSV for shells, scripts, Python, spreadsheets, plotting tools, and other downstream consumers.

The command language is intentionally compositional. A command describes three separate concerns:

- **source** — where tracker records come from and how they are selected,
- **process** — how those records are presented to the Farm,
- **output** — how the process is rendered.

For example:

```text
csv tracker file "crypto3.tkr" Total ZScore AnticipatedPercentage
```

reads naturally from the outside in:

```text
csv
    tracker
        file "crypto3.tkr"
```

The inner `file` expression produces a tracker source, `tracker` turns that source into a Farm process, and `csv` projects selected metrics from the process.

> **The generated help is authoritative.** TruthInTheFlip Farm is deliberately self-describing. If this README or another document ever disagrees with the output of `-help`, `list`, or `show metrics`, use the program's generated help as the bottom line for the build you are running.

## Quick start

Build the solution with the .NET SDK used by the repository:

```bash
dotnet build
```

Then ask the executable for its current command surface:

```bash
TruthInTheFlip_Farm -help
```

or:

```bash
TruthInTheFlip_Farm list
```

To inspect the metric names available to CSV projection:

```bash
TruthInTheFlip_Farm show metrics
```

### Export tracker records as CSV

```bash
TruthInTheFlip_Farm \
    csv tracker \
    file "/path/to/crypto3.tkr" \
    Total ZScore AnticipatedPercentage
```

The CSV is written to standard output, making the command easy to redirect:

```bash
TruthInTheFlip_Farm \
    csv tracker \
    file "/path/to/crypto3.tkr" \
    Total ZScore AnticipatedPercentage \
    > tracker.csv
```

### Export segment statistics

```bash
TruthInTheFlip_Farm \
    csv segment \
    file "/path/to/crypto3.tkr" \
    by_total 100B \
    Index EndTotal MeanTrueZ EndTrueZ BestTrueZ
```

`Count` arguments accept metric notation such as `K`, `M`, `B`, and `T`, so `100B` means 100 billion.

### Apply a rolling window

```bash
TruthInTheFlip_Farm \
    csv segment \
    window by_total 100B \
        file "/path/to/crypto3.tkr" \
    by_total 100B \
    Index EndTotal MeanTrueZ EndTrueZ BestTrueZ
```

Here the source is windowed before it is segmented.

### Select an absolute range

```bash
TruthInTheFlip_Farm \
    csv tracker \
    from absWallclock 01:00:00 \
        to absWallclock 02:00:00 \
            file "/path/to/crypto3.tkr" \
    WallclockTime absTotal ZScore UtcEndTime
```

`from` and `to` are inclusive selectors built from typed tracker boundaries. Current boundary forms include absolute total, absolute wall-clock duration, absolute wall-clock nanoseconds, and UTC begin/end timestamps. Use `-help` for the exact forms supported by the current build.

## Metric paths

Farm metrics can be nested. A segment exposes tracker records such as `Begin`, `End`, and `Z`, so a CSV field can walk through the metric graph:

```text
End.AnticipatedPercentage
End.SamePercentage
Begin.Total
Z.AnticipatedPercentage
```

For example:

```bash
TruthInTheFlip_Farm \
    csv segment \
    file "/path/to/crypto3.tkr" \
    by_total 100B \
    Index End.AnticipatedPercentage End.SamePercentage
```

Run:

```bash
TruthInTheFlip_Farm show metrics
```

to see the metric catalogs, value types, and descriptions available in the current build.

## Command composition

The important idea is that FluentCommandLine does not merely split tokens. Each fluent method returns a typed object that can become an argument to another fluent method.

A simplified expression such as:

```text
csv segment window by_total 100B file "crypto3.tkr" by_total 100B Index EndTrueZ
```

can be read structurally as:

```text
csv(
    segment(
        window(
            by_total(100B),
            file("crypto3.tkr")),
        by_total(100B)),
    Index,
    EndTrueZ)
```

This typed composition is what allows the language to stay compact while remaining extensible.

## Output and automation

CSV data is written to standard output. Diagnostics and errors are intended for standard error, allowing the Farm to participate cleanly in pipelines and subprocess workflows.

Python can consume the output directly without creating an intermediate file:

```python
result = subprocess.run(
    [executable, *arguments],
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    text=True,
    check=False,
)

frame = pandas.read_csv(io.StringIO(result.stdout))
```

A separate plotting guide is planned for practical Pandas and Matplotlib examples.

## Inspection commands

TruthInTheFlip Farm includes several ways to inspect the live command grammar:

```text
-help
```

Prints top-level usage and recursively discovered argument types.

```text
list
```

Lists the available commands and the typed constructors reachable from them.

```text
show metrics
```

Displays registered metric catalogs in columns including metric name, CLR value type, and help text.

```text
-info <path>
```

Writes the parsed configuration for a command line to a file. This is useful for inspecting how a composed command was interpreted.

When a `params` field list is followed by another top-level command or switch, `.END.` can be used to terminate the array explicitly. It is unnecessary when the field list is the end of the command line.

## Project structure

The Farm is being kept in the TruthInTheFlip solution as a small set of deliberately separated components:

- **FluentCommandLine** — typed command grammar, registries, parsing, help, module initialization, and environment-local context.
- **JWCFarm** — general Farm processes, lifecycle actions, metric catalogs, paths, binders, and projections.
- **TruthInTheFlip.Farm.Format** — TruthInTheFlip-specific sources, windows, boundaries, segmentation, metric reflection, and Farm process adapters.
- **TruthInTheFlip_Farm** — the application entry point, top-level commands, help/info integration, and final composition.

The split is intentional: reusable machinery is separated from TruthInTheFlip-specific semantics while the repository remains available as one coherent solution.

## Extensibility

FluentCommandLine modules can contribute more than commands. A module initializer can configure the active `FluentEnvironment`, register supporting modules and type parsers, and install contextual services used by fluent methods.

This makes it possible for advanced users to add capabilities without rewriting the application entry point or modifying unrelated framework code.

JWCFarm is kept independent of FluentCommandLine so Farm processing can also be used from non-command-line front ends.

## Tracker compatibility

The Farm validates tracker files when they are opened. The current implementation expects a TruthInTheFlip tracker record format compatible with version `TruthInTheFlip.v1.1.0` and rejects incompatible versions with a Farm input error.

File sources currently materialize tracker records into a detached list before downstream processing. This keeps CSV processing and plotting independent of a long-lived tracker file enumeration.

## Documentation

- `Farm.README.md` — quick orientation and first commands.
- `FarmDocs/FarmGuide.md` — command language, composition, architecture, and extension concepts.
- `FarmDocs/Metrics.md` — planned metric reference.
- `FarmDocs/PlottingWithPython.md` — planned Python/Pandas/Matplotlib guide.

Again, when documentation and generated output differ, **the help produced by the executable is authoritative for that build**.
