# TruthInTheFlip: A High-Performance Meta-Guessing Simulation 🪙

Welcome to **TruthInTheFlip**, a high-performance, multithreaded C# testing harness designed to process and evaluate hundreds of billions of random bits. This project explores the natural properties of randomness—specifically, the behavior of standard deviation and runs—to test a specific negentropy/meta-guessing hypothesis.

Instead of trying to guess "Heads" or "Tails," the core algorithm evaluates the *relationship* between consecutive states:
* If the last two flips were the **same**, it anticipates the next flip will be **different**.
* If the last two flips were **different**, it anticipates the next will be the **same**.

Our goal? To measure whether this meta-anticipation yields a success rate statistically greater than 50% over massive datasets (100+ billion iterations), continuously evaluated via Z-score calculations.

---

## 🚀 What's New in v1.1.0: Unleashing Data Science

With the release of **TruthInTheFlip v1.1.0**, the simulation has been heavily upgraded to support rigorous data science, custom logic, and massive external datasets:

* **High-Precision Telemetry:** Nanosecond-resolution batch tracking (`Stopwatch`-backed) and Unix Epoch timestamps cleanly anchor simulations in real-world time.
* **Plug-and-Play Extensibility:** The new `TrackerRunner` architecture allows you to easily inject entirely custom anticipation strategies (via delegates and virtual methods) and test them across all CPU cores without touching the multithreading logic.
* **Safe Streaming for Massive Data:** The `TrackerStore` now supports lazy file-reading (`Enumerate()`). Downstream clients (like Python/Pandas) can stream massive historical `.tkr` records without memory bloat.
* **Advanced Betting Metrics:** Granular tracking of bet distributions explicitly proves the 50/50 baseline of the guessing mechanism itself.

---

## 📁 Repository Structure

This repository contains the core simulation engine and supporting utilities:

### 1. `TruthInTheFlip/` (Core Simulation Harness)
The main multithreaded simulation engine. It handles CPU-bound parallel processing, massive random byte-array pooling (`BitFactory`), and high-precision telemetry generation.
👉 **[Read the full documentation on architecture, performance, and CLI usage here.](TruthInTheFlip/README.md)**

### 2. `TruthInTheFlip.Format/` (Domain Logic & Serialization)
The completely decoupled domain library. This namespace cleanly isolates the `Tracker` logic, the `TrackerRunner` execution engine, and the `TrackerStore` (which handles backward-compatible binary serialization and state management).

### 3. `TruthInTheFlip_sample_csv/` (Data Export Utility)
A sample client project demonstrating how to use the new architecture to safely extract historical `.tkr` records into a backward-compatible CSV format, making it easy to analyze your billion-flip simulations in tools like Excel, Pandas, or Jupyter Notebooks.

### 4. 'TruthInTheFlip_sample_report/' ('.tkr' Report Program)
A sample client project demonstrating how to create a report using time windows on an enumeration to show the last 1,2,4,8 hours telemetry.

---

## 🛠️ Getting Started

To dive right into the main simulation harness:

```bash
# Clone the repository
git clone <your-repo-url>
cd TruthInTheFlip

# Build the project
dotnet build

# Run the simulation (requires a file path to store/load state)
dotnet run -- session_data.tkr -create -record
```
