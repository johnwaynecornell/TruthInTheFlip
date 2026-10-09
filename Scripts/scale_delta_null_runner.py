#!/usr/bin/env python3
"""
Scale-Delta Null Population Runner.

Orchestrates deterministic multi-trial populations for scale-delta analysis
by executing TruthInTheFlip_Farm and collecting summary metrics via
window_scale_delta.py without modifying or reimplementing the instruments.
"""

import argparse
import datetime
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

MASK64 = 0xFFFFFFFFFFFFFFFF
SPLITMIX_ADD = 0x9E3779B97F4A7C15
SPLITMIX_MUL1 = 0xBF58476D1CE4E5B9
SPLITMIX_MUL2 = 0x94D049BB133111EB


def splitmix64(x: int) -> int:
    """
    64-bit SplitMix64 mixing function matching C# NullTrialProcess.SplitMix64.
    """
    z = (x + SPLITMIX_ADD) & MASK64
    z = ((z ^ (z >> 30)) * SPLITMIX_MUL1) & MASK64
    z = ((z ^ (z >> 27)) * SPLITMIX_MUL2) & MASK64
    return (z ^ (z >> 31)) & MASK64


def derive_trial_seed(base_seed: int, trial_index: int) -> int:
    """
    Deterministically derives a 64-bit trial seed from a base seed and trial index.

    Matches C# NullTrialProcess.DeriveTrialSeed exactly:
      - Trial index 0 returns the base seed directly.
      - Trial index > 0 uses SplitMix64-based index hashing and seed mixing.
    """
    if trial_index == 0:
        return base_seed & MASK64

    index_hash = splitmix64((trial_index * SPLITMIX_ADD) & MASK64)
    return splitmix64((base_seed ^ index_hash) & MASK64)


def find_farm_executable(hint: Optional[str] = None) -> Path:
    """
    Locates the TruthInTheFlip_Farm binary.
    """
    if hint:
        p = Path(hint).resolve()
        if p.is_file() and os.access(p, os.X_OK):
            return p
        # If it's a file but not executable on Linux, check if dotnet binary
        if p.is_file():
            return p
        raise FileNotFoundError(f"Specified Farm executable not found: {hint}")

    candidates = [
        Path("TruthInTheFlip_Farm/bin/Debug/net10.0/TruthInTheFlip_Farm"),
        Path("TruthInTheFlip_Farm/bin/Release/net10.0/TruthInTheFlip_Farm"),
        Path("../TruthInTheFlip_Farm/bin/Debug/net10.0/TruthInTheFlip_Farm"),
        Path("../TruthInTheFlip_Farm/bin/Release/net10.0/TruthInTheFlip_Farm"),
    ]

    for candidate in candidates:
        resolved = candidate.resolve()
        if resolved.is_file() and os.access(resolved, os.X_OK):
            return resolved

    which_farm = shutil.which("TruthInTheFlip_Farm")
    if which_farm:
        return Path(which_farm).resolve()

    raise FileNotFoundError(
        "TruthInTheFlip_Farm executable could not be found. "
        "Please provide --farm /path/to/TruthInTheFlip_Farm or build the project."
    )


def find_analyzer_script(hint: Optional[str] = None) -> Path:
    """
    Locates the window_scale_delta.py script.
    """
    if hint:
        p = Path(hint).resolve()
        if p.is_file():
            return p
        raise FileNotFoundError(f"Specified analyzer script not found: {hint}")

    candidates = [
        Path(__file__).resolve().parent / "window_scale_delta.py",
        Path("Scripts/window_scale_delta.py").resolve(),
        Path("../Scripts/window_scale_delta.py").resolve(),
    ]

    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()

    raise FileNotFoundError(
        "window_scale_delta.py analyzer script could not be found. "
        "Please provide --analyzer /path/to/window_scale_delta.py."
    )


def build_farm_command(
    farm_bin: str,
    tracker: str,
    strategy_window: str,
    seed: int,
    obs_windows: List[str],
) -> List[str]:
    """
    Constructs the argv list for TruthInTheFlip_Farm paired observational scale comparison.
    """
    cmd = [
        str(farm_bin),
        "json",
        "zip",
        ".expand.",
        "WS",
        *obs_windows,
        ":",
        "tracker",
        "window",
        "by_total",
        "WS",
        "synthetic",
        str(seed),
        "same_persistence_algorithmic",
        "file",
        str(tracker),
        str(strategy_window),
        ".expand_end.",
        ".END.",
        "item_0.absTotal",
        "item_1.absTotal",
        "sub#item_0.AnticipatedPercentage,item_1.AnticipatedPercentage",
    ]
    return cmd


