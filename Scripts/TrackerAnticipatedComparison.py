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
from matplotlib.lines import segment_hits

from Loader import load_tracker_frame


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
        "full",
        "to","absTotal",
        args.horizon,
    ]

    expressions = {
        "Anticipated": "offset50#mean#AnticipatedPercentage",
        "Bet Same Win Rate": "offset50#mean#BetSameWinRate",
        "Same": "offset50#mean#SamePercentage",
    }

    fields = [
        "End.absTotal",
        *expressions.values(),
    ]

    process = "segment"
    process_args = [ "full", "by_total", "10B"]

    frames = {
        "Crypto 3": load_tracker_frame(
            executable,
            process,
            tracker_path / "crypto3.tkr",
            horizon,
            fields,
            process_args,
        ),
        "Random SD": load_tracker_frame(
            executable,
            process,
            tracker_path / "crypto_RandomSD.tkr",
            horizon,
            fields,
            process_args,
        ),
        "Quant": load_tracker_frame(
            executable,
            process,
            tracker_path / "Quant.tkr",
            horizon,
            fields,
            process_args,

        ),
        "Quant IDQE": load_tracker_frame(
            executable,
            process,
            tracker_path / "Quant_IDQE.tkr",
            horizon,
            fields,
            process_args,
        ),
    }

    fig, axes = plt.subplots(
        nrows=len(frames),
        ncols=1,
        sharex=True,
        sharey=True,
        figsize=(16, 8),
    )

    for ax, (name, df) in zip(axes, frames.items()):
        for label, expr in expressions.items():
            ax.plot(
                df["End.absTotal"],
                df[expr],
                label=label,
            )

        ax.set_title(name)
        ax.axhline(0.0, linewidth=1)

    handles, labels = axes[0].get_legend_handles_labels()

    fig.legend(
        handles,
        labels,
        loc="upper center",
        ncol=3,
    )

    fig.supylabel("Percentage edge")
    fig.supxlabel("Absolute flips")

    fig.subplots_adjust(
        left=0.07,
        right=0.99,
        bottom=0.08,
        top=0.91,
        hspace=0.55,
    )

    plt.show()


if __name__ == "__main__":
    main()
