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

from Loader import query_horizon_segments

def load_report(executable: str, arguments: list[str]) -> pd.DataFrame:
    try:
        result = subprocess.run(
            [executable, *arguments],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            check=False,
        )
    except FileNotFoundError:
        print(
            f"Could not find TruthInTheFlip Farm executable: {executable}",
            file=sys.stderr,
        )
        print(
            "Install TruthInTheFlip_Farm on PATH or provide it with --farm.",
            file=sys.stderr,
        )
        raise SystemExit(127)

    if result.returncode != 0:
        if result.stderr:
            print(result.stderr, file=sys.stderr, end="")
        raise SystemExit(result.returncode)

    return pd.read_csv(io.StringIO(result.stdout))


def load_tracker_frame(executable, path, horizon, fields):
    frame = load_report(
        executable,
        [
            "csv",
            "tracker",
            *horizon,
            "window",
            "by_total",
            "100B",
            "file",
            str(path),
            *fields
        ],
    )

    required = {*fields}
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

    if not tracker_path.is_dir():
        print(
            f"Tracker directory not found: {tracker_path}",
            file=sys.stderr,
        )
        print(
            "specify with --tracker-dir",
            file=sys.stderr,
        )
        raise SystemExit(2)

    horizon = [
        "from", "absTotal", "100B",
        "to",
        "absTotal",
        args.horizon,
    ]

    fields = [
        "absTotal",
        "AnticipatedPercentage",
        "SamePercentage",
    ]

    fields = [
        "Index",
        "End.absTotal",
        "EndTrueZ",
        "End.SamePercentage",
        "sub#End.AnticipatedPercentage,50",
    ]

    filename = "Quant.tkr"

    df = query_horizon_segments(
        executable,
        tracker=tracker_path / filename,
        tracker_modifier=["full","window","by_total", "100B"],
        horizon_start=100_000_000_000,
        horizon_end=8_903_400_000_000,
        segment_count=12,
        process="segment",
        process_arguments=["by_total","100B"],
        fields=fields,
    )

    if (False):
        fig, ax = plt.subplots(figsize=(10, 6))

        for region, part in df.groupby("Region"):
            ax.plot(
                part["Index"],
                part["sub#End.AnticipatedPercentage,50"],
                label=f"Region {region}",
            )

        ax.axhline(0.0, linewidth=1)
        ax.set_title("Anticipated by region")
        ax.set_xlabel("Segment Index")
        ax.set_ylabel("Anticipated Edge")
        ax.legend()

        fig.tight_layout()
        plt.show()
    else:
        fold = (
            df.groupby("Index")["sub#End.AnticipatedPercentage,50"]
            .agg(["min", "max", "mean"])
            .reset_index()
        )

        fig, ax = plt.subplots(figsize=(10, 6))

        ax.fill_between(
            fold["Index"],
            fold["min"],
            fold["max"],
            alpha=0.2,
        )

        ax.plot(
            fold["Index"],
            fold["mean"],
            label="Mean",
        )

        ax.axhline(0.0, linewidth=1)
        ax.set_title(filename + " Anticipated edge envelope by folded region")
        ax.set_xlabel("Segment Index")
        ax.set_ylabel("Anticipated Edge")
        ax.legend()

        fig.tight_layout()
        plt.show()


if __name__ == "__main__":
    main()
