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


## 📝 Changelog / Release Notes: TruthInTheFlip CLI Unification & AST Parsing

**Refactor: Recursive CLI Registry & AST Construction**
*   Upgraded `DelegateMethodRegistry`'s argument parsing to construct an Abstract Syntax Tree (AST) of `RegistryParseResult` objects rather than eagerly compiling values.
*   This breakthrough enables registries to elegantly delegate to other registries seamlessly (e.g., `-anticipation RandomHT NET2` directly invokes the `RSourceOption` registry for the `NET2` parameter).
*   Greatly enhanced the `-info` command to recursively traverse the new AST, generating a beautiful, indented, tree-like display of all configured options, nested parameters, and explicit `[TypeHandler]` delegations.
*   Added comprehensive default-parameter tracking to the `-info` display, explicitly labeling when a user-supplied `"def"` keyword is used versus an implicit `implicit_def` fallback.

**Feature: Dynamic Anticipation Strategies**
*   Added an extensible `AnticipationStrategies` module complete with `ClassicMetaGuess`, `AlternatingMetaGuess`, `RandomHT`, `RandomSD`, and many others to pit various guessing heuristics against massive datasets.
*   Leveraged the new `Option` registry pattern to dynamically load strategies via a single extensible pipeline.

**Performance & Thread Safety: Isolated Strategy State**
*   Resolved a major performance bottleneck and reentrancy vulnerability within `AnticipationStrategies`.
*   Utilized highly optimized, isolated `ThreadLocal<BitFactory.Consumer>` instances bound directly to the closure of the generated strategy delegates.
*   Anticipation strategies are now completely thread-safe, massively parallelizable, and generate zero lock-contention or lookup overhead during billions of coin flips.

**Architecture & API Update: RNG Source State Encapsulation**
*   **Breaking API Change:** `RSourceOption` now resolves and returns fully encapsulated `BitFactory` objects rather than raw `Func<Action<byte[]>>` delegates.
*   Introduced a thread-safe `FactoryStorage` caching layer. Randomness sources (like `NET1` and `NET2`) now act as true singletons. 
*   This vital change allows multiple systems to share the exact same `BitFactory` instance, paving the way for serializing RNG states and efficiently multiplexing limited hardware entropy sources (like QRNGs) across massive parallel workloads.

**Feature: CLI**
*   Add -print through PrintOptions class for command line selection of print_delegates
