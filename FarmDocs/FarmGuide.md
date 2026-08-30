# TruthInTheFlip Farm Guide

## 1. Purpose

TruthInTheFlip Farm is a typed, compositional command-line analysis layer for TruthInTheFlip tracker data.

Its job is not to prescribe one report. Instead, it separates source selection, processing, metric projection, and output so they can be composed into small command expressions.

A typical command is:

```text
csv segment window by_total 100B file "crypto3.tkr" by_total 100B Index EndTrueZ BestTrueZ
```

The command says:

1. open a tracker **file**,
2. apply a rolling **window**,
3. process the resulting records as **segments**,
4. project selected metrics as **csv**.

This composition is central to the design.

> **Runtime help is the source of truth.** The Farm's command surface is generated from the same metadata and registries used to parse commands. If this guide differs from `-help`, `list`, or `show metrics`, follow the generated output from the executable you are using.

---

## 2. The grammar in one picture

The public language can be understood as a small typed graph:

```text
output
    csv <FarmProcess> <metric fields...>

report (curated, human-readable)
    segment_report <GradeArgument> <TrackerSelector> <SegSelector>

process
    tracker <TrackerSelector>
    segment <TrackerSelector> <SegSelector>
    segment_agg <TrackerSelector> <SegSelector> <AggSelector>

tracker source / transformation
    file <path>
    window <TrackerWindow> <TrackerSelector>
    from <TrackerBoundary> <TrackerSelector>
    to <TrackerBoundary> <TrackerSelector>
    full <TrackerSelector>

segment selector (SegSelector)
    by_total <Count>
    by_elapsed <TimeSpan>
    full <SegSelector>

aggregate selector (AggSelector)
    by_total <Count>
    by_elapsed <TimeSpan>
    full <AggSelector>

report grade (GradeArgument)
    None | Low | Med | High | All

tracker window
    by_total <Count>
    by_heads <Count>
    by_tails <Count>
    by_anticipated <Count>
    by_wallclock_ns <Int64>
    by_elapsed <TimeSpan>

tracker boundary
    absTotal <Count>
    absWallclock <TimeSpan>
    absWallclockNs <Int64>
    utcBeginTime <DateTimeOffset>
    utcEndTime <DateTimeOffset>
```

Not every symbol above is a top-level command. The return type of one fluent method determines where that expression can be used next.

For example, `file` returns a `TrackerSelector`, so it can be supplied to `tracker`, `segment`, `window`, `from`, or `to` where a `TrackerSelector` is expected.

---

## 3. Reading commands as typed expressions

Consider:

```text
csv tracker file "crypto3.tkr" Total ZScore
```

Conceptually, the parser is assembling:

```text
csv(
    tracker(
        file("crypto3.tkr")),
    "Total",
    "ZScore")
```

Likewise:

```text
csv segment window by_total 100B file "crypto3.tkr" by_total 100B Index EndTrueZ
```

corresponds roughly to:

```text
csv(
    segment(
        window(
            by_total(100B),
            file("crypto3.tkr")),
        by_total(100B)),
    "Index",
    "EndTrueZ")
```

The text syntax is prefix-oriented because each method must know the type of its next argument while parsing.

This has an important benefit: specialization remains local. A new source, process, selector, or output can be introduced by registering a method that returns or consumes the appropriate type rather than by extending a giant command switch.

---

## 4. Top-level commands

### `csv`

```text
csv <process> <fields...>
```

Formats a Farm process as CSV using the selected metric paths.

Examples:

```text
csv tracker file "crypto3.tkr" Total ZScore AnticipatedPercentage
```

```text
csv segment file "crypto3.tkr" by_total 100B Index EndTotal MeanTrueZ EndTrueZ BestTrueZ
```

The process decides which items exist. `csv` decides how those items are projected and written.

This distinction is deliberate: the tracker and segment processes do not need to contain CSV-specific behavior.

### `show metrics`

```text
show metrics
```

Displays all registered metric catalogs. The output includes:

