### 📝 Changelog 
## 📝 Changelog / Release Notes: TruthInTheFlip CLI Unification & Windowing Engine

**Refactor: Harmonize CLI handling and introduce dynamic Options Registry**

*   Replaced hardcoded command-line arguments in with a highly extensible `Option` base class. `Program.cs`
*   Migrated the `RandomSource` selection into a robust `RSourceOption` plugin.
*   Implemented a unified `TryParse`, `Info`, and `GetHelp` pipeline across all tools, ensuring the main executable and downstream reporting utilities share the exact same command-line behavior.
*   Cleaned up I/O handling in with -based transient retry logic (`RetryIO`), making massive parallel reads completely safe against temporary file locks. `TrackerStore``Stream`

**Feature: The `TrackerWindow` Drift-Resistant Telemetry Engine**

*   Introduced the `TrackerWindow` module to combat macro-drift by evaluating statistical Z-Scores over isolated, sliding segments of the dataset (e.g., the last 100 billion flips, or the last 1 hour of wallclock time).
*   Built a highly optimized, memory-efficient linked-list () to seamlessly subtract the oldest state from the newest, generating perfect localized metrics without bogging down the CPU. `UtilT.LinkNode<Tracker>`
*   Leveraged the new `Option` registry pattern to dynamically load `WindowByX` strategies (e.g., `WindowByWallclockTime`, `WindowByTotal`) via a single extensible `LoadDefaults()` pipeline. Third-party developers can now inject entirely new Window boundaries with zero reflection hassle via `windowOption.AddSource()`.

**Data Science: Enhanced Localized Reporting**

*   Integrated `TrackerWindow` into the new reporting utility, allowing instant deep-dive analytics.
*   New terminal reports now explicitly track the highest observed Z-score within a window (`maxZ`), the isolated win-rate at that specific peak (`aAtMaxZ`), and the raw baseline randomness (`ZHeadsAtMaxZ`), mathematically proving whether a temporary peak was due to an anticipation edge or just a momentary skew in the underlying RNG.
