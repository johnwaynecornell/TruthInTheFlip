# Plotting TruthInTheFlip Farm Output with Python

## 1. Purpose

TruthInTheFlip Farm deliberately stops at a clean data boundary: it produces CSV from a typed, compositional command language. Python, Pandas, and Matplotlib are one convenient way to consume that output for plotting and exploratory analysis.

The basic flow is:

```text
TruthInTheFlip_Farm
    -> CSV on stdout
        -> Python subprocess
            -> pandas.DataFrame
                -> Matplotlib
```

This separation is useful. The Farm remains responsible for selecting, transforming, segmenting, and projecting tracker data. Python remains free to evolve independently as a visualization and analysis layer.

> **Runtime help is authoritative.** If an example in this guide differs from the executable being run, use the generated `help`, `info`, and `show metrics` output from that build. In particular, `show metrics` is the bottom line for metric names, types, and availability.

---

## 2. Python dependencies

The examples in this guide use:

```text
pandas
matplotlib
```

Install them into the Python environment you intend to use, for example:

```bash
python -m pip install pandas matplotlib
```

The Farm itself does not depend on Python. Python is only one downstream CSV consumer.

---

## 3. Loading a Farm report into Pandas

A reusable helper can execute the Farm, capture stdout as CSV, keep diagnostics on stderr, and return a `DataFrame`.

```python
from __future__ import annotations

import io
import subprocess
import sys

import pandas as pd


def load_report(executable: str, arguments: list[str]) -> pd.DataFrame:
    result = subprocess.run(
        [executable, *arguments],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        check=False,
    )

    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        raise SystemExit(result.returncode)

    return pd.read_csv(io.StringIO(result.stdout))
```

Then point `executable` at the built Farm application:

```python
executable = (
    "/path/to/TruthInTheFlip_Farm/bin/Debug/net10.0/"
    "TruthInTheFlip_Farm"
)
```

The exact path depends on the checkout and build configuration.

### Why subprocess works well here

Farm reserves stdout for CSV and uses stderr for diagnostics. That makes the command-line interface naturally composable with tools such as Python.

Even a full `csv tracker` report can be consumed this way without requiring an intermediate CSV file.

---

## 4. Validate the columns you expect

Plots often outlive the command that created them. It is therefore useful to verify the returned header before plotting.

```python
required = {
    "Index",
    "MeanTrueZ",
    "EndTrueZ",
    "BestTrueZ",
}

missing = required.difference(frame.columns)

if missing:
    raise ValueError(
        f"Report is missing expected columns: {sorted(missing)}"
    )
```

This turns a renamed or unavailable metric into a clear error instead of a confusing plotting failure.

The current metric surface can always be inspected with:

```text
show metrics
```

---

## 5. First plot: segment True-Z trajectory

One of the most useful introductory segment reports compares the average, ending, and best True Z within each segment.

```python
frame = load_report(
    executable,
    [
        "csv",
        "segment",
        "window",
        "by_total",
        "100B",
        "file",
        "/path/to/crypto3.tkr",
        "by_total",
        "100B",
        "Index",
        "EndTotal",
        "MeanTrueZ",
        "EndTrueZ",
        "BestTrueZ",
    ],
)
```

Plot it with:

```python
import matplotlib.pyplot as plt

frame.plot(
    x="Index",
    y=["MeanTrueZ", "EndTrueZ", "BestTrueZ"],
    kind="line",
)

plt.axhline(0.0, linewidth=1)
plt.xlabel("Segment index")
plt.ylabel("True Z")
plt.title("TruthInTheFlip segment report")
plt.tight_layout()
plt.show()
```

The three lines answer different questions:

```text
MeanTrueZ
    What was the average True Z inside the segment?

EndTrueZ
    Where did the segment settle?

BestTrueZ
    What was the strongest positive excursion reached inside the segment?
```

A zero reference line is appropriate because True Z is naturally interpreted around zero.

---

## 6. Plotting percentages: use the correct baseline

A common plotting mistake is to copy the zero reference line from a Z-score plot into a percentage plot.

For example:

```text
End.AnticipatedPercentage
End.AnticipatedSamePercentage
End.AnticipatedDiffPercentage
```

are percentages centered near 50, not zero.

A suitable report is:

```python
frame = load_report(
    executable,
    [
        "csv",
        "segment",
        "window",
        "by_total",
        "100B",
        "file",
        "/path/to/crypto3.tkr",
        "by_total",
        "100B",
        "Index",
        "End.AnticipatedPercentage",
        "End.AnticipatedSamePercentage",
        "End.AnticipatedDiffPercentage",
    ],
)
```