- metric name,
- CLR value type,
- metric help text.

Use this command when choosing fields for `csv`.

### `-help`

```text
-help
```

Prints usage, available top-level commands, and the typed argument constructors reachable through them.

### `list`

```text
list
```

Lists the currently registered command graph.

Because type references discovered while rendering help are followed recursively, the list acts as a compact description of the fluent language available in the current environment.

### `-info`

```text
-info <outputPath>
```

Writes configuration information for the parsed command line to a file.

Example pattern:

```text
csv tracker file "crypto3.tkr" Total ZScore .END. -info command.info.txt
```

`.END.` terminates the preceding `params` array so the parser can resume with another top-level item. When the metric list is already the end of the command line, `.END.` is not needed.

---

## 5. Tracker sources

### `file`

```text
file <trackerPath>
```

Creates a tracker selector backed by a TruthInTheFlip tracker file.

The file source validates that:

- the path exists,
- the file identifies itself as a tracker record file,
- its version is compatible with the tracker format expected by the Farm.

The current file source materializes the tracker enumeration into a list before returning the stream. That means downstream CSV processing does not lazily hold an active file enumeration for the duration of plotting or other consumer work.

Example:

```text
csv tracker file "/data/trackers/crypto3.tkr" Total ZScore
```

---

## 6. Tracker transformations

Tracker transformations consume a `TrackerSelector` and return another `TrackerSelector`. This makes them naturally nestable.

### `window`

```text
window <TrackerWindow> <TrackerSelector>
```

Applies a rolling tracker window.

Example:

```text
window by_total 100B file "crypto3.tkr"
```

The window returns tracker records representing the state of the selected rolling interval rather than only the full cumulative history.

Available window definitions currently include:

```text
by_total
by_heads
by_tails
by_anticipated
by_wallclock_ns
by_elapsed
```

Each window definition has its own typed length argument and default value. Use `-help` to see the exact current defaults.

### `from`

```text
from <TrackerBoundary> <TrackerSelector>
```

Keeps records at or after the selected boundary.

Example:

```text
from absTotal 5T file "crypto3.tkr"
```

### `to`

```text
to <TrackerBoundary> <TrackerSelector>
```

Keeps records through the selected boundary.

Example:

```text
to absWallclock 02:00:00 file "crypto3.tkr"
```

### Combining `from` and `to`

Because both are source transformations, a range is expressed through composition:

```text
from absWallclock 01:00:00
    to absWallclock 02:00:00
        file "crypto3.tkr"
```

On one line:

```text
from absWallclock 01:00:00 to absWallclock 02:00:00 file "crypto3.tkr"
```

The current boundary predicates are inclusive.

---

## 7. Tracker boundaries

A `TrackerBoundary` defines two predicates: one interpretation for `from` and one for `to`.

This keeps the generic selectors independent of the coordinate being used.

### Absolute total

```text
absTotal <Count>
```

Defines a boundary by absolute flip count.

Example:

```text
from absTotal 5T file "crypto3.tkr"
```

### Absolute wall-clock duration

```text
absWallclock <TimeSpan>
```

Defines a boundary by absolute wall-clock duration.

Example:

```text
to absWallclock 12:00:00 file "crypto3.tkr"
```

### Absolute wall-clock nanoseconds

```text
absWallclockNs <Int64>
```

Provides the corresponding precise nanosecond boundary.

### UTC begin and end time

```text
utcBeginTime <DateTimeOffset>
utcEndTime <DateTimeOffset>
```

The query value is parsed as `DateTimeOffset` and normalized to UTC before comparison with tracker UTC timestamps.

Prefer an explicit UTC marker or offset:

```text
utcEndTime 2026-08-10T16:00:00Z
```

or:

```text
utcEndTime 2026-08-10T12:00:00-04:00
```

These represent the same instant.

---

## 8. Processes

### `tracker`

```text
tracker <TrackerSelector>
```

Processes tracker records individually.

This is useful when the desired metrics already exist on each tracker record:

