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

## 6. SegmentAggregate metric families

`segment_agg` turns `SegmentStats` items into `SegmentAggregate` records. Its metric catalog is populated by reflecting the same `[IsMetric]` attributes used for Tracker and SegmentStats.

Example:

```text
csv segment_agg file "crypto3.tkr" by_total 100B by_total 100B \
    Index AvgBestTrueZ AvgEndTrueZ MedianBestTrueZ
```

### 6.1 Aggregate identity and extent

Inherited from `StatsBase<SegmentStats>`:

```text
Index          Sequential index of the aggregate.
BeginTotal     Total flips at the start of the aggregate.
EndTotal       Total flips at the end of the aggregate.
BeginWallclock Wallclock time at the start of the aggregate.
EndWallclock   Wallclock time at the end of the aggregate.
Begin          Beginning SegmentStats record of the aggregate.
End            Ending SegmentStats record of the aggregate.
```

Because `Begin` and `End` are `SegmentStats` objects, all SegmentStats metrics are reachable through dotted paths:

```text
Begin.EndTrueZ
End.MeanA
```

### 6.2 True-Z aggregates

```text
Z                  SegmentStats record with the highest BestTrueZ inside the aggregate.
AvgBestTrueZ       Average of BestTrueZ across segments.
MedianBestTrueZ    Median of BestTrueZ across segments.
AvgEndTrueZ        Average of EndTrueZ across segments.
MedianEndTrueZ     Median of EndTrueZ across segments.
AvgMeanTrueZ       Average of MeanTrueZ across segments.
```

### 6.3 Anticipation and heads aggregates

```text
AvgPctAbove50      Average of PctAbove50 across segments.
AvgMeanA           Average of MeanA across segments.
AvgEndA            Average of EndA across segments.
MedianEndA         Median of EndA across segments.
AvgPctAAtLeast50   Average of PctAAtLeast50 across segments.
AvgMeanZHeads      Average of MeanZHeads across segments.
AvgEndZHeads       Average of EndZHeads across segments.
MedianEndZHeads    Median of EndZHeads across segments.
AvgPctZHeadsAbove0 Average of PctZHeadsAbove0 across segments.
```

### 6.4 Threshold percentages

Precomputed threshold metrics answer questions such as "what fraction of segments reached a best True Z at or above 1.96?"

```text
PctBestAtLeast_1_96
PctEndAtLeast_1_96
PctMeanAtLeast_1_96
PctEndZHeadsAtLeast_1_96
PctAbsEndZHeadsAtLeast_1_96
PctBestAtLeast_3_00
PctEndAtLeast_3_00
PctMeanAtLeast_3_00
PctEndZHeadsAtLeast_3_00
PctAbsEndZHeadsAtLeast_3_00
PctEndAAtLeast_50
```

Run `show metrics` to confirm exact names and descriptions in the current build.

---

## 7. Windowed metrics

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

## 8. Useful metric sets

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

## 9. Choosing direct SegmentStats metrics versus paths

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

## 10. Types and downstream CSV consumers

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

## 11. Metrics and extensibility

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

## 11.1 Super-user metric extension

Advanced callers can register metrics as `MetricDescriptor` instances directly on a `MetricCatalog`. This is the external/super-user extension path. It is distinct from the reflection-based catalog that the executable builds automatically from `[IsMetric]`-annotated members.

### Delegate-based descriptors

A property metric with a delegate getter:

```csharp
catalog.Add(new MetricDescriptor(
    "TrueZ2",               // name
    typeof(double),          // return type
    "Example TrueZ replica", // help
    (ctx, tracker) =>        // Func<MetricEvaluationContext, object, object?>
    {
        double z  = ctx.Get<double>("ZScore");
        double zh = ctx.Get<double>("ZScoreHeads");
        return z - Math.Abs(zh);
    })
{
    SourceExpressions = ["ZScore", "ZScoreHeads"]
});
```

A method (function) metric with a delegate invoker:

```csharp
catalog.Add(new MetricDescriptor(
    "myFunc",
    typeof(double),
    new List<MetricParameterDescriptor>
    {
        new MetricParameterDescriptor("x", MetricParameterType.Scalar)
    },
    "Double the input.",
    (ctx, obj, args) => (double)args[0]! * 2.0));
```

### SourceExpressions

`MetricDescriptor.SourceExpressions` declares which metric expression strings the descriptor needs to be pre-bound before evaluation. The strings use the same expression grammar as user-facing CSV field names.