Plot with a 50% reference line:

```python
frame.plot(
    x="Index",
    y=[
        "End.AnticipatedPercentage",
        "End.AnticipatedSamePercentage",
        "End.AnticipatedDiffPercentage",
    ],
    kind="line",
)

plt.axhline(50.0, linewidth=1)
plt.xlabel("Segment index")
plt.ylabel("Anticipation percentage")
plt.title("TruthInTheFlip anticipation by segment")
plt.tight_layout()
plt.show()
```

If a zero line is used instead, Matplotlib must include zero in the vertical scale and the interesting variation around 50% can become visually compressed.

---

## 7. Matplotlib's `+5e1` axis offset

When percentage values differ from 50 by only very small amounts, Matplotlib may display an axis offset such as:

```text
+5e1
```

This means the tick labels are being shown as small offsets from 50. It does **not** mean the data is near zero.

For TruthInTheFlip this behavior can actually be helpful because it enlarges microscopic deviations from chance. If the presentation is confusing, another clear option is to plot the edge from 50 explicitly.

---

## 8. Plot edge from chance instead of raw percentage

For a percentage metric `P`, define:

```text
edge = P - 50
```

In Python:

```python
frame["AnticipatedEdge"] = (
    frame["End.AnticipatedPercentage"] - 50.0
)

frame["SameEdge"] = (
    frame["End.SamePercentage"] - 50.0
)

frame["DiffEdge"] = (
    frame["End.DiffPercentage"] - 50.0
)
```

Then:

```python
frame.plot(
    x="Index",
    y=["AnticipatedEdge", "SameEdge", "DiffEdge"],
    kind="line",
)

plt.axhline(0.0, linewidth=1)
plt.xlabel("Segment index")
plt.ylabel("Percentage points above/below 50%")
plt.title("TruthInTheFlip edge from chance")
plt.tight_layout()
plt.show()
```

Now zero has an immediate interpretation:

```text
0      chance
> 0    above 50%
< 0    below 50%
```

This is often the cleanest way to compare very small percentage effects.

---

## 9. Same/Diff as a source-side baseline

The Tracker catalog exposes direct Same/Diff metrics:

```text
same

diff

SamePercentage
DiffPercentage

ZScoreSame
ZScoreDiff
```

These describe the underlying Same/Diff stream itself. They are different from strategy-side metrics such as:

```text
AnticipatedSamePercentage
AnticipatedDiffPercentage
```

A useful segment query is:

```text
csv segment file "crypto3.tkr" by_total 100B \
    Index \
    End.AnticipatedPercentage \
    End.SamePercentage \
    End.DiffPercentage
```

Because Same and Diff are complementary under the same population, plotting both may be visually redundant. For some questions a cleaner comparison is simply:

```text
Anticipated edge
vs.
Same edge
```

This asks whether the anticipation trajectory resembles a simple always-Same baseline without assuming that the two are equivalent.

---

## 10. Measure a visual relationship instead of guessing from it

A plot can suggest a relationship. Python makes it inexpensive to check whether that relationship survives a simple numerical summary.

For example:

```python
anticipated = frame["AnticipatedEdge"]
same = frame["SameEdge"]

sign_agreement = ((anticipated >= 0) == (same >= 0)).mean()
correlation = anticipated.corr(same)

print(f"Records: {len(frame)}")
print(f"Sign agreement: {sign_agreement:.2%}")
print(f"Correlation: {correlation:.6f}")
```

The four sign quadrants can also be counted:

```python
print("A+ / Same+ :", ((anticipated >= 0) & (same >= 0)).sum())
print("A+ / Same- :", ((anticipated >= 0) & (same < 0)).sum())
print("A- / Same+ :", ((anticipated < 0) & (same >= 0)).sum())
print("A- / Same- :", ((anticipated < 0) & (same < 0)).sum())
```

These are descriptive tools. A weak correlation or modest sign agreement can still be useful because it rules out an overly simple interpretation of a visual pattern.

The purpose of visualization is not only to confirm expectations. It can expose missing baselines, suggest new metrics, and show when an initially interesting relationship is actually weak.

---

## 11. Nested metric paths are especially useful for plots

`SegmentStats` exposes Tracker-valued anchors:

```text
Begin
End
Z
```

That means a plot can combine segment-level summaries with arbitrary Tracker metrics from meaningful points inside the segment.

Examples:

```text
End.AnticipatedPercentage
End.SamePercentage
End.ZScoreHeads
Z.AnticipatedPercentage
Z.absTotal
```

This avoids adding a dedicated `SegmentStats` convenience property for every possible plot.