```text
csv tracker file "crypto3.tkr" Total ZScore SamePercentage
```

A full tracker export can be large, but it is intentionally streamable to standard output so tools such as Pandas can consume it directly.

### `segment`

```text
segment <TrackerSelector> <SegSelector>
```

Divides tracker records into segments and emits `SegmentStats` items.

Example:

```text
csv segment file "crypto3.tkr" by_total 100B Index EndTrueZ BestTrueZ
```

Current segment selectors include:

```text
by_total <Count>
by_elapsed <TimeSpan>
```

A segment exposes its own aggregate metrics as well as selected tracker records such as `Begin`, `End`, and the tracker associated with its best True Z excursion.

The process hierarchy used by metric expressions descends as follows:

```text
Tracker records
    → segment <SegSelector>
        → SegmentStats
            → segment_agg <AggSelector>
                → SegmentAggregate
```

Each level is the child process of the one above it. This hierarchy determines how aggregate metric parameters collect their populations (see section 10).

### `segment_agg`

```text
segment_agg <TrackerSelector> <SegSelector> <AggSelector>
```

Processes `SegmentStats` items as a further layer of segmentation, producing `SegmentAggregate` records. The first selector divides tracker records into segments; the second groups those segments into aggregates.

Example:

```text
csv segment_agg file "crypto3.tkr" by_total 10B by_total 100B \
    Index AvgBestTrueZ AvgEndTrueZ MedianBestTrueZ
```

Current aggregate selectors are the same `by_total` and `by_elapsed` forms available for `segment`, using the `AggSelector` type.

`SegmentAggregate` exposes its own metric catalog, including averages, medians, and threshold percentages computed across its contained segments.

---

## 9. `segment_report`

```text
segment_report <GradeArgument> <TrackerSelector> <SegSelector>
```

`segment_report` produces a curated human-readable report rather than CSV. It collects `SegmentStats` items and writes a formatted summary whose depth is controlled by the `GradeArgument`.

```text
segment_report Med file "crypto3.tkr" by_total 100B
```

### Report grades

Grades are progressive: each includes everything from the level below it.

```text
None    Command configuration only.

Low     Edge statistics:
            segment count
            Edge Excursion Score  = median(best TrueZ per segment)
            Edge Settlement Score = mean(end TrueZ per segment)
            Edge Persistence Index = settlement × fraction positive

Med     Low plus anticipation and heads geometry:
            avgBestTrueZ, medianBestTrueZ, avgEndTrueZ, medianEndTrueZ, avgMeanTrueZ
            threshold percentages for best, end, and mean True Z
            Anticipation Path summary (avgMeanA, avgEndA, medianEndA, pctAAtLeast50)
            Underlying Heads summary (avgMeanZHeads, avgEndZHeads, medianEndZHeads, ...)

High    Med plus a per-segment detail table with:
            span, bestTrueZ, aAtBestZ, endTrueZ, meanTrueZ,
            endA, meanA, endZH, meanZH, %a>=50, %ZH>=0

All     High plus:
            Pearson correlations (ZHeads vs TrueZ, A vs ZHeads, A vs TrueZ)
            Retained Anticipation
            Settlement Adjusted Anticipation
            Standout segments (best excursion, best/worst settlement)
```

The report always ends with file compute time.

### Empty segment behavior

If no segments match the configuration, `segment_report` writes an error to standard error:

```text
There are no segments matching this report configuration.
```

No statistics are attempted when the segment list is empty.

### When to use `segment_report` versus `csv segment`

`segment_report` is a curated summary intended for human reading. `csv segment` and `csv segment_agg` are projection interfaces intended for programmatic downstream analysis, scripting, and plotting.

---

## 10. Metric expressions

JWCFarm treats metrics as a type-indexed graph.

A `MetricCatalog` describes the metrics available on one CLR type. A `MetricCatalogs` collection resolves catalogs by type. `MetricBinder` walks a requested expression and constructs a reusable `MetricProjection`.

### Metric paths

