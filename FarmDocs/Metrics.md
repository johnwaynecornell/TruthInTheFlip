# TruthInTheFlip Farm Metric Guide

## 1. Purpose

TruthInTheFlip Farm metrics are the values that can be selected as columns when a process is formatted with `csv`.

Examples:

```text
csv tracker file "crypto3.tkr" absTotal ZScore AnticipatedPercentage
```

```text
csv segment file "crypto3.tkr" by_total 100B Index EndTotal MeanTrueZ EndTrueZ BestTrueZ
```

A metric may be a direct member of the process item, a calculated value, or a path through another metric-bearing object.

> **Runtime metric help is authoritative.** The Farm discovers metrics from the catalogs configured in the active application. If this guide differs from `show metrics`, use the output from the executable you are running. The generated metric list is the bottom line for names, types, and availability in that build.

The command:

```text
show metrics
```

prints the registered metric catalogs with each metric's name, CLR type, and description.

---

## 2. Metrics are selected by the process type

The process supplied to `csv` determines which metric catalog is used.

```text
csv tracker ...
```

produces `Tracker` records, so its first-level fields come from the Tracker catalog.

```text
csv segment ...
```

produces `SegmentStats` records, so its first-level fields come from the SegmentStats catalog.

The same `csv` command works with both because field binding is performed against the item type carried by the process.

Conceptually:

```text
csv
    tracker  -> Tracker metrics
    segment  -> SegmentStats metrics
```

This is also an extensibility point. A future Farm process can participate in CSV projection when its output type has a registered metric catalog.

---

## 3. Metric paths

Some metrics return another metric-bearing object. Those values can be traversed with dotted paths.

`SegmentStats` includes three Tracker-valued metrics:

```text
Begin   beginning Tracker record of the segment
End     ending Tracker record of the segment
Z       Tracker record where the segment reached its best True Z
```

That makes expressions such as these valid:

```text
Begin.absTotal
End.AnticipatedPercentage
End.ZScoreHeads
Z.ZScore
Z.AnticipatedPercentage
```

For example:

```text
csv segment file "crypto3.tkr" by_total 100B \
    Index Begin.absTotal End.absTotal End.AnticipatedPercentage Z.ZScore
```

The path is resolved one catalog at a time:

```text
SegmentStats.End
    -> Tracker
        -> AnticipatedPercentage
```

This is different from a hard-coded list of special segment fields. The path follows the type information carried by the metric catalog.

When an intermediate object is unavailable for a particular row, the CSV layer's null-value policy determines how that field is represented.

---

## 4. Tracker metric families

The Tracker catalog is broad because a Tracker record contains both persisted counters and calculated interpretations of those counters.

The following families are useful ways to think about the available fields. Use `show metrics` for the exact list in the current build.

### 4.1 Core counts

These are the underlying cumulative counters carried by a Tracker record.

```text
total
heads
tails
anticipated
baseAnticipated
anticipatedHeads
anticipatedTails
betHeads
betSame
anticipatedSame
```

Typical uses include low-level inspection, independent calculations, or validation of higher-level percentages.

Example:

```text
csv tracker file "crypto3.tkr" total heads tails anticipated
```

---

### 4.2 Absolute coordinates

Windowing changes the local counters visible through a windowed Tracker record. The Farm therefore exposes absolute coordinates that continue to identify the record's position in the underlying tracker.

Current absolute coordinates include:

```text
absTotal
absWallclockTimeNs
absWallclockTime
```

These are especially useful for reporting and for relating windowed output back to the original run.

Example:

```text
csv tracker window by_total 100B file "crypto3.tkr" \
    absTotal total absWallclockTime WallclockTime
```

The distinction is intentional:

```text
total / WallclockTime
    values interpreted in the current Tracker view

absTotal / absWallclockTime
    coordinates in the underlying run
```

The query grammar follows the same idea with boundaries such as:

```text
from absTotal 5T ...
from absWallclock 01:00:00 ...
```

---

### 4.3 Wall-clock and UTC timing

Tracker timing metrics include raw stored values and convenient typed projections.

Examples include:

