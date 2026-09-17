# TruthInTheFlip Farm

**TruthInTheFlip Farm** is a compositional command-line analysis tool for TruthInTheFlip tracker data. It turns tracker recordings into typed processing pipelines, metric projections, and curated reports.

The command language is intentionally compositional. A command determines:

1. where tracker state comes from;
2. the coordinate system and origin in which it is expressed;
3. the observational scale of each tracker state;
4. the population or grouping over which statistics are computed;
5. the output representation.

> **The order of the pipeline is part of the analysis.**

Source transformations are semantic operations, not merely syntactic wrappers. For example, segmenting accumulated lifetime states and segmenting mature rolling-window states produce different statistical populations even when the same metric names are projected.

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

To inspect the metric names available to metric projection:

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

### Pretty-print output (`pretty`)

The `pretty` command is the human-oriented rendering of the same metric projection mechanism used by `csv`. It preserves each requested metric/projection expression on the left and renders each process item as an indexed record block.

As a usable example...
```bash
TruthInTheFlip_Farm pretty segment window by_total 10B files "Quant.tkr" "Quant2.tkr" .END. by_total 100B MeanA MeanTrueZ PctAAtLeast50
```

And to display form...
```bash
TruthInTheFlip_Farm \
    pretty tracker \
    file "/path/to/crypto3.tkr" \
    Total ZScore AnticipatedPercentage
```

Example output:

```text
[1/179]
    Index = 0
    EndTotal = 100000000000
    MeanTrueZ = -0.3349180880974233
```

This is a Farm-native human inspection format. It is not JSON or a Python serialization.

### Export segment statistics

```bash
TruthInTheFlip_Farm \
    csv segment \
    file "/path/to/crypto3.tkr" \
    by_total 100B \
    Index EndTotal MeanTrueZ EndTrueZ BestTrueZ
```

`Count` arguments accept metric notation such as `K`, `M`, `B`, and `T`, so `100B` means 100 billion.

### Observation scale and coordinate space

Without a window:

```text
file "Quant.tkr"
```

the source exposes accumulated lifetime tracker states. With:

```text
window by_total 10B file "Quant.tkr"
```

each emitted tracker expresses the state of a rolling interval bounded by 10 billion source flips. The endpoint still has an absolute source coordinate, while ordinary tracker counters describe the interval-relative observation.

This changes the meaning of downstream path statistics. `MeanA`, `MeanTrueZ`, and `PctAAtLeast50` over lifetime states are not the same estimands as those metrics over rolling 10B states.

Rolling windows have a warm-up period. Before enough source history exists to fill the requested bound, the emitted observation is partial: it is valid, but has a smaller effective span. Prefixing the tracker selector with `full` keeps only mature observations:

```text
pretty segment \
    full window by_total 10B file "Quant.tkr" \
    whole \
    MeanA PctAAtLeast50
```

Use partial windows when startup behavior matters. Use `full window ...` when later statistics assume a consistent observational scale. Without `full`, early partial windows participate in the `whole` population above.

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

## Source algebra

The source surface is a compositional language over `TrackerSelector`: each form below produces a selector, and each transformation consumes one and returns another.

- `file` acquires one tracker recording.
- `files` acquires and sequentially joins raw compatible recordings.
- `concat` compositionally joins compatible tracker selectors.
- `from` / `to` restrict a source by absolute boundaries.
- `rebase` establishes a new accumulator origin from the first selected record.
- `window` converts accumulated state into interval-relative state.
- `full` restricts a tracker source to complete or mature observations.

Because the forms compose, their order carries meaning. `window ... from ... file ...`, for example, is not interchangeable with `from ... window ... file ...`.

### Join multiple tracker files (`files` and `concat`)

Multiple tracker files can be joined into a continuous accumulated tracker stream. Joining occurs at the accumulated-source level, rebasing each subsequent stream onto the cumulative state of the preceding one. Applying a window *after* joining yields continuous, relative rolling metrics across the combined horizon without boundary discontinuities.

Use `files ... .END.` to sequentially join a list of file paths:

```bash
TruthInTheFlip_Farm \
    csv segment full \
    window by_total 100B \
        files "Quant.tkr" "Quant2.tkr" .END. \
    whole \
    PctAAtLeast50 MeanA
```

Alternatively, `concat` provides the compositional selector form for combining two tracker selectors:

```bash
TruthInTheFlip_Farm \
    csv segment \
    concat file "Quant.tkr" file "Quant2.tkr" \
    by_total 100B \
    Index EndTotal MeanTrueZ
```