def run_farm_trial(
    farm_bin: Path,
    tracker: Path,
    strategy_window: str,
    seed: int,
    obs_windows: List[str],
    output_path: Path,
) -> None:
    """
    Executes TruthInTheFlip_Farm and writes raw NDJSON output to output_path.
    """
    cmd = build_farm_command(
        str(farm_bin),
        str(tracker),
        strategy_window,
        seed,
        obs_windows,
    )
    with open(output_path, "wb") as f_out:
        proc = subprocess.run(
            cmd,
            stdout=f_out,
            stderr=subprocess.PIPE,
            check=False,
        )

    if proc.returncode != 0:
        stderr_msg = proc.stderr.decode("utf-8", errors="replace").strip()
        raise RuntimeError(
            f"Farm process failed with exit code {proc.returncode} for seed {seed}.\n"
            f"Command: {' '.join(cmd)}\n"
            f"Stderr: {stderr_msg}"
        )


def run_analyzer(
    analyzer_script: Path,
    trial_ndjson_path: Path,
) -> Dict[str, Any]:
    """
    Executes window_scale_delta.py --summary-json and returns the parsed summary dict.
    """
    cmd = [
        sys.executable,
        str(analyzer_script),
        str(trial_ndjson_path),
        "--summary-json",
    ]
    proc = subprocess.run(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        check=False,
    )

    if proc.returncode != 0:
        stderr_msg = proc.stderr.strip()
        raise RuntimeError(
            f"Analyzer failed with exit code {proc.returncode} on '{trial_ndjson_path}'.\n"
            f"Stderr: {stderr_msg}"
        )

    try:
        summary = json.loads(proc.stdout)
    except json.JSONDecodeError as e:
        raise ValueError(
            f"Analyzer produced invalid JSON on '{trial_ndjson_path}': {e}\nOutput: {proc.stdout}"
        ) from e

    return summary


def get_sidecar_metadata_path(output_path: Path) -> Path:
    """
    Returns the metadata sidecar path corresponding to the output NDJSON file.
    """
    if output_path.suffix.lower() == ".ndjson":
        return output_path.with_suffix(".meta.json")
    return output_path.parent / f"{output_path.name}.meta.json"


def load_completed_trials(output_path: Path, base_seed: int) -> Dict[int, Dict[str, Any]]:
    """
    Loads existing completed trial records from the output NDJSON file.
    Validates seed alignment and absence of duplicate trial indices.
    """
    completed: Dict[int, Dict[str, Any]] = {}
    if not output_path.exists():
        return completed

    with open(output_path, "r", encoding="utf-8") as f:
        for line_num, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                rec = json.loads(line)
            except json.JSONDecodeError as e:
                raise ValueError(
                    f"Corrupt NDJSON at {output_path}:{line_num}: {e}"
                ) from e

            if "TrialIndex" not in rec or "Seed" not in rec:
                raise ValueError(
                    f"Missing TrialIndex or Seed at {output_path}:{line_num}"
                )

            trial_idx = int(rec["TrialIndex"])
            stored_seed = int(rec["Seed"])

            if trial_idx in completed:
                raise ValueError(
                    f"Duplicate TrialIndex {trial_idx} found at {output_path}:{line_num}"
                )

            expected_seed = derive_trial_seed(base_seed, trial_idx)
            if stored_seed != expected_seed:
                raise ValueError(
                    f"Seed mismatch at {output_path}:{line_num} for TrialIndex {trial_idx}: "
                    f"found {stored_seed}, expected {expected_seed} from base seed {base_seed}"
                )

            completed[trial_idx] = rec

    return completed


def append_trial_record(output_path: Path, record: Dict[str, Any]) -> None:
    """
    Appends a single JSON record to the output NDJSON file and flushes to disk.
    """
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, "a", encoding="utf-8") as f:
        f.write(json.dumps(record, separators=(",", ":")) + "\n")
        f.flush()
        os.fsync(f.fileno())