The dot operator traverses from one metric-bearing object into another.

For a segment, a flat metric is simple:

```text
EndTrueZ
```

A nested metric walks through another metric whose value has its own catalog:

```text
End.AnticipatedPercentage
```

Conceptually:

```text
SegmentStats
    End -> Tracker
        AnticipatedPercentage -> Double
```

Other examples:

```text
Begin.Total
End.SamePercentage
Z.AnticipatedPercentage
```

### Metric functions

The `#` operator applies a named function. The function name appears before `#`; its arguments appear after:

```text
abs#EndTrueZ
mean#anticipatedTails
pearson#ZScoreHeads,ZScoreTails
```

The `,` operator separates multiple arguments within a single function call:

```text
clamp#EndTrueZ,-1,1
lerp#MinZHeads,MaxZHeads,.5
pearson#ZScoreHeads,ZScoreTails
```

The number of arguments each function consumes is determined by its reflected CLR method signature. This is what allows commas to serve as unambiguous separators without parentheses.

### Numeric literals

Numeric literals are valid scalar arguments:

```text
.5
-1
0
49.9
```

They are recognized by attempting `double.TryParse` before looking up a metric name.

### Scalar and aggregate parameters

Parameter type in the CLR signature determines evaluation semantics.

```text
double parameter  →  scalar
    Evaluates a single expression in the current process item context.

List<double> parameter  →  aggregate
    Collects one value per item from the child process population.
```

In practice, the parameter name convention in `show metrics` output makes this visible:

```text
abs#value           value is scalar (double)
mean#values         values is aggregate (List<double>)
pearson#x_values,y_values    both are aggregate (List<double>)
clamp#value,min,max all three are scalar (double)
lerp#a,b,amount     all three are scalar (double)
```

For `csv segment`:
- The child process is the stream of Tracker records within each segment.
- An aggregate parameter collects one value per Tracker record.

For `csv segment_agg`:
- The child process is the stream of `SegmentStats` items within each aggregate.
- An aggregate parameter collects one value per `SegmentStats`.

Nested aggregate calls descend one additional process level per aggregate parameter:

```text
clamp#mean#abs#stddev_sample#AnticipatedPercentage,-1,0
```

At `csv segment_agg`:
- `clamp` — scalar, evaluates at SegmentAggregate level
- `mean` — aggregate, descends to SegmentStats (the child)
- `abs` — scalar, evaluates at SegmentStats level
- `stddev_sample` — aggregate, descends to Tracker records (the child of SegmentStats)
- `AnticipatedPercentage` — Tracker property

Result: for each aggregate, clamp(mean across segments of abs(stddev of AnticipatedPercentage within each segment), -1, 0).

### Multi-parameter aggregate functions

When a function has multiple aggregate parameters, each collects its own population from the same child process. The populations are aligned by position across the child items.

```text
pearson#ZScoreHeads,ZScoreTails
```

Both `x_values` and `y_values` are `List<double>`. For each Tracker record in the segment, `ZScoreHeads` is added to the first list and `ZScoreTails` to the second. The Pearson function receives the two aligned lists.

An inner scalar function transforms values before collection:

```text
pearson#ZScoreHeads,abs#ZScoreTails
```

Here `abs#ZScoreTails` is evaluated for each Tracker record; the absolute value is what enters the `y_values` list.

### Canonical expression text

The metric expression string is preserved as the canonical form in the projection. `show metrics` shows the runtime function vocabulary. When an expression becomes a CSV column name, the expression text is passed through the CSV quoting layer along with the data values. Expressions containing commas (such as `pearson#x_values,y_values`) are therefore quoted in the header:

```text
mean#anticipatedTails,"pearson#ZScoreHeads,ZScoreTails"
```

Downstream tools such as Pandas parse this as two correctly named columns and can address them by their full expression string.

Use:

```text
show metrics
```

for the actual metric names, function signatures, and return types available in the running build.

---

## 11. Counts, times, and typed arguments

### `Count`