```text
wallclockTimeNs
batchWallclockTimeNs
utcBeginTimeMs
utcEndTimeMs
UtcBeginTime
UtcEndTime
WallclockTime
BatchWallclockTime
```

The typed values are normally the easiest ones to work with in CSV and downstream tools.

For command-line UTC boundaries, the Farm accepts an explicit time value through the boundary constructors such as `utcBeginTime` and `utcEndTime`. Prefer an ISO-style value with `Z` or an explicit offset so the represented instant is unambiguous.

Example:

```text
from utcEndTime 2026-08-10T16:00:00Z file "run.tkr"
```

---

### 4.4 Source balance

The source itself can be inspected independently of the anticipation strategy.

Useful metrics include:

```text
HeadsPercentage
TailsPercentage
ZScoreHeads
ZScoreTails
```

Example:

```text
csv tracker file "crypto3.tkr" \
    absTotal HeadsPercentage TailsPercentage ZScoreHeads
```

This distinction is important when interpreting anticipation results. A strategy metric and a source-balance metric answer different questions.

---

### 4.5 Overall anticipation

These metrics describe the principal anticipation result.

```text
AnticipatedPercentage
ZScore
BaseAnticipatedPercentage
ZScoreBaseAnticipated
BiasDelta
InversionGain
```

A common compact set is:

```text
absTotal AnticipatedPercentage ZScore ZScoreHeads
```

For plotting around chance, `AnticipatedPercentage` is naturally interpreted relative to 50 percent. A downstream tool can plot the percentage directly with a 50% reference line or subtract 50 and plot the resulting edge around zero.

---

### 4.6 Heads and tails decomposition

The Tracker also exposes metrics that separate anticipation behavior according to the observed outcome.

```text
AnticipatedHeadsPercentage
AnticipatedTailsPercentage
ZScoreAnticipatedHeads
ZScoreAnticipatedTails
```

Related betting-distribution metrics include:

```text
BetHeadsPercentage
BetTailsPercentage
ZScoreBetHeads
ZScoreBetTails
BetHeadsWinRate
BetTailsWinRate
WinDistributionHeads
WinDistributionTails
```

These are useful when the aggregate anticipation result needs to be compared with source-side or choice-side asymmetry.

---

### 4.7 Same and Different decomposition

TruthInTheFlip can also be viewed through whether the next outcome is the same as or different from the prior outcome.

The established Tracker metrics include strategy-side values such as:

```text
BetSamePercentage
BetDiffPercentage
AnticipatedSamePercentage
AnticipatedDiffPercentage
ZScoreBetSame
ZScoreBetDiff
ZScoreAnticipatedSame
ZScoreAnticipatedDiff
BetSameWinRate
BetDiffWinRate
WinDistributionSame
WinDistributionDiff
```

The current Tracker catalog also exposes direct source-side Same/Diff metrics:

```text
same
diff
SamePercentage
DiffPercentage
ZScoreSame
ZScoreDiff
```

Those source-side values are useful as simple baselines:

```text
actual anticipation strategy
vs.
always Same
vs.
always Different
```

For example, when available:

```text
csv segment file "crypto3.tkr" by_total 100B \
    Index End.AnticipatedPercentage End.SamePercentage End.DiffPercentage
```

The source-side Same/Diff metrics should not be confused with `AnticipatedSamePercentage` and `AnticipatedDiffPercentage`. The latter describe how the strategy performed when making those choices; the former describe what the underlying Same/Diff stream itself did.

---

## 5. SegmentStats metric families

`segment` turns Tracker records into `SegmentStats`. These metrics summarize the records that fall inside each segment.

Example:

```text
csv segment file "crypto3.tkr" by_total 100B \
    Index BeginTotal EndTotal MeanTrueZ EndTrueZ BestTrueZ
```

### 5.1 Segment identity and extent

```text
Index
BeginTotal
EndTotal
BeginWallclock
EndWallclock
Count
```

These metrics answer where the segment is and how much tracker data it contains.