### Rebase tracker streams (`rebase`)

`rebase` changes the origin of an accumulated tracker stream by using the first selected record as the baseline and expressing subsequent records relative to it. Joining prepared sources is one application, but it is not the definition of the operation:

```bash
TruthInTheFlip_Farm \
    csv segment \
    window by_total 100B \
    concat \
        rebase from absTotal 100B file "Quant1.tkr" \
        rebase file "Quant2.tkr" \
    by_total 100B \
    Index EndTotal MeanTrueZ
```

In this example, rebasing removes the inherited accumulator origin from each selected stream before `concat` joins them.

### Form one population (`whole`)

`whole` creates one segment or aggregate over the complete population produced by the upstream source/process stage. The upstream expression defines what that population means:

```bash
TruthInTheFlip_Farm \
    csv segment full \
    file "crypto3.tkr" \
    whole \
    PctAAtLeast50 MeanA
```

A whole segment over raw accumulated tracker records is therefore semantically different from a whole segment over mature 10B rolling windows.

### Export segment aggregates

`segment_agg` groups segments into larger aggregates and produces a `SegmentAggregate` record for each group.

```bash
TruthInTheFlip_Farm \
    csv segment_agg \
    file "/path/to/crypto3.tkr" \
    by_total 100B \
    by_total 100B \
    Index AvgBestTrueZ AvgEndTrueZ MedianBestTrueZ
```

The first `by_total` is the `SegSelector` that divides tracker records into segments. The second `by_total` is the `AggSelector` that groups those segments into aggregates.

### Run a segment report

`segment_report` is a curated analytical report rather than a metric projection. It decides which statistics to present; its grade controls the level of detail:

```bash
TruthInTheFlip_Farm segment_report Med file "/path/to/crypto3.tkr" by_total 100B
```

Available grades in ascending detail: `None`, `Low`, `Med`, `High`, `All`. See the generated help for grade descriptions.

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

## Metric expressions

Metric projection field lists (`csv` and `pretty`) support a compact expression language with three operators:

```text
.   metric path traversal  (walk through a nested metric-bearing object)
#   metric function        (apply a named function to one or more arguments)
,   argument separator     (separate multiple arguments within a function call)
```

### Metric paths

A dot walks from one metric-bearing object into another. A segment exposes tracker records such as `Begin`, `End`, and `Z`:

```text
End.AnticipatedPercentage
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

### Metric functions

A `#` applies a named function to its arguments:

```text
abs#EndTrueZ
mean#anticipatedTails
pearson#ZScoreHeads,ZScoreTails
lerp#MinZHeads,MaxZHeads,.5
```

The number of arguments a function consumes is determined by its reflected CLR signature. The `,` separator divides arguments without parentheses.

Numeric literals are valid scalar arguments:

```text
clamp#EndTrueZ,-1,1
lerp#MinZHeads,MaxZHeads,.5
```

Functions fall into two categories based on their parameter types:

```text
Scalar parameters (double)
    evaluate a single expression in the current process item context

Aggregate parameters (List<double>)
    collect one value per item from the child process population
```

In `csv segment`, the child process is the stream of Tracker records within each segment. In `csv segment_agg`, the child process is the stream of `SegmentStats` items within each aggregate.

Nested function calls descend through process levels accordingly:

```text
mean#anticipatedTails
    → mean of anticipatedTails across all Tracker records in each segment

pearson#ZScoreHeads,ZScoreTails
    → Pearson correlation of ZScoreHeads and ZScoreTails across Tracker records

mean#abs#stddev_sample#AnticipatedPercentage
    → mean across segments of the abs of the sample stddev of
      AnticipatedPercentage within each segment (at segment_agg level)
```

Projection field names preserve the canonical expression text. In `csv`, names are properly quoted when an expression contains a comma. For example, a field list of `mean#anticipatedTails pearson#ZScoreHeads,ZScoreTails` produces a header where the Pearson column is quoted:

```text
mean#anticipatedTails,"pearson#ZScoreHeads,ZScoreTails"
```

Use `show metrics` to see available functions, their parameter names, and their return types. The parameter name convention in the output distinguishes aggregate parameters (plural, e.g. `values`, `x_values`, `y_values`) from scalar ones (singular, e.g. `value`, `min`, `max`).

Run:

```bash
TruthInTheFlip_Farm show metrics
```

to see the full metric catalogs, value types, and available functions in the current build.

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

A plotting guide with practical Pandas and Matplotlib examples is available in `FarmDocs/PlottingWithPython.md`.

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