TruthInTheFlip Farm provides a typed `Count` argument for large quantities.

Examples:

```text
100K
25M
100B
5T
```

This keeps large tracker coordinates readable while ordinary `long` parameters remain ordinary numeric values.

### `TimeSpan`

Duration arguments use invariant `TimeSpan` parsing.

Typical form:

```text
01:00:00
```

for one hour.

### `DateTimeOffset`

UTC boundary commands accept `DateTimeOffset` values, allowing either `Z` timestamps or explicit offsets. Explicit offset information is preferred because it identifies an unambiguous instant.

---

## 12. CSV behavior

`csv` binds the requested field list against the process item type, then installs CSV-specific lifecycle actions on the process:

```text
begin   -> write header
item    -> write projected row
end     -> flush output
abort   -> report the failure
```

The process itself remains format-neutral.

This allows the architecture to grow other output adapters later without duplicating tracker or segment enumeration.

CSV is written to standard output so it can be redirected or captured by a subprocess.

For automated consumers, treat standard output as data and standard error as the diagnostic channel.

---

## 13. Python workflow

The Farm has been exercised successfully by launching it as a subprocess and parsing stdout directly with Pandas.

A minimal pattern is:

```python
from __future__ import annotations

import io
import subprocess
import pandas as pd

result = subprocess.run(
    [executable, *arguments],
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    text=True,
    check=False,
)

if result.returncode != 0:
    raise RuntimeError(result.stderr)

frame = pd.read_csv(io.StringIO(result.stdout))
```

This makes the Farm a convenient boundary between TruthInTheFlip's native tracker representation and the Python analysis ecosystem.

A separate plotting guide will cover practical visualizations, including segment True Z behavior, anticipation percentages, Same/Diff baselines, and exploratory comparisons.

---

## 14. Architecture

The Farm is intentionally split into small layers while remaining inside the TruthInTheFlip solution.

### FluentCommandLine

Owns the typed command language:

- return-type registries,
- fluent method discovery,
- typed parameter parsing,
- defaults,
- enum parsing,
- help/list/info generation,
- environment-local context,
- module initialization.

A fluent method is not merely a command callback. Its return type becomes part of the grammar available to enclosing methods.

### JWCFarm

Owns reusable processing infrastructure:

- `FarmCommand`,
- `FarmProcess`,
- process lifecycle actions,
- metric catalogs,
- metric descriptors,
- metric paths,
- metric binding,
- metric projections,
- metric evaluation sessions.

JWCFarm deliberately does not require FluentCommandLine. It can therefore be reused from other front ends.

### TruthInTheFlip.Farm.Format

Owns the TruthInTheFlip-specific adaptation:

- tracker file sources,
- tracker selectors,
- rolling windows,
- absolute and UTC boundaries,
- segment selectors,
- tracker and segment processes,
- TruthInTheFlip metric reflection and additions,
- TruthInTheFlip-specific type parsers and fluent modules.

### TruthInTheFlip_Farm

Owns the executable application experience:

- the top-level environment,
- application command registration,
- help and info commands,
- information displays such as `show metrics`,
- execution of the final Farm command.

The exact physical placement may continue to evolve, but the responsibility boundary is intentional: reusable processing should not be coupled to one command-line application, while TruthInTheFlip-specific behavior should not be pushed into the general Farm layer.

---

## 15. Fluent modules and contextual state

FluentCommandLine supports module initialization through a public static initializer convention:

```csharp
public static void FluentModuleInitialize(FluentEnvironment env)
```

A module can use that hook to install related modules, add parse handlers, and configure contextual services associated with that `FluentEnvironment`.

This is important for extensibility. Advanced users can contribute a module that participates in the same typed grammar without editing the executable's core implementation.

The environment's context is type-keyed, which allows cooperating modules to find shared application services without introducing global framework state.

Because the active environment is entered through a scoped current-environment mechanism during parsing/invocation, fluent methods can access the environment that is actually performing the parse.

---

## 16. Extending the Farm

There are several natural extension directions.