```csharp
SourceExpressions = ["ZScore", "ZScoreHeads"]         // flat property sources
SourceExpressions = ["mean#abs#ZScoreHeads"]           // nested function source
```

**Design rule:**

```text
declare dependencies during bind   (SourceExpressions)
consume dependencies during eval   (ctx.Get<T>("expr"))
```

Never call `MetricBinder.Bind` from inside a `Getter` or `Invoke` delegate.

### Bind-time preparation

When `MetricBinder.Bind` processes a field expression and encounters a descriptor that declares `SourceExpressions`, it:

1. Binds each source expression at the same type and process context as the descriptor occurrence.
2. Stores the resulting bound `MetricPath` as a **hidden dependency** in `MetricProjection.Dependencies`.
3. Applies the same scalar/aggregate descent rules as normal metric expressions.

Hidden dependencies participate in `MetricProjection.Inspect` (aggregate state accumulation) but do **not** appear as CSV output columns. Only expressions listed in the `csv` field list become output columns.

Cycle detection is applied: if a descriptor's `SourceExpressions` would transitively reference itself (e.g. `Foo → Bar → Foo`), binding fails with a structured `MetricBindError` describing the cycle chain.

### MetricEvaluationContext.Get<T>

Inside a `Getter` or `Invoke` delegate, bound source expressions are retrieved via:

```csharp
T value = ctx.Get<T>("expression");
```

Where `"expression"` is one of the strings declared in `SourceExpressions`. The expression is looked up by canonical string key in the projection's hidden dependencies and evaluated against the current stats object.

If the expression was not declared in `SourceExpressions`, `Get<T>` throws `KeyNotFoundException` immediately, making the missing declaration visible at development time rather than at deployment.

### Example: flat property dependencies

```csharp
// Registers TrueZ2 on the Tracker catalog.
catalog.Add(new MetricDescriptor(
    "TrueZ2", typeof(double),
    "TrueZ from declared sources",
    (ctx, _) =>
    {
        double z  = ctx.Get<double>("ZScore");
        double zh = ctx.Get<double>("ZScoreHeads");
        return z - Math.Abs(zh);
    })
{
    SourceExpressions = ["ZScore", "ZScoreHeads"]
});
```

When `TrueZ2` appears in a `csv tracker` field list, `ZScore` and `ZScoreHeads` are bound as hidden dependencies. The `Getter` retrieves them by canonical string at evaluation time.

### Example: nested function source dependency

```csharp
// Registers MeanAbsHeads on the SegmentStats catalog.
catalog.Add(new MetricDescriptor(
    "MeanAbsHeads", typeof(double),
    "Mean absolute ZScoreHeads across tracker records in the segment",
    (ctx, _) => ctx.Get<double>("mean#abs#ZScoreHeads"))
{
    SourceExpressions = ["mean#abs#ZScoreHeads"]
});
```

When `MeanAbsHeads` appears in a `csv segment` field list, the expression `mean#abs#ZScoreHeads` is fully bound during `MetricBinder.Bind`:

- `mean` → aggregate, descends to Tracker records
- `abs` → scalar at Tracker level
- `ZScoreHeads` → Tracker property

Aggregate state is accumulated during `MetricProjection.Inspect` (one value per Tracker record in the segment). The `Getter` retrieves the computed mean via `ctx.Get<double>("mean#abs#ZScoreHeads")` at the segment evaluation boundary.

Hidden dependencies do not appear in CSV output unless explicitly listed as a separate field.

---

## 12. Metric naming and stability

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

## 13. Metric expression grammar

Metric fields can do more than name a property. The expression language uses three operators.

```text
.   metric path traversal
#   metric function application
,   function argument separator
```

Descriptive grammar (for reference; the parser is cursor-based and driven by reflected arity):

```text
path        := identifier ("." identifier)*
literal     := double-precision numeric value
expression  := literal | path | identifier "#" arguments
arguments   := expression ("," expression)*
```

The number of arguments a function consumes is exactly the number of parameters in its reflected CLR signature. The parser advances a cursor through the expression string and stops consuming arguments for a given function when its parameter count is satisfied.

### Examples

```text
EndTrueZ                        (path, one step)
End.AnticipatedPercentage       (path, two steps through nested catalog)
abs#EndTrueZ                    (function, one argument)
clamp#EndTrueZ,-1,1             (function, three arguments)
pearson#ZScoreHeads,ZScoreTails (function, two arguments, both aggregate)
lerp#MinZHeads,MaxZHeads,.5     (function, three arguments, .5 is a literal)
```

