### Q&A Information Document

**1\. What is the significance of the expression in ?`tracker.anticipated << 1 >= tracker.total``WatchVariables.cs`**

*   **Answer:** In binary arithmetic, shifting left by 1 (`<< 1`) is equivalent to multiplying by 2. So the expression is essentially `anticipated * 2 >= total`, which is an optimized way of checking `anticipated / total >= 0.5`. This determines whether the anticipated metric is at or above 50%, which tracks the "time above 50%" metric seen in the log output (`45.6853...% of the time above 50%`).

**2\. How are the time windows (1hr, 2hr, 4hr, 8hr) being tracked and evaluated?**

*   **Answer:** The program sets up multiple objects, each with a designated start time (e.g., total duration minus 1 hour). As the enumerates through historical data points (`t1`), it compares the record's timestamp against each watcher's trigger time (`watchTimes[w2]`). When the chronological replay passes that specific time threshold, the watcher starts accumulating stats (like min/max Z-score, a-values, etc.) via `Inspect()`. `WatchVariables``TrackerStore`

**3\. What does `TrackerStore.Enumerate()` yield, and how is it used?**

*   **Answer:** behaves like a time-series or event-sourcing database for the flip states. `Enumerate()` yields a chronological sequence of snapshots. This allows the utility to "replay" the entire lifetime of the process, aggregating statistics across different rolling windows instead of just reading a single final state. `TrackerStore``Tracker`

**4\. What are and tracking?`minZ` `maxZ`**

*   **Answer:** They track the highest and lowest Standard Z-Scores observed throughout the tracker's history (or within a specific time window). The Z-score is a statistical measurement of a score's relationship to the mean in a group of scores—in this case, measuring how far the `heads`/`tails` counts deviate from normal probability distributions. The log notes `Z(1.96)` and `Z(3.00)`, which relate to the 95% and 99.7% confidence intervals, respectively.

**5\. How does the program ensure backwards compatibility with older `TrackerRecord` files?**

*   **Answer:** It extracts the version from the file (`TrackerStore.ReadVersion`) and uses a custom `VersionCompare` method against a baseline of `1.1.0`. The script will halt if the version is newer than what it can handle, or if it's too old (lower than 1.1.0). Furthermore, it conditionally processes fields (like `batchTotal`, `wallclockTimeNs`, `betHeads`, etc.) only if the version allows for it, avoiding parse errors on older formats.