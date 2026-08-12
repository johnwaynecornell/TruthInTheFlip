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

process
    tracker <TrackerSelector>
    segment <TrackerSelector> <SegSelector>

tracker source / transformation
    file <path>
    window <TrackerWindow> <TrackerSelector>
    from <TrackerBoundary> <TrackerSelector>
    to <TrackerBoundary> <TrackerSelector>

segment selector
    by_total <Count>
    by_elapsed <TimeSpan>

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

---

## 9. Metric projection and paths

JWCFarm treats metrics as a type-indexed graph.

A `MetricCatalog` describes the metrics available on one CLR type. A `MetricCatalogs` collection resolves catalogs by type. `MetricBinder` walks a requested dotted path and constructs a reusable `MetricProjection`.

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

This path mechanism is what allows one CSV command to project both segment-level and tracker-level information without adding report-specific columns to the command implementation.

Use:

```text
show metrics
```

for the actual metric names available in the running build.

---

## 10. Counts, times, and typed arguments

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

## 11. CSV behavior

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

## 12. Python workflow

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

## 13. Architecture

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
- metric projections.

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

## 14. Fluent modules and contextual state

FluentCommandLine supports module initialization through a public static initializer convention:

```csharp
public static void FluentModuleInitialize(FluentEnvironment env)
```

A module can use that hook to install related modules, add parse handlers, and configure contextual services associated with that `FluentEnvironment`.

This is important for extensibility. Advanced users can contribute a module that participates in the same typed grammar without editing the executable's core implementation.

The environment's context is type-keyed, which allows cooperating modules to find shared application services without introducing global framework state.

Because the active environment is entered through a scoped current-environment mechanism during parsing/invocation, fluent methods can access the environment that is actually performing the parse.

---

## 15. Extending the Farm

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

## 16. Design principles

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

## 17. Practical examples

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

## 18. Next documentation

This guide focuses on the language and architecture. Two companion documents are natural next steps:

- **Metrics guide** — a browsable reference to Tracker and SegmentStats metrics, nested metric paths, types, and interpretation.
- **Plotting with Python** — subprocess integration, Pandas loading, Matplotlib examples, axis choices, centering percentages around 50%, and exploratory workflows used during development.

Those guides should complement rather than duplicate the generated help.