`Index` is a sequential segment number. `BeginTotal` and `EndTotal` describe the tracker totals at the edges of the segment, while `BeginWallclock` and `EndWallclock` provide the corresponding wall-clock positions.

---

### 5.2 Record anchors

```text
Begin
End
Z
```

These metrics are especially powerful because each is a Tracker object and therefore opens the Tracker catalog through a dotted path.

Examples:

```text
Begin.ZScore
End.ZScore
End.AnticipatedPercentage
Z.absTotal
Z.AnticipatedPercentage
```

`Z` is the Tracker record associated with the highest True Z reached inside the segment.

---

### 5.3 True-Z trajectory

The current SegmentStats implementation defines True Z from the Tracker record as:

```text
ZScore - abs(ZScoreHeads)
```

The principal True-Z segment metrics are:

```text
MeanTrueZ
EndTrueZ
BestTrueZ
```

They describe three different aspects of the segment:

```text
MeanTrueZ
    average True Z across the segment

EndTrueZ
    True Z at the segment's ending record

BestTrueZ
    highest True Z reached anywhere inside the segment
```

A useful plotting set is:

```text
Index MeanTrueZ EndTrueZ BestTrueZ
```

This was one of the first Farm visualizations because it makes excursion, average behavior, and settlement visible at the same time.

---

### 5.4 Anticipation summary

```text
MeanA
EndA
MinA
MaxA
Good
AAtLeast50
PctAbove50
PctAAtLeast50
```

These summarize anticipation percentage within the segment.

`MeanA`, `MinA`, and `MaxA` describe the level and range. `EndA` gives the ending value. The count and percentage fields describe occupancy relative to the 50% chance boundary.

Example:

```text
csv segment file "crypto3.tkr" by_total 100B \
    Index MeanA EndA MinA MaxA PctAbove50
```

---

### 5.5 Heads Z summary

```text
MeanZHeads
EndZHeads
MinZHeads
MaxZHeads
ZHeadsAboveZero
PctZHeadsAbove0
```

These summarize the underlying heads Z-score across the segment and can be used beside the True-Z metrics to distinguish strategy behavior from source balance.

Example:

```text
csv segment file "crypto3.tkr" by_total 100B \
    Index EndTrueZ EndZHeads MeanZHeads PctZHeadsAbove0
```

---

### 5.6 Anticipation/Same sign agreement

SegmentStats can also summarize whether anticipation percentage and source-side Same percentage occupy the same side of the 50% boundary.

The current catalog includes:

```text
AnticipatedSameSignCount
PctAnticipatedSameSign
```

These are exploratory descriptive metrics. They should not be read as evidence of causation; they simply report sign agreement within the segment.

As with all metrics, `show metrics` remains authoritative for the build being run.

---

## 6. Windowed metrics

A window changes the Tracker view before the process consumes it.

For example:

```text
csv tracker window by_total 100B file "crypto3.tkr" \
    absTotal total AnticipatedPercentage ZScore
```

Here, metrics based on the ordinary cumulative counters describe the configured window, while the `abs*` coordinates still identify the record's position in the full tracker.

The same transformation can feed segmentation:

```text
csv segment window by_total 100B file "crypto3.tkr" \
    by_total 100B Index End.absTotal End.AnticipatedPercentage
```

This makes windowing useful for asking local questions without losing the original run coordinate.

When comparing windowed and non-windowed output, be explicit about which values are local measurements and which are absolute coordinates.

---

## 7. Useful metric sets

These are starting points rather than prescribed reports.

### Basic Tracker trajectory

```text
absTotal ZScore ZScoreHeads AnticipatedPercentage
```

```text
csv tracker file "crypto3.tkr" \
    absTotal ZScore ZScoreHeads AnticipatedPercentage
```

### Basic segment trajectory

```text
Index EndTotal MeanTrueZ EndTrueZ BestTrueZ
```

```text
csv segment file "crypto3.tkr" by_total 100B \
    Index EndTotal MeanTrueZ EndTrueZ BestTrueZ
```

### Segment anticipation

```text
Index MeanA EndA MinA MaxA PctAbove50
```

### Ending Tracker state by segment