### Numeric literals

A literal is recognized before metric name lookup. Any value that parses as `double` (invariant culture) is a literal:

```text
.5     →  0.5
-1     →  -1.0
0      →  0.0
49.9   →  49.9
```

Negative literals work because the minus sign is consumed as part of the literal, not as a separate token.

### CSV column names

The canonical expression string is preserved as the CSV column header, and headers pass through the same CSV quoting layer as data values. An expression such as `pearson#x_values,y_values` produces a quoted column name in the output:

```text
mean#anticipatedTails,"pearson#ZScoreHeads,ZScoreTails"
```

Downstream tools parse this as two correctly named columns and address them by their exact expression strings.

---

## 14. Evaluation semantics

### Scalar parameters

A `double` parameter evaluates a single sub-expression in the context of the current process item.

In `csv segment`, the current item is a `SegmentStats` record. A scalar expression such as `abs#EndTrueZ` evaluates `EndTrueZ` (a SegmentStats property) and applies `abs` to produce one double value per segment.

### Aggregate parameters

A `List<double>` parameter collects one value per item from the child process population.

In `csv segment`, the child process provides Tracker records. An aggregate expression such as `mean#anticipatedTails` evaluates `anticipatedTails` for each Tracker record inside the segment and passes the resulting list to `mean`.

In `csv segment_agg`, the child process provides SegmentStats records. An aggregate expression such as `mean#EndTrueZ` evaluates `EndTrueZ` for each SegmentStats inside the aggregate.

### Process hierarchy

```text
csv tracker
    current: Tracker  (no child)

csv segment
    current: SegmentStats
    child:   Tracker records within the segment

csv segment_agg
    current: SegmentAggregate
    child:   SegmentStats items within the aggregate
    child's child: Tracker records within each segment
```

### Nested aggregate descent

Each aggregate parameter descends one child level. Nested calls descend further:

```text
clamp#mean#abs#stddev_sample#AnticipatedPercentage,-1,0
```

At `segment_agg`:

```text
clamp(value, -1, 0)
    where value = mean over SegmentStats of:
        abs(stddev_sample over Tracker records of:
            AnticipatedPercentage)
```

- `clamp` — scalar, operates at SegmentAggregate level
- `mean` — aggregate, descends to SegmentStats (child)
- `abs` — scalar, operates at SegmentStats level
- `stddev_sample` — aggregate, descends to Tracker records (child of SegmentStats)
- `AnticipatedPercentage` — Tracker property

---

## 15. Scalar metric functions

Scalar functions take `double` parameters; all arguments evaluate at the current process level.

Run `show metrics` to see function signatures and descriptions. The reference below uses source-attributed names and descriptions.

| Function | Signature | Description |
|---|---|---|
| `abs` | `abs#value` | Absolute value |
| `negate` | `negate#value` | Negate an input value |
| `square` | `square#value` | Square an input value |
| `sqrt` | `sqrt#value` | Square root |
| `ln` | `ln#value` | Natural logarithm |
| `pow` | `pow#value,exponent` | Raise a value to a power |
| `offset` | `offset#value,amount` | Add an offset to an input value |
| `offset50` | `offset50#value` | Measure an input relative to the 50 percent baseline |
| `scale` | `scale#value,factor` | Multiply an input by a scale factor |
| `ratio` | `ratio#numerator,denominator` | Divide one input by another |
| `clamp` | `clamp#value,min,max` | Limit an input to the inclusive minimum and maximum |
| `lerp` | `lerp#a,b,amount` | Linearly interpolate between two values |

The `offset50` function is equivalent to `offset#value,-50` and is convenient when plotting percentages relative to the chance baseline.

---

## 16. Aggregate metric functions

Aggregate functions take `List<double>` parameters; those arguments collect one value per item from the child process population.

The `pearson` and `covariance_*` functions accept two separate populations. Each is collected independently from the same child process items, keeping them aligned by position.