For example:

```text
csv segment file "crypto3.tkr" by_total 100B \
    Index BestTrueZ Z.absTotal Z.AnticipatedPercentage Z.ZScoreHeads
```

asks for the state of several Tracker metrics at the record where the segment reached its best True Z.

---

## 12. Full `csv tracker` output

Segment reports are compact and excellent for visualization, but the Farm can also emit the full Tracker stream.

Example:

```text
csv tracker file "crypto3.tkr" \
    absTotal \
    absWallclockTime \
    AnticipatedPercentage \
    SamePercentage \
    ZScore \
    ZScoreHeads
```

The same Python loader works unchanged:

```python
frame = load_report(executable, arguments)
```

For large trackers there may be a pause while the Farm produces the records, but no separate intermediate-file workflow is required. Pandas receives the completed CSV directly from stdout.

Use the full Tracker stream when individual tracker records matter. Use `segment` when the analysis benefits from summaries, extrema, occupancy metrics, or a coarser plotting grain.

---

## 13. Use Farm queries before plotting

It is usually better to ask Farm for the desired region than to load everything into Python and filter afterward.

Examples include:

```text
from absTotal 5T ...
```

```text
to absWallclock 02:00:00 ...
```

and composed ranges such as:

```text
from absWallclock 01:00:00 \
    to absWallclock 02:00:00 \
        file "run.tkr"
```

A bounded segment report might look like:

```text
csv segment \
    from absWallclock 01:00:00 \
        to absWallclock 02:00:00 \
            file "run.tkr" \
    by_total 100B \
    Index End.absTotal End.AnticipatedPercentage End.ZScore
```

This keeps source selection in the Farm grammar and plotting in Python.

---

## 14. Window before segmenting

A window transforms Tracker records before the selected process consumes them.

For example:

```text
csv segment \
    window by_total 100B file "crypto3.tkr" \
    by_total 100B \
    Index MeanTrueZ EndTrueZ BestTrueZ
```

The windowed Tracker values describe the local view, while absolute coordinates such as `absTotal` and `absWallclockTime` continue to locate the record in the underlying run.

This is useful when a plot should show local behavior without losing global position.

---

## 15. Scatter plots

Line plots are best for ordered trajectories. Scatter plots are useful when the question is about the relationship between two measurements rather than their ordering.

For example, compare segment settlement with excursion:

```python
frame.plot(
    x="EndTrueZ",
    y="BestTrueZ",
    kind="scatter",
)

plt.xlabel("Ending True Z")
plt.ylabel("Best True Z")
plt.title("Segment excursion versus settlement")
plt.tight_layout()
plt.show()
```

Or compare anticipation edge with Same edge:

```python
frame.plot(
    x="SameEdge",
    y="AnticipatedEdge",
    kind="scatter",
)

plt.axhline(0.0, linewidth=1)
plt.axvline(0.0, linewidth=1)
plt.xlabel("Same edge")
plt.ylabel("Anticipation edge")
plt.title("Anticipation edge versus Same edge")
plt.tight_layout()
plt.show()
```

The quadrants then correspond directly to the sign-agreement counts.

---

## 16. Histograms and distributions

A histogram ignores segment order and instead asks how values are distributed.

For example:

```python
frame["EndTrueZ"].plot(
    kind="hist",
    bins=30,
)

plt.xlabel("Ending True Z")
plt.title("Distribution of segment ending True Z")
plt.tight_layout()
plt.show()
```

Histograms can help reveal:

```text
centering
skew
spread
heavy tails
multiple clusters
outlying segments
```

They complement trajectory plots rather than replace them.

---

## 17. Plot against an absolute coordinate

`Index` is convenient when every segment has a comparable meaning, but an absolute tracker coordinate may be more informative when segment sizes differ or when comparing reports.

For example:

```python
frame.plot(
    x="End.absTotal",
    y=["EndTrueZ", "BestTrueZ"],
    kind="line",
)

plt.xlabel("Total flips")
plt.ylabel("True Z")
plt.tight_layout()
plt.show()
```

This places the visualization directly on the experiment's accumulated flip coordinate.

Similarly, `End.absWallclockTime` can be useful when elapsed runtime is the meaningful horizontal axis.

---

## 18. Time columns in Pandas

Farm metrics include typed values such as:

```text
TimeSpan
DateTime
```

Depending on the exact CSV representation and the operation being performed, Pandas may initially load these as strings.

Date/time columns can be converted explicitly, for example:

```python
frame["UtcEndTime"] = pd.to_datetime(
    frame["UtcEndTime"],
    utc=True,
)
```

