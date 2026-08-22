# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test TruthInTheFlip.Farm.Tests/TruthInTheFlip.Farm.Tests.csproj

# Run a single test class
dotnet test TruthInTheFlip.Farm.Tests/TruthInTheFlip.Farm.Tests.csproj --filter "FullyQualifiedName~MetricBinderTests"

# Run a single test method
dotnet test TruthInTheFlip.Farm.Tests/TruthInTheFlip.Farm.Tests.csproj --filter "FullyQualifiedName~FarmProcessTests.Execute_RunsLifecycleInOrder"

# Run the Farm analysis tool
dotnet run --project TruthInTheFlip_Farm -- <args>

# Run the core simulation
dotnet run --project TruthInTheFlip -- <args>
```

**Farm self-documentation commands (authoritative for the running build):**
```
TruthInTheFlip_Farm -help
TruthInTheFlip_Farm list
TruthInTheFlip_Farm show metrics
```

## Architecture

The solution has two independent stacks that share only `TruthInTheFlip.Format`.

### Simulation stack (`TruthInTheFlip`)

**`TruthInTheFlip.Format`** — core domain

- `Tracker` / `ITracker` — the per-record state snapshot (total flips, heads, tails, anticipated matches, Z-scores, wall-clock time, UTC timestamps). Fields and properties marked `[IsMetric]` or `[IsRecord]` are automatically reflected into Farm metric catalogs by `TruthInTheFlip_Fluent`.
- `TrackerStore` — binary serialization, version checking, forward/reverse streaming of `.tkr` files without loading the full dataset into memory. Compatible version is `TruthInTheFlip.v1.1.0`.
- `TrackerRunner` — parallel execution engine. Coordinates workers and accepts injected anticipation logic without changing the threading model.
- `TrackerWindow` — sliding telemetry window for bounded local inspection (last N flips, last N heads/tails/anticipated, or a wall-clock duration).
- `BitFactory` + consumers — randomness pipeline. Workers pull byte-buffer chunks rather than competing bit-by-bit, keeping the source pluggable while preserving throughput.
- `DelegateMethodRegistry` / `AnticipationStrategies` — strategy registry powering `-rsource`, `-window`, and `-anticipate` CLI options in the simulation executable.

**`TruthInTheFlip`** — simulation entry point. Accepts CLI options (`-create`, `-record`, `-iter`, `-stopwatch`, `-rsource`, `-window`, `-anticipate`, etc.) and drives `TrackerRunner`.

### Farm analysis stack (`TruthInTheFlip_Farm`)

Four deliberately separated layers, from innermost to outermost:

#### 1. `FluentCommandLine` — typed command grammar

`FluentEnvironment` holds return-type registries. When you call `AddModule<T>()`, it:
1. Scans `T` for `public static` methods decorated with `[FluentMethodAttribute]` and registers them by their **return type**.
2. Calls `T.FluentModuleInitialize(FluentEnvironment env)` if it exists (the module self-configuration hook).

The grammar is **return-type-driven**. A method returning `TrackerSelector` automatically participates wherever a `TrackerSelector` is expected. The user never sees the CLR types — the parser assembles the typed tree from the command tokens. Custom CLR type parsers are registered via `env.TypeParseHandlers`. Parameter arrays end at `.END.` or end-of-input.

#### 2. `JWCFarm` — format-neutral processing infrastructure

- `FarmCommand` — abstract `Execute(FarmContext)`. The unit of top-level work.
- `FarmProcess : FarmCommand` — enumerates items through a `begin / process / end / abort` lifecycle. Holds a `MetricProjection` and a `ProcessActions` delegate bundle. `Execute` is sealed; subclasses implement `EnumerateItems`.
- Metrics subsystem:
  - `MetricDescriptor` — one metric: a name, CLR value type, `Getter` lambda, and descriptor kind (`Property`, `Scalar`, `Aggregate`).
  - `MetricCatalog` — dictionary of `MetricDescriptor` for one CLR type.
  - `MetricCatalogs` — type-indexed registry. Supports lazy population via the `Reflect` delegate (called once per type, result cached).
  - `MetricPath` — an ordered chain of `MetricDescriptor` instances representing a dotted navigation (e.g., `End.AnticipatedPercentage`).
  - `MetricProjection` — ordered list of `MetricPath` values; the bound field set for one CSV command.
  - `MetricBinder.Bind(...)` — resolves dotted string paths into a `MetricProjection`. Supports `#`-notation for aggregate metrics that require input-process context.