```text
Index End.absTotal End.AnticipatedPercentage End.ZScore End.ZScoreHeads
```

### Best True-Z record by segment

```text
Index BestTrueZ Z.absTotal Z.AnticipatedPercentage Z.ZScoreHeads
```

### Source balance

```text
absTotal HeadsPercentage TailsPercentage ZScoreHeads
```

### Same/Diff exploration

When direct Same/Diff metrics are present in the current Tracker catalog:

```text
Index End.AnticipatedPercentage End.SamePercentage End.DiffPercentage
```

### Timing

```text
absTotal absWallclockTime UtcEndTime
```

For segments:

```text
Index BeginWallclock EndWallclock Begin.absTotal End.absTotal
```

---

## 8. Choosing direct SegmentStats metrics versus paths

Sometimes the same conceptual value can be reached through a direct segment summary or through a Tracker anchor.

For example:

```text
EndA
```

is the segment's direct ending anticipation metric, while:

```text
End.AnticipatedPercentage
```

reaches the value through the ending Tracker record.

Likewise:

```text
EndZHeads
```

and:

```text
End.ZScoreHeads
```

represent closely related access paths.

Use the direct SegmentStats field when the segment summary itself is what matters. Use the dotted Tracker path when you want to stay in the Tracker vocabulary, combine several values from the same anchor, or reach a Tracker metric for which SegmentStats has no dedicated shortcut.

The path system is intentionally broader than the set of hand-written segment convenience metrics.

---

## 9. Types and downstream CSV consumers

`show metrics` displays the CLR type associated with each field. Common types include:

```text
Int64
Double
TimeSpan
DateTime
Tracker
```

A `Tracker` value is normally useful as an intermediate path node rather than as a final scalar CSV column.

For Python/Pandas workflows, numeric scalar fields normally arrive as numeric columns, while `TimeSpan` and `DateTime` values may require explicit conversion depending on the analysis being performed.

The separate plotting guide develops this workflow in more detail.

---

## 10. Metrics and extensibility

The metric system is intentionally not a single hard-coded switch inside the executable.

`JWCFarm` provides the catalog, descriptor, binder, path, and projection machinery. The application configures the catalogs appropriate to its domain.

Conceptually:

```text
MetricCatalogs
    Type -> MetricCatalog

MetricCatalog
    name -> MetricDescriptor

MetricBinder
    requested dotted paths
        -> MetricProjection
```

This allows specialization without requiring every Farm consumer to share one global metric universe.

A module can configure or extend the metric catalogs in its `FluentEnvironment` context and then expose commands whose field binding uses those catalogs.

For a user, the important consequence is simple:

> the metrics visible through `show metrics` are the metrics available to the current application environment.

---

## 11. Metric naming and stability

Metric names are part of the practical CSV interface. Scripts and plots may depend on them, so changing an established metric name should be treated as an interface change rather than a cosmetic edit.

At the same time, TruthInTheFlip Farm remains under active development. The generated catalog output therefore takes precedence over examples in static documents.

Before building a long-lived script, it is reasonable to inspect:

```text
show metrics
```

and confirm the names used by that build.

For scripts that depend on a known set of fields, validate the returned CSV header before plotting or analysis.

Example in Python:

```python
required = {
    "Index",
    "MeanTrueZ",
    "EndTrueZ",
    "BestTrueZ",
}

missing = required.difference(frame.columns)
if missing:
    raise ValueError(f"Missing expected metrics: {sorted(missing)}")
```

---

## 12. Where to go next

Use the Farm guide for command composition and source/process semantics:

```text
FarmDocs/FarmGuide.md
```

Use this guide to choose and understand fields.

The plotting guide continues from the CSV interface into Python, Pandas, and Matplotlib with examples such as:

- True-Z trajectory,
- anticipation around the 50% baseline,
- edge-from-chance plots,
- Same/Diff comparisons,
- scatter and correlation views,
- histograms and segment distributions,
- windowed and bounded analyses.

TruthInTheFlip Farm deliberately stops at a clean data boundary. CSV is the interchange layer; plotting and statistical exploration can then evolve independently downstream.
