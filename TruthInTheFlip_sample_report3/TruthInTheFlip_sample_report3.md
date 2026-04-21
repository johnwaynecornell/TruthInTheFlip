# TruthInTheFlip_sample_report3

`TruthInTheFlip_sample_report3` is a segment-oriented report for `TrackerRecord` files.

Where `TruthInTheFlip_sample_report2` emphasizes named windows such as `last 1hr`, `last 1day`, and `lifetime`, `sample_report3` emphasizes **comparable segments** and separates three different questions:

1. **What can the edge do locally?**
2. **What does the edge keep by the end of a segment?**
3. **How often does it hold above chance while it is doing it?**

This report exists because local excursions and long-arc settlement are not the same thing.

---

## Purpose

TruthInTheFlip is concerned with the **edge relation** between events. In practice, this means a run can show strong local adjusted peaks while still settling weakly over longer spans. A single number such as lifetime `Z` or even best `TrueZ` can hide that distinction.

`sample_report3` is designed to expose that shape.

It does this by dividing a tracker into segments and reporting, for each segment:

- its **best** adjusted local edge
- where it **ends**
- what it looks like **on average**
- how often it remains **at or above chance**

---

## Relationship to sample_report2

Use `TruthInTheFlip_sample_report2` when you want:

- named windows
- entry/final records
- max/min behavior inside window ranges
- detailed per-window `TrueZ` and `aAtTrueZ`

Use `TruthInTheFlip_sample_report3` when you want:

- comparable chunking across a run
- excursion vs settlement comparisons
- segment tables
- run-level summary scores based on segments

A good rule of thumb is:

- `sample_report2` answers **“what happened in these windows?”**
- `sample_report3` answers **“what kind of run was this when divided into comparable pieces?”**

---

## Core Concepts

### TrueZ

`TrueZ` is the adjusted edge measure used by the report.

It is defined as:

```text
TrueZ = ZScore - abs(ZScoreHeads)
```

This preserves the sign and magnitude of the main anticipation `ZScore` while penalizing heads/tails drift by the absolute size of `ZScoreHeads`.

The purpose is not to create a mathematically canonical statistic, but to create a project-relevant adjusted edge measure.

### Best TrueZ per segment

This is the strongest local adjusted excursion observed inside a segment.

It answers:

> How strong did the edge get at its best within this segment?

This is useful for measuring **excursion**, but it is still a peak statistic and therefore selection-biased upward.

### End TrueZ

This is the `TrueZ` at the end of the segment.

It answers:

> Where did the segment actually settle?

This is often a better measure of durable segment character than the peak.

### Mean TrueZ

This is the average `TrueZ` over the inspected states in the segment.

It answers:

> What was the segment like on average while it unfolded?

### % above 50

This is the fraction of inspected states in the segment where anticipation was at or above chance.

In code terms, this corresponds to states where anticipated results were `>= 50%`.

It answers:

> How often did the segment remain non-negative in performance terms?

* * *

Summary Scores
--------------

`sample_report3` prints three summary scores.

### Edge Excursion Score

Edge Excursion Score = median(best TrueZ per segment)

This is a robust measure of typical flare strength.

It is intentionally based on the **median** rather than the mean so that a single spectacular segment does not dominate the run narrative.

Interpretation:

*   higher = stronger typical local excursions
*   lower = weaker typical local excursions

### Edge Settlement Score

Edge Settlement Score = mean(end TrueZ per segment)

This measures where segments tend to **finish**, not where they peak.

Interpretation:

*   positive = segments tend to end well
*   negative = segments tend to fade, collapse, or settle poorly

This is often the truer measure when the question is durability rather than spectacle.

### Edge Persistence Index

Edge Persistence Index = Edge Settlement Score × (avgPctAbove50 / 100)

This is a project metric, not a standard statistical term.

It combines settlement with the fraction of time segments remain at or above chance.

Interpretation:

*   large positive = segments tend to settle well and remain positive often
*   near zero = weak or mixed persistence
*   negative = the run tends to lose settlement and/or spend substantial time below chance

