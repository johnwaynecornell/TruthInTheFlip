# Truth in the Flip: Meta-Guessing Simulation

## Overview
The core of meta-guessing relies entirely on anticipation. Fittingly, in anticipation of testing against a physical Quantum Random Number Generator (QRNG), I built this simulation harness to be modular from the ground up. By wrapping the random generation in a thread-safe, extensible delegate, the architecture is completely decoupled—ready to swap from standard C# pseudo-randomness to true quantum hardware, or even cryptographically secure randomness, with just a command-line flag.

This project is a high-performance, multithreaded C# testing harness designed to process and evaluate hundreds of billions of random bits to test a specific negentropy/meta-guessing hypothesis.

## The Hypothesis
The algorithm explores the natural properties of randomness—specifically, the behavior of standard deviation and runs. The core logic does not try to guess "Heads" or "Tails" (1 or 0). Instead, it evaluates the *relationship* between consecutive states:
* If the last two flips were the **same**, the algorithm anticipates the next flip will be **different**.
* If the last two flips were **different**, the algorithm anticipates the next will be the **same**.

The goal is to measure whether this meta-anticipation yields a success rate statistically greater than 50% over a massive dataset, evaluated via continuous Z-score calculation.

## Architecture & Performance

To achieve the massive sample sizes required for statistical proof (100+ billion iterations), the harness is heavily optimized for CPU-bound parallel processing and decoupled for maximum extensibility.

### Core Domain: `TruthInTheFlip.Format`
The core logic has been isolated into its own namespace to allow for easy integration with external client utilities (like CSV exporters or Python data pipelines).

* **`TrackerStore`:** Decouples all binary serialization, file locks, and version checking from the domain model. It safely manages state accumulation across restarts and provides lazy, bidirectional file-reading capabilities (`Enumerate()`, `ReverseEnumerate()`) so downstream clients can stream massive history files without memory bloat. 
* **`TrackerRunner`:** A pluggable, parallel execution engine. It allows you to inject custom anticipation logic via delegates, making it trivial to test entirely new guessing strategies across all CPU cores without modifying the underlying multithreading architecture.
* **`TrackerWindow`:** A drift-resistant telemetry engine built on a highly optimized doubly-linked list. It allows for the calculation of Z-scores over isolated, sliding segments of the dataset (e.g., the last 100 billion flips, or the last 1 hour of compute time) to isolate local anomalies from global history.

### `BitFactory` & `Consumer`
Instead of multiple threads competing for individual random bits (which would cause severe lock contention), the system uses a `BitFactory` to serve massive, 1-Megabyte byte arrays to worker threads.
* **Source-Agnostic:** The random source is injected via an `Action<byte[]>` delegate.
* **Thread-Safe Pooling:** A `Consumer` class manages the bitwise extraction locally per thread, only returning to the `BitFactory` lock when its buffer is exhausted.

## Getting Started

### Prerequisites
* .NET 8.0 SDK (or newer)

### Running the Simulation
1. Clone the repository.
2. Build the project: `dotnet build`
3. Run the simulation using the command-line interface:

```bash
dotnet run -- [options] <filepath>
```

*(Note: The simulation runs perpetually by default to generate massive datasets. Use `-iter` or `-stopwatch` to run for a fixed duration).*

### Command-Line Options
The extensible CLI allows you to configure everything from the random source to the sliding window metrics. 

**Core Options:**
* `<filepath>` : Path to the tracker state file (required). Recommended extension is `.tkr`.
* `-create` : Create the state file if it doesn't exist.
* `-record` : Append records to the state file to build a history.
* `-log` : Enable detailed logging output.
* `-show` : Show stats or log tail and exit.
* `-info` : Show current configuration state and exit.
* `-help` : Show up to date command help.

**Runtime Limits:**
* `-iter <integer>` : Run for *x* iterations and exit.
* `-stopwacth <time>` : Run for *x* amount of time (e.g., `1:0:0` for 1hr) and exit.

**Plugins (Extensible Architecture):**
* `-rsource list` : List all registered random sources.
* `-rsource <string>` : Select a specific random source (e.g., `NET1` for `System.Random`, `NET2` for `System.Security.Cryptography`).
* `-window list` : List all available sliding window strategies.
* `-window def` : Use the default sliding window (last 100 Billion flips).
* `-window <string> [params...]` : Configure a specific sliding window strategy with custom parameters.

### Example Run
To start a brand new simulation using Cryptographically Secure randomness, saving the history to `MyRun.tkr`, and applying the default sliding window for local Z-score analysis:
```bash
./TruthInTheFlip MyRun.tkr -create -record -rsource NET2 -window def
```

### External Utilities &amp; Samples

 Because the core domain logic () is completely decoupled from the execution engine, it is incredibly easy to build custom analytics tools that interact with your massive .tkr datasets—even while the main simulation is actively writing to them! TruthInTheFlip.Format

 We have included several sample projects to demonstrate this capability:

 - **TruthInTheFlip_sample_csv**: Demonstrates how to use TrackerStore.Enumerate() to safely stream massive binary .tkr records and parse them into a backward-compatible, human-readable CSV format. Perfect for exporting billion-flip datasets into Pandas, Excel, or Jupyter Notebooks.
- **TruthInTheFlip_sample_report**: A lightweight reporting utility showing how to query the raw historical tracker state.
- **TruthInTheFlip.sample_report2**: A heavily advanced analytical tool showcasing the true power of the engine. It leverages custom Option plugins to apply sliding Z-score windows over historical data, allowing you to retrospectively isolate local statistical anomalies (like peak Z-scores within specific timeframes) from the global lifetime average. TrackerWindow

