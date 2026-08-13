from __future__ import annotations

import argparse
import io
import subprocess
import sys
from pathlib import Path

import pandas as pd
import matplotlib

# Force Matplotlib to use the Tkinter GUI backend
# matplotlib.use('TkAgg')
import matplotlib.pyplot as plt

def load_report(executable: str, arguments: list[str]) -> pd.DataFrame:
    result = subprocess.run(
        [executable, *arguments],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        check=False,
    )

    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        raise SystemExit(result.returncode)

    return pd.read_csv(io.StringIO(result.stdout))

def load_tracker_frame(executable, path, horizon, fields):

    frame = load_report(
        executable,
        [
            "csv",
            "segment",
            *horizon,
            "window",
            "by_total",
            "100B",
            "file",
            str(path),
            "by_total",
            "100B",
            *fields
        ],
    )

    required = { *fields }
    missing = required.difference(frame.columns)

    if missing:
        raise ValueError(
            f"Report is missing expected columns: {sorted(missing)}"
        )

    return frame

def parse_args():
    script_dir = Path(__file__).resolve().parent

    default_tracker_dir = (
        script_dir.parent
        / "Artifacts"
        / "Trackers"
    )

    parser = argparse.ArgumentParser(
        description="Compare True Z trajectories across multiple TruthInTheFlip trackers."
    )

    parser.add_argument(
        "--farm",
        default="TruthInTheFlip_Farm",
        help=(
            "TruthInTheFlip_Farm executable. "
            "Defaults to TruthInTheFlip_Farm on PATH."
        ),
    )

    parser.add_argument(
        "--tracker-dir",
        type=Path,
        default=default_tracker_dir,
        help=(
            "Directory containing tracker files. "
            f"Defaults to {default_tracker_dir}"
        ),
    )

    parser.add_argument(
        "--horizon",
        default="8903400M",
        help="Maximum absolute total to include.",
    )

    return parser.parse_args()

def main() -> None:
    args = parse_args()

    executable = args.farm
    tracker_path = args.tracker_dir

    horizon = [
        "to",
        "absTotal",
        args.horizon,
    ]

    fields = [
        "Index",
        "EndTotal",
        "MeanTrueZ",
        "EndTrueZ",
        "BestTrueZ",
        "End.absTotal",
    ]

    frames = {
        "Quant": load_tracker_frame(
            executable,
            tracker_path / "Quant.tkr",
            horizon,
            fields,
        ),
        "Crypto 3": load_tracker_frame(
            executable,
            tracker_path / "crypto3.tkr",
            horizon,
            fields,
        ),
        "Random SD": load_tracker_frame(
            executable,
            tracker_path / "crypto_RandomSD.tkr",
            horizon,
            fields,
        ),
    }


    fig, axes = plt.subplots(
        nrows=len(frames),
        ncols=1,
        sharex=True,
        sharey=True
    )

    for ax, (name, df) in zip(axes, frames.items()):
        ax.plot(df["End.absTotal"], df["EndTrueZ"])
        ax.set_title(name)
        ax.axhline(0.0, linewidth=1)

    plt.ylabel("True Z")
    plt.xlabel("Absolute Flips")
    plt.tight_layout()
    plt.show()


if __name__ == "__main__":
    main()