### Add a new tracker source

Create a method returning `TrackerSelector`:

```text
network ...
archive ...
stdin ...
```

Once registered, it can automatically participate anywhere a `TrackerSelector` is accepted.

### Add a new source transformation

Consume and return `TrackerSelector`:

```text
sample <...> <TrackerSelector>
filter <...> <TrackerSelector>
```

### Add a new process

Return a `FarmProcess` that emits another recognized item type.

Because `csv` consumes the common process base and binds metrics dynamically from the process item type, a new process can become CSV-capable without adding a new CSV command.

### Add metrics

Extend the appropriate `MetricCatalog` or provide domain-specific reflection/configuration for a recognized type.

### Add another output adapter

A future output method can consume `FarmProcess` just as `csv` does while installing a different set of lifecycle actions.

The goal is composition rather than a growing matrix of commands such as `csv_tracker_report`, `csv_segment_report`, `json_tracker_report`, and so on.

---

## 17. Design principles

Several principles explain the current shape of the Farm.

### Specialization is an asset

A FluentEnvironment should contain the language useful to that application. String-processing tools do not need tracker sources; tracker tools do not need unrelated domain commands.

### Type information can remain useful even when hidden from the user

A user types `file`, `window`, `segment`, and `csv`. The CLR types returned between those calls constrain the grammar and keep composition deterministic even though the type names usually never appear in the command itself.

### Output format should not define the process

`tracker` and `segment` produce processes. `csv` formats them. This keeps processing and presentation independent.

### Metadata should drive discoverability

Command help, parameter help, metric help, and type descriptions are not secondary comments. They form the self-describing interface of the command language.

### Generated help wins

The program's current registries and annotations are closer to executable truth than static documentation. When there is a discrepancy, use:

```text
TruthInTheFlip_Farm -help
TruthInTheFlip_Farm list
TruthInTheFlip_Farm show metrics
```

as the authoritative reference for the installed build.

---

## 18. Practical examples

### Whole tracker, selected metrics

```bash
TruthInTheFlip_Farm \
    csv tracker \
    file "/data/trackers/crypto3.tkr" \
    Total ZScore AnticipatedPercentage SamePercentage
```

### Windowed tracker records

```bash
TruthInTheFlip_Farm \
    csv tracker \
    window by_total 10B \
        file "/data/trackers/crypto3.tkr" \
    WallclockTime absTotal ZScore AnticipatedPercentage
```

### Segments over a rolling source window

```bash
TruthInTheFlip_Farm \
    csv segment \
    window by_total 100B \
        file "/data/trackers/crypto3.tkr" \
    by_total 100B \
    Index EndTotal MeanTrueZ EndTrueZ BestTrueZ
```

### Nested tracker metrics from segments

```bash
TruthInTheFlip_Farm \
    csv segment \
    file "/data/trackers/crypto3.tkr" \
    by_total 100B \
    Index End.AnticipatedPercentage End.SamePercentage
```

### Absolute wall-clock range

```bash
TruthInTheFlip_Farm \
    csv tracker \
    from absWallclock 01:00:00 \
        to absWallclock 02:00:00 \
            file "/data/trackers/crypto3.tkr" \
    absTotal WallclockTime ZScore UtcEndTime
```

### Inspect the metric vocabulary first

```bash
TruthInTheFlip_Farm show metrics
```

Then copy the desired metric names directly into the CSV field list.

---

## 19. Next documentation

This guide focuses on the language and architecture. Two companion documents develop related topics:

- **Metrics guide** (`FarmDocs/Metrics.md`) — a browsable reference to Tracker, SegmentStats, and SegmentAggregate metrics, nested metric paths, the expression grammar, and scalar/aggregate function references.
- **Plotting with Python** (`FarmDocs/PlottingWithPython.md`) — subprocess integration, Pandas loading, Matplotlib examples, axis choices, centering percentages around 50%, metric expressions as column names, and exploratory workflows.

Those guides complement rather than duplicate the generated help.