`TimeSpan`-style duration strings can often be converted with:

```python
frame["absWallclockTime"] = pd.to_timedelta(
    frame["absWallclockTime"]
)
```

Inspect `frame.dtypes` before assuming how a column was inferred:

```python
print(frame.dtypes)
```

---

## 19. Saving a figure

For documentation, comparison, or publication, save the figure before or instead of displaying it interactively:

```python
plt.tight_layout()
plt.savefig("truthintheflip_truez.png", dpi=160)
```

Then optionally:

```python
plt.show()
```

The plotting script can therefore serve both interactive exploration and repeatable figure generation.

---

## 20. GUI backends and headless environments

Matplotlib normally chooses a suitable backend automatically. In an IDE or desktop environment, the default backend is usually the best starting point.

If the local graphics stack or IDE has problems initializing a GUI backend, a script can explicitly choose another installed backend before importing `pyplot`:

```python
import matplotlib
matplotlib.use("TkAgg")
import matplotlib.pyplot as plt
```

For noninteractive or headless figure generation, a non-GUI backend may be more appropriate.

Backend selection is a Python/Matplotlib concern; it does not change the Farm CSV interface.

---

## 21. A reusable plotting script

A compact starting point combines the loader, header validation, and one plot:

```python
from __future__ import annotations

import io
import subprocess
import sys

import matplotlib.pyplot as plt
import pandas as pd


def load_report(executable: str, arguments: list[str]) -> pd.DataFrame:
    result = subprocess.run(
        [executable, *arguments],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        check=False,
    )

    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        raise SystemExit(result.returncode)

    return pd.read_csv(io.StringIO(result.stdout))


def main() -> None:
    executable = "/path/to/TruthInTheFlip_Farm"

    frame = load_report(
        executable,
        [
            "csv",
            "segment",
            "file",
            "/path/to/crypto3.tkr",
            "by_total",
            "100B",
            "Index",
            "MeanTrueZ",
            "EndTrueZ",
            "BestTrueZ",
        ],
    )

    required = {
        "Index",
        "MeanTrueZ",
        "EndTrueZ",
        "BestTrueZ",
    }

    missing = required.difference(frame.columns)
    if missing:
        raise ValueError(
            f"Report is missing expected columns: {sorted(missing)}"
        )

    frame.plot(
        x="Index",
        y=["MeanTrueZ", "EndTrueZ", "BestTrueZ"],
        kind="line",
    )

    plt.axhline(0.0, linewidth=1)
    plt.xlabel("Segment index")
    plt.ylabel("True Z")
    plt.title("TruthInTheFlip segment report")
    plt.tight_layout()
    plt.show()


if __name__ == "__main__":
    main()
```

From there, most exploratory plots require changing only the Farm metric list and the plotting block.

---

## 22. A productive exploration loop

A useful working cycle is:

```text
1. Ask a question.
2. Choose a source and process in Farm.
3. Select the smallest useful metric set.
4. Load CSV into Pandas.
5. Plot the relationship.
6. Quantify anything visually interesting.
7. If the question recurs, consider whether a new derived metric belongs in Tracker or SegmentStats.
```

This last step has already proved useful in TruthInTheFlip development. Visualization can expose a logically missing baseline or summary metric that is useful beyond the specific plot that revealed it.

The goal is not to push plotting logic back into the Farm. It is to let downstream exploration inform the metric vocabulary when a derived quantity is broadly meaningful.

---

## 23. Suggested next plots

Once the basic workflow is working, useful experiments include:

```text
True-Z trajectory
    Index -> MeanTrueZ, EndTrueZ, BestTrueZ

Anticipation edge
    Index -> End.AnticipatedPercentage - 50

Same baseline
    Index -> Anticipated edge vs Same edge

Source balance
    Index -> End.ZScoreHeads

Excursion vs settlement
    EndTrueZ vs BestTrueZ scatter

Ending distribution
    histogram of EndTrueZ

Best-record state
    Index -> Z.AnticipatedPercentage, Z.ZScoreHeads

Wall-clock trajectory
    End.absWallclockTime -> selected metrics
```

These are starting points, not prescribed analyses.

---

## 24. Where to go next

For command composition, sources, processes, boundaries, and windows, see:

```text
FarmDocs/FarmGuide.md
```

For metric meanings, families, dotted paths, and suggested metric sets, see:

```text
FarmDocs/Metrics.md
```

For the executable's exact current interface, use its generated help:

```text
help
info
show metrics
```

TruthInTheFlip Farm provides the query and projection layer. Python provides one flexible visualization layer on top of it. Keeping those responsibilities separate makes both easier to extend.
