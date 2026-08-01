# TruthInTheFlip_sample_report4

`TruthInTheFlip_sample_report4` is an enhanced segment-oriented report for `TrackerRecord` files.

It extends `sample_report3` by distinguishing **anticipation behavior** (`a`), **underlying heads drift** (`ZHeads`), and **adjusted performance** (`TrueZ`). This allows the report to identify whether anticipation geometry appears to move independently of raw heads drift.

The intended conceptual model is:
- **a**: what the strategy is doing (raw anticipation path)
- **ZHeads**: what the source is doing (underlying heads/tails drift)
- **TrueZ**: what remains after accounting for source drift (adjusted anticipation geometry)

---

## Purpose

TruthInTheFlip is concerned with the **edge relation** between events. In practice, this means a run can show strong local adjusted peaks while still settling weakly over longer spans.

`sample_report4` is designed to expose the shape of the run while also providing source-balance telemetry.

It divides a tracker into segments and reports, for each segment:

- its **best** adjusted local edge (`TrueZ`)
- where it **ends** and its **average** state
- how often it remains **at or above chance**
- detailed **underlying heads drift** statistics (`ZHeads`)

---

## Relationship to other reports

Use `TruthInTheFlip_sample_report2` when you want:
- named windows (last 1hr, last 1day, etc.)
- entry/final records

Use `TruthInTheFlip_sample_report3` when you want:
- comparable chunking across a run
- basic excursion vs settlement scores without additional source telemetry

Use `TruthInTheFlip_sample_report4` when you want:
- all the features of `sample_report3`
- **Underlying Heads drift** telemetry per segment
- **Correlations** between heads drift and anticipation performance
- A more detailed per-segment table

---

## Core Concepts

### TrueZ

`TrueZ` is the adjusted edge measure used by the report.

It is defined as:
```text
TrueZ = ZScore - abs(ZScoreHeads)
```

This penalizes heads/tails drift by the absolute size of `ZScoreHeads`, focusing on the edge strength relative to the underlying balance.

### Anticipation Path (a)

The raw anticipation percentage. It represents the behavior of the strategy without any source-balance adjustment. The report uses a **>= 50.00%** convention for occupancy.

### ZHeads (Underlying Heads)

`ZHeads` represents the raw heads/tails drift of the source, expressed as a Z-score. It reports the state of the balance itself, regardless of anticipation.

The report uses a **>= 0.00** convention for heads-heavy occupancy.

---

## Segment Metrics

### Anticipation Path Metrics
- **Mean a**: Average anticipation percentage in the segment.
- **End a**: Anticipation percentage at the end of the segment.
- **Min/Max a**: The range of anticipation encountered in the segment.
- **% a >= 50**: Fraction of time anticipation was at or above chance (>= 50%).

### TrueZ Metrics
- **Best TrueZ**: The strongest local adjusted excursion inside a segment.
- **End TrueZ**: The `TrueZ` at the end of the segment (settlement).
- **Mean TrueZ**: The average `TrueZ` while the segment unfolded.
- **% above 50**: Fraction of time anticipation was at or above chance (>= 50%).

### ZHeads Metrics
- **Mean ZHeads**: Average heads/tails drift in the segment.
- **End ZHeads**: Heads/tails drift at the end of the segment.
- **Min/Max ZHeads**: The range of drift encountered in the segment.
- **% ZH >= 0**: Fraction of time the drift was non-negative (heads-heavy).

---

## Summary Scores

### Edge Excursion Score
`median(best TrueZ per segment)`
A robust measure of typical flare strength.

### Edge Settlement Score
`mean(end TrueZ per segment)`
Measures where segments tend to finish.

### Edge Persistence Index
`Edge Settlement Score * (avgPctAbove50 / 100)`
Combines settlement with the fraction of time spent at or above chance.

---

## Summary Blocks

### Anticipation Path Summary
At `-grade med` and above, the report includes aggregate strategy telemetry:
- **avgMeanA**
- **avgEndA**
- **medianEndA**
- **avgPctAAtLeast50**
- **endA >= 50.00**

### Underlying Heads Summary
At `-grade med` and above, the report includes aggregate source telemetry:
- **avgMeanZHeads**
- **avgEndZHeads**
- **medianEndZHeads**
- **avgPctZHeadsAbove0**
- **endZHeads >= 0.00**
- **|endZHeads| >= 1.96**

---

## Output Grades

### `-grade none`
Print only the main run line.

### `-grade low`
Print summary scores only.

### `-grade med`
Includes the **Anticipation Path** and **Underlying Heads** blocks and aggregate rates.

### `-grade high`
Includes the extended per-segment table:
`idx | span | bestTrueZ | aBest | endTrueZ | meanTrueZ | endA | meanA | endZH | meanZH | %a>=50 | %ZH>=0`

### `-grade all`
Adds best/worst segment summaries and descriptive correlations:
- **corrMeanZHeadsMeanTrueZ**
- **corrEndZHeadsEndTrueZ**
- **corrMeanAMeanZHeads**
- **corrEndAEndZHeads**
- **corrMeanAMeanTrueZ**
- **corrEndAEndTrueZ**

---

## Example

```bash
TruthInTheFlip_sample_report4 tracker.tkr -window def -grade all
```

---

## Practical Reading Strategy

1. **Edge Scores**: Check Excursion, Settlement, and Persistence.
2. **Anticipation Path**: Check what the strategy was typically doing (avgMeanA, avgEndA).
3. **Underlying Heads**: Check if the source was significantly drifted (avgEndZHeads, avgPctZHeadsAbove0).
4. **Correlations**: Look at the correlations. A low correlation between `a` and `ZHeads` suggests the strategy behavior is independent of source drift.
5. **Per-segment table**: Identify specific segments where strategy flares or collapses relative to the heads drift.
