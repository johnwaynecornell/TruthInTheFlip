# Scale-Delta Null Population Runner (`scale_delta_null_runner.py`)

## Purpose

`scale_delta_null_runner.py` is an orchestration runner for generating empirical null populations for the window scale-delta experiment.

It generates deterministic synthetic tracker realizations under active SamePersistence strategy feedback matching historical experiment geometry, passes paired 10B/100B observational window streams to the frozen analyzer, and collects summary metric records into a persistent population NDJSON file.

## Architecture

The orchestration maintains strict separation of concerns across tools:

```text
scale_delta_null_runner.py (Runner)
    ├── Deterministically derives 64-bit trial seed
    ├── Invokes TruthInTheFlip_Farm per trial
    ├── Invokes window_scale_delta.py on trial NDJSON
    ├── Appends complete summary record to population NDJSON
    └── Maintains experiment metadata sidecar JSON

TruthInTheFlip_Farm (Synthetic Experiment Instrument)
    ├── Generates deterministic synthetic tracker realization from seed
    ├── Applies 10B and 100B observational rolling windows
    └── Emits paired-difference NDJSON stream

window_scale_delta.py (Frozen Authoritative Analyzer)
    ├── Computes frozen summary metrics (MeanDelta, NetArea, Runs, ACF, etc.)
    └── Emits summary JSON via `--summary-json`
```

**Key principle**: `TruthInTheFlip_Farm` generates the synthetic experiment; `window_scale_delta.py` measures it; `scale_delta_null_runner.py` only orchestrates execution, temporary files, resume safety, and record collection. The runner does not compute or modify scale-delta statistics.

## Required Tools

1. **Python 3.8+**
2. **TruthInTheFlip_Farm**: Compiled binary (e.g. `TruthInTheFlip_Farm/bin/Debug/net10.0/TruthInTheFlip_Farm`). Auto-detected if built in default location or on `PATH`.
3. **window_scale_delta.py**: Located in `Scripts/window_scale_delta.py`. Auto-detected when running from repository.
4. **Historical Tracker**: e.g., `Artifacts/Trackers/SamePersistence.NET1.rep2.tkr`.

## Example Invocation

```bash
# 5-trial development / validation run
python3 Scripts/scale_delta_null_runner.py \
  --trials 5 \
  --base-seed 12345 \
  --tracker Artifacts/Trackers/SamePersistence.NET1.rep2.tkr \
  --strategy-window 10B \
  --output /tmp/scale_delta_null.ndjson
```

### Full CLI Options

| Option | Default | Description |
|---|---|---|
| `--trials` | `5` | Number of trials in population |
| `--base-seed` | `12345` | Base 64-bit random seed |
| `--tracker` | `Artifacts/Trackers/SamePersistence.NET1.rep2.tkr` | Path to historical tracker file |
| `--strategy-window` | `10B` | Strategy decision window size |
| `--farm` | Auto-detected | Path to `TruthInTheFlip_Farm` binary |
| `--analyzer` | Auto-detected | Path to `window_scale_delta.py` |
| `--output` | Required | Path to output population NDJSON |
| `--observational-windows` | `10B 100B` | Observational rolling window sizes |
| `--keep-trials` | `False` | Retain raw per-trial Farm NDJSON files |
| `--trials-dir` | `<output_dir>/raw_trials` | Directory for retained raw trial NDJSON files |
| `--continue-on-error` | `False` | Continue remaining trials if a trial fails |

## Output Files

1. **Population NDJSON** (`<output>.ndjson`):
   One JSON line per completed trial, preserving the full analyzer schema with prepended `TrialIndex` and `Seed`:
   ```json
   {"TrialIndex":0,"Seed":12345,"Observations":44756,"MeanDelta":4.8877e-07,"NetArea":0.021876,...}
   {"TrialIndex":1,"Seed":6049483646029349647,"Observations":44756,"MeanDelta":-1.7662e-07,"NetArea":-0.007905,...}
   ```

2. **Metadata Sidecar JSON** (`<output>.meta.json`):
   Captures provenance and reproducibility parameters:
   - `baseSeed`
   - `trialCount`
   - `tracker`
   - `strategyWindow`
   - `observationalWindows`
   - `farm` executable path
   - `analyzer` script path
   - `nullSpec` (`same_persistence_algorithmic`)
   - `startTime` and `lastUpdated`
   - `completedTrials` count

## Seed Derivation

Trial seeds are derived using the exact algorithm from C#'s `NullTrialProcess.DeriveTrialSeed`:

- **Trial index 0**: returns `baseSeed` directly (preserving alignment with single-trial runs).
- **Trial index > 0**:
  ```python
  index_hash = splitmix64((trial_index * 0x9E3779B97F4A7C15) & 0xFFFFFFFFFFFFFFFF)
  seed = splitmix64((base_seed ^ index_hash) & 0xFFFFFFFFFFFFFFFF)
  ```

This equivalence has been verified against Farm's C# implementation across multiple base seeds and trial indices.

## Resume Safety

The runner natively supports safe resumption:
- If the output NDJSON already contains completed trials, rerun skips those trial indices without duplicating them.
- Before skipping, each existing record's `Seed` is validated against `derive_trial_seed(base_seed, trial_index)`. Any mismatch raises an error to prevent silently mixing populations.
- Duplicate `TrialIndex` values in existing output are detected and rejected.
- Existing sidecar metadata is validated against the active command arguments to ensure parameter compatibility.

## Temporary File Handling

- **Default mode**: Raw Farm NDJSON output for each trial is written to a temporary file, analyzed by `window_scale_delta.py`, and immediately deleted after the summary is recorded.
- **Debug mode (`--keep-trials`)**: Raw trial NDJSON files are preserved in `--trials-dir` with deterministic naming (`trial_0000_seed_12345.ndjson`).

## Development vs. Production Execution

- **Development Recommendation**: Limit execution to **5 trials** (`--trials 5`, indices 0..4). This exercises the full orchestration pipeline, seed derivation, resume logic, and invariant checks in ~2 minutes.
- **Production Execution (100+ trials)**:
  ```bash
  python3 Scripts/scale_delta_null_runner.py \
    --trials 100 \
    --base-seed 12345 \
    --tracker Artifacts/Trackers/SamePersistence.NET1.rep2.tkr \
    --output /path/to/scale_delta_null_100trials.ndjson
  ```