* * *

Interpreting the Story
----------------------

The three summary scores are meant to be read together.

### High excursion, weak settlement

The run can produce strong local flares, but they do not hold.

This often means the edge can organize briefly without becoming durable.

### Modest excursion, strong settlement

The run does not flare dramatically, but it keeps what it gains.

This can be more interesting than a spectacular peak.

### High excursion, high settlement

Rare and especially interesting.

This means the run both reaches strong local states and retains them by the end of segments.

### Weak excursion, weak settlement

Little evidence of meaningful segment-level edge behavior.

* * *

Segmentation Modes
------------------

The report supports two segmentation styles.

### `-segtotal <n>`

Segments by total flips.

Example:

TruthInTheFlip\_sample\_report3 tracker.tkr -window def -segtotal 100000000000

This divides the run into equal-sized flip buckets.

Use this when you want comparisons based on **amount of event exposure**.

### `-segwall <timespan>`

Segments by wallclock time.

Example:

TruthInTheFlip\_sample\_report3 tracker.tkr -window def -segwall 04:00:00

This divides the run into equal wallclock buckets.

Use this when you want comparisons based on **elapsed runtime** rather than total flips.

* * *

Source progression vs measured state
------------------------------------

A subtle but important design choice in `sample_report3` is that segmentation may be based on the **source progression**, while the measurements may still be taken from the **windowed/measured state**.

This is especially useful when a window is applied and the windowed totals become uniform or less natural as segment anchors.

In practice, this means:

*   the segment boundary can reflect the underlying run progression
*   the reported values can still reflect the currently chosen measurement model

This keeps segmentation structurally meaningful while allowing windowed interpretation.

* * *

Output Grades
-------------

### `-grade none`

Print only the main run line.

### `-grade low`

Print the summary scores only.

### `-grade med`

Print summary scores plus aggregate rates and thresholds.

### `-grade high`

Print the per-segment table.

### `-grade all`

Print the per-segment table and the special segment summaries:

*   best excursion segment
*   best settlement segment
*   worst settlement segment

* * *

Example
-------

TruthInTheFlip\_sample\_report3 tracker.tkr -window def -grade all

Typical output includes:

*   run summary
*   number of segments
*   Edge Excursion Score
*   Edge Settlement Score
*   Edge Persistence Index
*   aggregate rates such as:
    *   `bestTrueZ >= 1.96`
    *   `bestTrueZ >= 3.00`
    *   `endTrueZ >= 0.00`
    *   `meanTrueZ >= 0.00`
*   per-segment rows
*   special segment summaries

* * *

Important Caution
-----------------

Best-per-segment statistics are useful for describing **excursions**, but they are not unbiased estimates of stable advantage.

A run can show strong local best `TrueZ` values while still settling weakly overall.

That is one of the main reasons `sample_report3` exists.

Peaks alone are not the story.

* * *

Practical Reading Strategy
--------------------------

A good reading order is:

1.  **Edge Excursion Score**  
    How strong are typical local flares?
2.  **Edge Settlement Score**  
    Do segments actually keep anything?
3.  **Edge Persistence Index**  
    Does the run remain positive often enough for that settlement to matter?
4.  **Per-segment table**  
    Where are the exceptions, clusters, collapses, and unusually strong holds?
5.  **Best/Worst segment summaries**  
    What do the standout segments actually look like?

* * *

Suggested Interpretation Language
---------------------------------

The following phrases are often appropriate when writing about report3 output:

*   “The run shows strong local excursion but weak settlement.”
*   “Settlement remains negative despite positive local flares.”
*   “The best excursion segment is not the best settlement segment.”
*   “Persistence remains weak even when local adjusted peaks are strong.”
*   “This tells a truer story than peak comparison alone.”

* * *

Closing
-------

`TruthInTheFlip_sample_report3` exists to give the project a truer vocabulary.

It separates what the edge can do,  
what the edge keeps,  
and how often the edge holds.

That distinction matters.
