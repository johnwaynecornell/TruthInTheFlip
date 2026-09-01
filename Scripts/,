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

def plot_comparison(frames):
    fig, axes = plt.subplots(
        nrows=2,
        ncols=2,
        figsize=(13, 9),
    )

    ax_z = axes[0, 0]
    ax_scatter = axes[0, 1]
    ax_same = axes[1, 0]
    ax_anticipated = axes[1, 1]

    for name, df in frames.items():
        anticipated_edge = (
                df["End.AnticipatedPercentage"] - 50.0
        )

        same_edge = (
                df["End.SamePercentage"] - 50.0
        )
        #
        # True Z trajectory
        #
        ax_z.plot(
            df["End.absTotal"],
            df["EndTrueZ"],
            label=name,
        )

        #
        # Anticipated vs Same scatter
        #
        ax_scatter.scatter(
            anticipated_edge,
            same_edge,
            label=name,
            alpha=0.6,
        )

        #
        # Same percentage trajectory
        #
        ax_same.plot(
            df["End.absTotal"],
            same_edge,
            label=name,
        )

        #
        # Anticipated percentage trajectory
        #
        ax_anticipated.plot(
            df["End.absTotal"],
            anticipated_edge,
            label=name,
        )

    #
    # True Z
    #
    ax_z.axhline(0.0, linewidth=1)
    ax_z.set_title("True Z by absolute flips")
    ax_z.set_xlabel("Absolute flips")
    ax_z.set_ylabel("True Z")
    ax_z.legend()

    #
    # Scatter
    #
    ax_scatter.axhline(0.0, linewidth=1)
    ax_scatter.axvline(0.0, linewidth=1)
    ax_scatter.set_title("Anticipated vs Same")
    ax_scatter.set_xlabel("Anticipated edge %")
    ax_scatter.set_ylabel("Same edge %")
    ax_scatter.legend()

    #
    # Same
    #
    ax_same.axhline(0.0, linewidth=1)
    ax_same.set_title("Same percentage")
    ax_same.set_xlabel("Absolute flips")
    ax_same.set_ylabel("Same edge %")

    #
    # Anticipated
    #
    ax_anticipated.axhline(0.0, linewidth=1)
    ax_anticipated.set_title("Anticipated percentage")
    ax_anticipated.set_xlabel("Absolute flips")
    ax_anticipated.set_ylabel("Anticipated edge %")

    fig.suptitle(
        "TruthInTheFlip Tracker Comparison — 100B Segments",
    )

    fig.tight_layout()
    plt.show()

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
        "to", "absTotal", args.horizon,
    ]

    fields = [
        "End.absTotal",
        "EndTrueZ",
        "End.SamePercentage",
        "End.AnticipatedPercentage",
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
        # When mature:
        "Quant IDQE": load_tracker_frame(
            executable,
            tracker_path / "Quant_IDQE.tkr",
            horizon,
            fields,
        ),
    }

    plot_comparison(frames)

if __name__ == "__main__":
    main()
