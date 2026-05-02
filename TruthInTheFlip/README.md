# Truth in the Flip: Meta-Guessing Simulation

## Overview
The core of meta-guessing relies entirely on anticipation. Fittingly, in anticipation of testing against a physical Quantum Random Number Generator (QRNG), I built this simulation harness to be modular from the ground up. By wrapping the random generation in a thread-safe, extensible delegate, the architecture is completely decoupled—ready to swap from standard C# pseudo-randomness to true quantum hardware with just a few lines of code.

This project is a high-performance, multithreaded C# testing harness designed to process and evaluate hundreds of billions of random bits to test a specific negentropy/meta-guessing hypothesis.

## The Hypothesis
The algorithm explores the natural properties of randomness—specifically, the behavior of standard deviation and runs. The core logic does not try to guess "Heads" or "Tails" (1 or 0). Instead, it evaluates the *relationship* between consecutive states:
* If the last two flips were the **same**, the algorithm anticipates the next flip will be **different**.
* If the last two flips were **different**, the algorithm anticipates the next will be the **same**.

The goal is to measure whether this meta-anticipation yields a success rate statistically greater than 50% over a massive dataset, evaluated via continuous Z-score calculation.

The goal is to measure whether this meta-anticipation yields a success rate statistically greater than 50% over a massive dataset, evaluated via continuous Z-score calculation.

## Architecture & Performance

To achieve the massive sample sizes required for statistical proof (100+ billion iterations), the harness is heavily optimized for CPU-bound parallel processing and decoupled for maximum extensibility.

### Core Domain: `TruthInTheFlip.Format`
The core logic has been isolated into its own namespace to allow for easy integration with external client utilities (like CSV exporters or Python data pipelines).

* **`TrackerStore`:** Decouples all binary serialization, file locks, and version checking from the domain model. It safely manages state accumulation across restarts and provides lazy file-reading capabilities (`Enumerate()`) so downstream clients can stream massive history files without memory bloat. Supports legacy v1.0 state migration.
* **`TrackerRunner`:** A pluggable, parallel execution engine. It allows you to inject custom anticipation logic via delegates, making it trivial to test entirely new guessing strategies across all CPU cores without modifying the underlying multithreading architecture.

### `BitFactory` & `Consumer`
Instead of multiple threads competing for individual random bits (which would cause severe lock contention), the system uses a `BitFactory` to serve massive, 1-Megabyte byte arrays to worker threads.
* **Source-Agnostic:** The random source is injected via an `Action<byte[]>` delegate, making it trivial to swap `System.Random` for a Quantis QRNG API or file stream.
* **Thread-Safe Pooling:** A `Consumer` class manages the bitwise extraction locally per thread, only returning to the `BitFactory` lock when its buffer is exhausted.

### `Tracker`
The `Tracker` handles the logic evaluation, mathematical logging, and high-precision telemetry.
* **External 2-Flip Prime:** To keep the hot loop free of branching, the `Tracker` is primed externally by the worker thread. Two initial flips establish the state, ensuring a mathematically pure guess rate.
* **Real-Time Z-Score & Telemetry:** Calculates standard error and Z-scores on the fly. It tracks highly specific metrics, including bet distributions (proving the 50/50 baseline), nanosecond-resolution batch durations, and Unix Epoch timestamps for exact real-world data anchoring.

## Getting Started

### Prerequisites
* .NET 8.0 SDK for the supported baseline build.
* .NET 10.0 SDK to build and run the newer high-performance target.

### Running the Simulation
1. Clone the repository.
2. Build the project: `dotnet build` for both or `dotnet buld -f net8.0` or `dotnet build -f net10.0` 
3. Run the simulation using the command-line interface:

```bash
dotnet run -- [file_path] [options]
```
### Swapping the Random Source
To test against a physical QRNG or a static file, simply update the `initRandom_Net()` delegate in `BitFactory.cs` to pipe your byte stream directly into the buffers.