| Function | Signature | Description |
|---|---|---|
| `count` | `count#values` | Number of values in the population |
| `sum` | `sum#values` | Sum of the population |
| `mean` | `mean#values` | Arithmetic mean |
| `min` | `min#values` | Minimum value |
| `max` | `max#values` | Maximum value |
| `median` | `median#values` | Median value |
| `variance_population` | `variance_population#values` | Population variance |
| `variance_sample` | `variance_sample#values` | Sample variance (n−1 denominator) |
| `stddev_population` | `stddev_population#values` | Population standard deviation |
| `stddev_sample` | `stddev_sample#values` | Sample standard deviation (n−1 denominator) |
| `rms` | `rms#values` | Root mean square |
| `mean_abs` | `mean_abs#values` | Mean absolute value |
| `covariance_population` | `covariance_population#x_values,y_values` | Population covariance between two populations |
| `covariance_sample` | `covariance_sample#x_values,y_values` | Sample covariance between two populations |
| `pearson` | `pearson#x_values,y_values` | Pearson correlation coefficient |

Notes on `pearson`:

- Both populations must be non-empty and equal in length; returns `NaN` otherwise.
- Result is clamped to `[-1, 1]` to correct for floating-point rounding at exact limits.
- `NaN` is returned when either population has zero variance.
- The function makes no claim about causation or statistical significance; it reports correlation coefficient mechanics only.

Notes on `variance_sample` and `stddev_sample`:

- Return `NaN` when the population has fewer than two values, since the n−1 denominator would produce a division by zero.

---

## 17. Worked expressions

The following expressions have been verified against the current build. Process context is noted where the expression requires a specific level.

### Simple path and property

```text
EndTrueZ
```

Direct `SegmentStats` field. Equivalent to `End.ZScore - abs(End.ZScoreHeads)` as computed by `SegmentStats.EndTrueZ`.

### Nested path through record anchor

```text
End.AnticipatedPercentage
```

Walks `SegmentStats.End` (a `Tracker`) and returns `Tracker.AnticipatedPercentage`.

### Scalar function on a direct field

```text
abs#EndTrueZ
```

At `csv segment`: evaluates `EndTrueZ` for the current segment and returns its absolute value. Result is a single double per segment.

### Lerp to find midpoint (segment level)

```text
lerp#MinZHeads,MaxZHeads,.5
```

At `csv segment`: all three arguments are scalar. `MinZHeads` and `MaxZHeads` are `SegmentStats` fields. `.5` is a literal. The expression computes the midpoint of the ZHeads range within the segment.

### Aggregate mean (segment level)

```text
mean#anticipatedTails
```

At `csv segment`: `mean` collects `anticipatedTails` from each Tracker record in the segment and returns the arithmetic mean.

### Pearson correlation of two Tracker fields (segment level)

```text
pearson#ZScoreHeads,ZScoreTails
```

At `csv segment`: for each Tracker record in the segment, `ZScoreHeads` enters the first population and `ZScoreTails` enters the second. The Pearson correlation coefficient is computed over the aligned lists.

### Pearson with transformed second population (segment level)

```text
pearson#ZScoreHeads,abs#ZScoreTails
```

At `csv segment`: the first population is `ZScoreHeads`; the second population is `abs(ZScoreTails)` evaluated per Tracker record.

### Nested aggregate descent (segment_agg level)

```text
clamp#mean#abs#stddev_sample#AnticipatedPercentage,-1,0
```

At `csv segment_agg`:

- `clamp(value, -1, 0)` — value is scalar at SegmentAggregate level
- `mean#(...)` — aggregate; collects from SegmentStats items
- `abs#(...)` — scalar; evaluated at SegmentStats level
- `stddev_sample#AnticipatedPercentage` — aggregate; collects `AnticipatedPercentage` from Tracker records within each segment

Result: for each aggregate, clamp(mean over segments of abs(sample stddev of AnticipatedPercentage within segment), -1, 0).

### Double-clamped version

```text
clamp#clamp#mean#abs#stddev_sample#AnticipatedPercentage,-1,0,-1,-.5
```

The outer `clamp` restricts the already-clamped result to `[-1, -0.5]`. All arguments at the outer clamp level are scalar.

---

## 18. Where to go next

Use `FarmDocs/FarmGuide.md` for command composition, source/process semantics, and the full expression grammar in the context of the language architecture.

Use this guide to choose and understand metric fields, functions, and expressions.

Use `FarmDocs/PlottingWithPython.md` for the CSV-to-Pandas workflow, axis choices, reference lines, edge-from-chance calculations, scatter and correlation views, and metric expression columns.

TruthInTheFlip Farm deliberately stops at a clean data boundary. CSV is the interchange layer; plotting and statistical exploration evolve independently downstream.

Run `show metrics` for the authoritative metric list in the installed build.
