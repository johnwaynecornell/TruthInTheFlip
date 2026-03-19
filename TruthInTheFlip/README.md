# Truth in the Flip: Meta-Guessing Simulation

## Overview
The core of meta-guessing relies entirely on anticipation. Fittingly, in anticipation of testing against a physical Quantum Random Number Generator (QRNG), I built this simulation harness to be modular from the ground up. By wrapping the random generation in a thread-safe, extensible delegate, the architecture is completely decoupled—ready to swap from standard C# pseudo-randomness to true quantum hardware with just a few lines of code.

This project is a high-performance, multithreaded C# testing harness designed to process and evaluate hundreds of billions of random bits to test a specific negentropy/meta-guessing hypothesis.

## The Hypothesis
The algorithm explores the natural properties of randomness—specifically, the behavior of standard deviation and runs. The core logic does not try to guess "Heads" or "Tails" (1 or 0). Instead, it evaluates the *relationship* between consecutive states:
* If the last two flips were the **same**, the algorithm anticipates the next flip will be **different**.
* If the last two flips were **different**, the algorithm anticipates the next will be the **same**.

The goal is to measure whether this meta-anticipation yields a success rate statistically greater than 50% over a massive dataset, evaluated via continuous Z-score calculation.

## Architecture & Performance

To achieve the massive sample sizes required for statistical proof (100+ billion iterations), the harness is heavily optimized for CPU-bound parallel processing.

### `BitFactory` & `Consumer`
Instead of multiple threads competing for individual random bits (which would cause severe lock contention), the system uses a `BitFactory` to serve massive, 1-Megabyte byte arrays to worker threads.
* **Source-Agnostic:** The random source is injected via a `Action<byte[]>` delegate, making it trivial to swap `System.Random` for a Quantis QRNG API or file stream.
* **Thread-Safe Pooling:** A `Consumer` class manages the bitwise extraction locally per thread, only returning to the `BitFactory` lock when its 1MB buffer is exhausted.

### `Tracker`
The `Tracker` handles the logic evaluation and mathematical logging.
* **External 2-Flip Prime:** To keep the hot loop free of branching and purely focused on logic, the `Tracker` is primed externally by the worker thread. Two initial flips are processed to establish the state (`priorFlip` and `guessAnticipateChange`), and then the scores are immediately wiped via `Reset()`. This ensures a mathematically pure guess rate without complicating the tracker's internal logic.
* **Real-Time Z-Score:** Calculates standard error and Z-scores on the fly (`(actualWinRate - 0.5) / standardError`) to track statistical significance without bottlenecking the processing loop.
* **Binary Serialization:** Safely saves and loads states (`allTime.TrackerRecord`) across program restarts, allowing continuous accumulation of billions of flips.

### `Program` (The Multithreaded Engine)
Utilizes `Parallel.For` with thread-local state initialization. Each core receives its own `Consumer` and `Tracker`, processes 10,000,000 flips independently, and then safely aggregates the results into the master state using a lightweight `lock`.

## Getting Started

### Prerequisites
* .NET 8.0 SDK (or newer)

### Running the Simulation
1. Clone the repository.
2. Build the project: `dotnet build`
3. Run the simulation: `dotnet run`
   The program will immediately begin processing utilizing all available CPU cores, periodically printing the current aggregate Z-score and win percentages to the console.

### Swapping the Random Source
To test against a physical QRNG or a static file, simply update the `initRandom_Net()` delegate in `BitFactory.cs` to pipe your byte stream directly into the buffers.