`JWCFarm` has no dependency on `FluentCommandLine`, so it can be used from non-command-line front ends.

#### 3. `TruthInTheFlip.Farm.Format` — TruthInTheFlip-specific adaptation

- `TrackerSelector` — a lazy `Func<TrackerStream>`. It is the central composition type: sources, filters, and range selectors all return and accept it, enabling nesting.
- `TrackerStream` — materializes a `TrackerStore` enumeration into a list; optional predicate filter. Materialization keeps downstream consumers independent of an active file handle.
- `TrackerWindows` — rolling window transforms (`by_total`, `by_heads`, `by_tails`, `by_anticipated`, `by_wallclock_ns`, `by_elapsed`). Each consumes and returns `TrackerSelector`.
- `TrackerBoundarys` — `from`/`to` boundary predicates (`absTotal`, `absWallclock`, `absWallclockNs`, `utcBeginTime`, `utcEndTime`).
- `SegSelector` / `AggSelector` — typed predicate closures that determine segment and aggregate boundaries.
- `SegmentProcess<TStats, TProduct>` — abstract base that drives the segment enumeration loop (begin/boundary/end/source-exhausted).
- `SegmentStats` — per-segment aggregate: `Begin`, `End`, `Z` (state at best TrueZ), `Index`, `IsComplete`, `CompletionReason`, etc.
- `SegmentAggregateProcess` — higher-order process that segments `SegmentStats` items (segments-of-segments).
- `TruthInTheFlip_Fluent` — the `FluentModuleInitialize` hub. Registers all TruthInTheFlip fluent methods (`file`, `tracker`, `segment`, `segment_agg`, `csv`, `window`, `from`, `to`, `by_total`, `by_elapsed`, `full`), installs `TimeSpan`/`DateTime`/`DateTimeOffset` type parsers, and wires the `MetricCatalogs` reflector that auto-discovers `[IsMetric]`/`[IsRecord]` members on `Tracker`.

#### 4. `TruthInTheFlip_Farm` — executable entry point

`Program.cs` creates a `FluentEnvironment`, registers `HelpCommand`, `InfoCommand`, `Commands`, and `TruthInTheFlip_Fluent`, then parses the command line and calls `Execute` on the resulting `FarmCommand`.

### Sample utilities

`TruthInTheFlip_sample_csv`, `TruthInTheFlip_sample_report`, `_report2`, `_report3`, `_report4` — standalone programs for CSV export and windowed/segment reporting, demonstrating direct `TrackerStore` consumption without the Farm layer.

### Test project

`TruthInTheFlip.Farm.Tests` uses xUnit. Tests cover `FarmProcess` lifecycle, `MetricBinder` path resolution, `FluentEnvironment` parsing, and `CsvFormatting`. Tracker file smoke tests (`TrackerFileSmokeTests`) require actual `.tkr` files and may be skipped in CI.

## Key design conventions

- **Adding a new fluent command:** create a `public static` method on any class, annotate with `[FluentMethodAttribute]`, and call `env.AddModule<T>()` (or add a `FluentModuleInitialize` hook that does it). The return type automatically joins the grammar.
- **Adding a new metric:** annotate a field, property, or method on `Tracker` (or another Farm item type) with `[IsMetric]` or `[IsRecord]`, plus `[StringHelp("...")]`. `TruthInTheFlip_Fluent.DefaultReflect` picks it up automatically. For aggregate metrics (accepting `List<double>`), `MetricDescriptor.EType.Aggregate` is used.
- **Adding a new source transformation:** write a method that accepts and returns `TrackerSelector`. It composes for free with `window`, `from`, `to`, and all processes.
- **The generated help is authoritative.** When in doubt about the current command surface or available metrics, run `TruthInTheFlip_Farm -help` or `show metrics` against the built executable.
