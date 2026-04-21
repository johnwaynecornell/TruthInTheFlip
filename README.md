# TruthInTheFlip

A high-performance, multithreaded C# simulation harness for testing anticipation strategies against massive random bit streams.

Project site: https://johncornell.net

---

## Overview

**TruthInTheFlip** explores a simple but persistent question:

Can a meta-guessing strategy perform better than chance over a very large sequence of random bits?

The project is built as a modular testing harness rather than a single fixed experiment. Random sources, anticipation strategies, and telemetry windows are all configurable through a composable command-line system.

The current architecture supports:

- pluggable random sources
- pluggable anticipation strategies
- sliding telemetry windows for local statistical inspection
- structured, version-aware strategy registration
- high-volume multithreaded execution over massive datasets

---

## The Hypothesis

The baseline hypothesis explores the relationship between consecutive flips rather than guessing absolute values directly.

A simple example:

- if the last two flips were the same, anticipate the next will be different
- if the last two flips were different, anticipate the next will be the same

The goal is to measure whether an anticipation strategy yields a success rate meaningfully above 50% over a sufficiently large run, evaluated through ongoing statistical telemetry such as Z-score tracking.

---
## Reading the Results

As the project has evolved, one distinction has become especially important:

**local edge excursion is not the same thing as long-arc settlement.**

A run may produce strong local adjusted peaks while still settling weakly over time. For that reason, TruthInTheFlip now treats edge behavior in three related ways:

- **Excursion**: how strongly the edge flares locally
- **Settlement**: where the edge tends to finish
- **Persistence**: how often the edge remains at or above chance

This distinction matters because peak statistics alone can overstate the apparent strength of a run. A high local `TrueZ` may be real and still fail to represent durable behavior across the broader history.

To support this, the reporting utilities now serve different roles:

- **`TruthInTheFlip_sample_report2`** emphasizes named windows such as `last 1hr`, `last 1day`, and `lifetime`
- **`TruthInTheFlip_sample_report3`** emphasizes comparable segments and separates excursion from settlement

`sample_report3` introduces three project metrics for segment-based interpretation:

- **Edge Excursion Score** = median(best `TrueZ` per segment)
- **Edge Settlement Score** = mean(end `TrueZ` per segment)
- **Edge Persistence Index** = settlement × fraction of segment states at or above chance

These are project metrics intended to tell a truer story about edge behavior. They are not standard published statistics, but practical instruments for distinguishing what the edge can do, what it keeps, and how often it holds.
---
## Architecture

### Core Domain: `TruthInTheFlip.Format`

The core logic is organized so that execution, serialization, and strategy selection remain decoupled.

#### `TrackerStore`
Handles binary serialization, version checking, state persistence, and safe access to tracker history.

It supports forward and reverse streaming of large tracker files without requiring the full dataset in memory.

#### `TrackerRunner`
The parallel execution engine.

It coordinates the run and allows custom anticipation logic to be injected without changing the core threading model.

#### `TrackerWindow`
A drift-resistant telemetry layer for bounded statistical inspection.

Instead of relying only on lifetime aggregates, a tracker window allows local behavior to be measured over a selected segment such as:

- the last N flips
- the last N heads
- the last N tails
- the last N anticipated matches
- a bounded amount of wallclock time

This helps isolate local anomalies that may be smoothed out by global history.

#### `DelegateMethodRegistry`
A reusable strategy registry for command-line composition.

Strategies are described in a structured way through:

- name
- help text
- typed parameters
- default values
- version metadata

Parsing produces an explicit result object rather than relying on hidden registry state. Registries can also cooperate recursively, allowing one strategy to accept another structured strategy as a parameter.

This is the foundation now used by the upgraded option layout, including `-window`, `-rsource`, and `-anticipate`.

---

## Randomness Pipeline

### `BitFactory` and `Consumer`

To avoid lock contention at the bit level, worker threads do not compete for one bit at a time.

Instead:

- a `BitFactory` supplies large byte buffers
- each worker consumes bits locally
- synchronization only happens when a new buffer is needed

This keeps the runtime source pluggable while preserving throughput.

Random sources are injected through delegates, making it easy to swap between pseudo-random, cryptographic, or future hardware-backed sources.

---

## Getting Started

### Prerequisites

- .NET 8 SDK or newer

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run -- \[options\] <filepath>
```
By default, runs may continue indefinitely in order to build large datasets. Use runtime-limiting options such as `-iter` or `-stopwatch` to bound execution.

* * *

Command-Line Options
--------------------

### Core Options

*   `<filepath>` : path to the tracker state file
*   `-create` : create the state file if it does not exist
*   `-record` : append records to the state file
*   `-log` : enable detailed logging
*   `-show` : display stats or log tail and exit
*   `-info` : show current configuration state and exit
*   `-help` : show current command help

### Runtime Limits

*   `-iter <integer>` : run for a fixed number of iterations
*   `-stopwatch <time>` : run for a fixed amount of time (example: `1:0:0` for one hour)

### Strategy Options

*   `-rsource list` : list registered random sources
*   `-rsource <string>` : select a random source
*   `-window list` : list registered telemetry window strategies
*   `-window def` : use the default telemetry window
*   `-window <string> [params...]` : configure a specific telemetry window
*   `-anticipate list` : list registered anticipation strategies
*   `-anticipate <string> [params...]` : configure a specific anticipation strategy

Use `-help` for the current up-to-date command surface exposed by the registry system.

* * *

Example
-------

Start a new recorded run using cryptographic randomness and the default telemetry window:

./TruthInTheFlip MyRun.tkr -create -record -rsource NET2 -window def

Example with explicit anticipation strategy:

./TruthInTheFlip MyRun.tkr -create -record -rsource NET2 -anticipate def -window def -info

* * *

Sample Utilities
----------------

The repository includes supporting sample tools for working with `.tkr` datasets.

*   **TruthInTheFlip\_sample\_csv**  
    Streams tracker history and exports it to CSV for external analysis tools such as Python, Pandas, Excel, or notebooks.
*   **TruthInTheFlip\_sample\_report**  
    Lightweight reporting utility for inspecting stored tracker history.
*   **TruthInTheFlip.sample\_report2**  
    A more advanced reporting tool that demonstrates windowed analysis over historical tracker data.
*   **TruthInTheFlip_sample_report3**  
    Segment-oriented reporting utility for separating local edge excursion from long-arc settlement and persistence.
* * *

Notes
-----

TruthInTheFlip is designed as an experimentation harness.

It is useful for:

*   testing anticipation heuristics
*   comparing random sources
*   studying drift and local statistical behavior
*   building external reporting pipelines over very large histories

* * *

Links
-----

*   Project site: [https://johncornell.net](https://johncornell.net)
*   Repository: https://github.com/johnwaynecornell/TruthInTheFlip