def write_metadata(
    meta_path: Path,
    base_seed: int,
    trial_count: int,
    tracker: str,
    strategy_window: str,
    obs_windows: List[str],
    farm_bin: str,
    analyzer_script: str,
    completed_trials: int,
    null_spec: str = "same_persistence_algorithmic",
    start_time: Optional[str] = None,
) -> None:
    """
    Writes or updates the experiment metadata sidecar JSON file.
    """
    meta: Dict[str, Any] = {}
    if meta_path.exists():
        try:
            with open(meta_path, "r", encoding="utf-8") as f:
                meta = json.load(f)
        except Exception:
            meta = {}

    now_iso = datetime.datetime.now(datetime.timezone.utc).isoformat()
    if "startTime" not in meta or not meta["startTime"]:
        meta["startTime"] = start_time or now_iso

    meta.update({
        "baseSeed": base_seed,
        "trialCount": trial_count,
        "tracker": str(tracker),
        "strategyWindow": strategy_window,
        "observationalWindows": obs_windows,
        "farm": str(farm_bin),
        "analyzer": str(analyzer_script),
        "nullSpec": null_spec,
        "lastUpdated": now_iso,
        "completedTrials": completed_trials,
    })

    meta_path.parent.mkdir(parents=True, exist_ok=True)
    temp_meta = meta_path.with_suffix(".tmp.json")
    with open(temp_meta, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
    temp_meta.replace(meta_path)


def validate_metadata(
    meta_path: Path,
    base_seed: int,
    tracker: str,
    strategy_window: str,
    obs_windows: List[str],
    null_spec: str = "same_persistence_algorithmic",
) -> None:
    """
    Validates that existing metadata matches current experiment configuration.
    """
    if not meta_path.exists():
        return

    with open(meta_path, "r", encoding="utf-8") as f:
        meta = json.load(f)

    if meta.get("baseSeed") != base_seed:
        raise ValueError(
            f"Metadata mismatch in '{meta_path}': baseSeed {meta.get('baseSeed')} != requested {base_seed}"
        )
    if meta.get("tracker") != str(tracker):
        raise ValueError(
            f"Metadata mismatch in '{meta_path}': tracker '{meta.get('tracker')}' != requested '{tracker}'"
        )
    if meta.get("strategyWindow") != strategy_window:
        raise ValueError(
            f"Metadata mismatch in '{meta_path}': strategyWindow '{meta.get('strategyWindow')}' != requested '{strategy_window}'"
        )
    if meta.get("observationalWindows") != obs_windows:
        raise ValueError(
            f"Metadata mismatch in '{meta_path}': observationalWindows {meta.get('observationalWindows')} != requested {obs_windows}"
        )
    if meta.get("nullSpec") != null_spec:
        raise ValueError(
            f"Metadata mismatch in '{meta_path}': nullSpec '{meta.get('nullSpec')}' != requested '{null_spec}'"
        )


def run_population(
    trials: int,
    base_seed: int,
    tracker_path: Path,
    strategy_window: str,
    farm_path: Path,
    analyzer_path: Path,
    output_path: Path,
    keep_trials: bool = False,
    trials_dir: Optional[Path] = None,
    obs_windows: Optional[List[str]] = None,
    continue_on_error: bool = False,
) -> int:
    """
    Main orchestration function for running the scale-delta null population.
    """
    if obs_windows is None:
        obs_windows = ["10B", "100B"]

    if not tracker_path.exists():
        raise FileNotFoundError(f"Tracker file not found: {tracker_path}")

    meta_path = get_sidecar_metadata_path(output_path)
    validate_metadata(
        meta_path,
        base_seed=base_seed,
        tracker=str(tracker_path),
        strategy_window=strategy_window,
        obs_windows=obs_windows,
    )

    completed = load_completed_trials(output_path, base_seed=base_seed)
    initial_completed_count = len(completed)

    if keep_trials:
        if trials_dir is None:
            trials_dir = output_path.parent / "raw_trials"
        trials_dir.mkdir(parents=True, exist_ok=True)

    start_iso = datetime.datetime.now(datetime.timezone.utc).isoformat()
    write_metadata(
        meta_path=meta_path,
        base_seed=base_seed,
        trial_count=trials,
        tracker=str(tracker_path),
        strategy_window=strategy_window,
        obs_windows=obs_windows,
        farm_bin=str(farm_path),
        analyzer_script=str(analyzer_path),
        completed_trials=len(completed),
        start_time=start_iso,
    )

    newly_completed = 0

    for trial_idx in range(trials):
        seed = derive_trial_seed(base_seed, trial_idx)

        if trial_idx in completed:
            sys.stderr.write(
                f"[{trial_idx + 1}/{trials}] trial={trial_idx} seed={seed} (already completed, skipping)\n"
            )
            sys.stderr.flush()
            continue

        sys.stderr.write(
            f"[{trial_idx + 1}/{trials}] trial={trial_idx} seed={seed}\n"
        )
        sys.stderr.flush()

        if keep_trials and trials_dir:
            trial_raw_path = trials_dir / f"trial_{trial_idx:04d}_seed_{seed}.ndjson"
            is_temp = False
        else:
            temp_fd, temp_name = tempfile.mkstemp(suffix=".ndjson", prefix=f"trial_{trial_idx}_")
            os.close(temp_fd)
            trial_raw_path = Path(temp_name)
            is_temp = True

        try:
            # 1. Invoke Farm
            run_farm_trial(
                farm_bin=farm_path,
                tracker=tracker_path,
                strategy_window=strategy_window,
                seed=seed,
                obs_windows=obs_windows,
                output_path=trial_raw_path,
            )

            # 2. Invoke Analyzer
            summary = run_analyzer(
                analyzer_script=analyzer_path,
                trial_ndjson_path=trial_raw_path,
            )

            # 3. Construct record (ensuring TrialIndex and Seed are leading fields)
            record: Dict[str, Any] = {
                "TrialIndex": trial_idx,
                "Seed": seed,
            }
            record.update(summary)

            # 4. Append to population NDJSON
            append_trial_record(output_path, record)
            completed[trial_idx] = record
            newly_completed += 1

            # 5. Update metadata
            write_metadata(
                meta_path=meta_path,
                base_seed=base_seed,
                trial_count=trials,
                tracker=str(tracker_path),
                strategy_window=strategy_window,
                obs_windows=obs_windows,
                farm_bin=str(farm_path),
                analyzer_script=str(analyzer_path),
                completed_trials=len(completed),
            )

        except Exception as e:
            sys.stderr.write(f"Error in trial {trial_idx} (seed {seed}): {e}\n")
            sys.stderr.flush()
            if not continue_on_error:
                raise
        finally:
            if is_temp and trial_raw_path.exists():
                try:
                    trial_raw_path.unlink()
                except Exception:
                    pass

    return newly_completed


def parse_args(argv: Optional[List[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Automated window scale-delta null population runner."
    )
    parser.add_argument(
        "--trials",
        type=int,
        default=5,
        help="Number of trials to execute (default: 5)",
    )
    parser.add_argument(
        "--base-seed",
        type=int,
        default=12345,
        help="Base 64-bit seed for deterministic trial generation (default: 12345)",
    )
    parser.add_argument(
        "--tracker",
        type=str,
        default="Artifacts/Trackers/SamePersistence.NET1.rep2.tkr",
        help="Path to historical tracker file (default: Artifacts/Trackers/SamePersistence.NET1.rep2.tkr)",
    )
    parser.add_argument(
        "--strategy-window",
        type=str,
        default="10B",
        help="Strategy decision window size (default: 10B)",
    )
    parser.add_argument(
        "--farm",
        type=str,
        default=None,
        help="Path to TruthInTheFlip_Farm binary (default: auto-detected)",
    )
    parser.add_argument(
        "--analyzer",
        type=str,
        default=None,
        help="Path to window_scale_delta.py analyzer script (default: auto-detected)",
    )
    parser.add_argument(
        "--output",
        type=str,
        required=True,
        help="Path to output population NDJSON file",
    )
    parser.add_argument(
        "--keep-trials",
        action="store_true",
        help="Retain raw per-trial Farm NDJSON files instead of deleting them after analysis",
    )
    parser.add_argument(
        "--trials-dir",
        type=str,
        default=None,
        help="Directory to store retained raw trial NDJSON files when --keep-trials is enabled",
    )
    parser.add_argument(
        "--observational-windows",
        nargs="+",
        default=["10B", "100B"],
        help="Observational window sizes (default: 10B 100B)",
    )
    parser.add_argument(
        "--continue-on-error",
        action="store_true",
        help="Continue executing remaining trials if an individual trial fails",
    )
    return parser.parse_args(argv)


def main(argv: Optional[List[str]] = None) -> None:
    args = parse_args(argv)

    farm_path = find_farm_executable(args.farm)
    analyzer_path = find_analyzer_script(args.analyzer)
    tracker_path = Path(args.tracker).resolve()
    output_path = Path(args.output).resolve()
    trials_dir = Path(args.trials_dir).resolve() if args.trials_dir else None

    if args.trials <= 0:
        raise ValueError(f"--trials must be positive, got {args.trials}")

    run_population(
        trials=args.trials,
        base_seed=args.base_seed,
        tracker_path=tracker_path,
        strategy_window=args.strategy_window,
        farm_path=farm_path,
        analyzer_path=analyzer_path,
        output_path=output_path,
        keep_trials=args.keep_trials,
        trials_dir=trials_dir,
        obs_windows=args.observational_windows,
        continue_on_error=args.continue_on_error,
    )


if __name__ == "__main__":
    main()
