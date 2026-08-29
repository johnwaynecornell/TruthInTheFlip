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

def load_my_frame(executable, process, path, horizon, fields, process_args):
    df= load_tracker_frame(executable, process, path, horizon, fields, process_args)
    df = df.sort_values("End.absTotal").reset_index(drop=True)

    df["NextAnticipatedEdge"] = (
        df["offset50#mean#AnticipatedPercentage"]
        .shift(-1)
    )

    for grain in [ "4","8" ]: #,"16","32","64","128" ]:
        #
        # paired = df.dropna(
        #     subset=[
        #         "BetSameGapTrend#128",
        #         "NextAnticipatedEdge",
        #     ]
        # )
        #
        # corr = paired["BetSameGapTrend#128"].corr(
        #     paired["NextAnticipatedEdge"]
        # )
        #
        # print(str(path) + ":lag +1 correlation:", corr)
        #

        name="BetSameGapTrend#"+grain

        print(name)

        target = "offset50#mean#AnticipatedPercentage"

        print("    auto correlation")
        print("      " + "self")
        for lag in range(1, 11):
            target_auto = df[target].corr(df[target].shift(-lag))

            backward = df[name].corr(df[target].shift(lag))
            forward = df[name].corr(df[target].shift(-lag))

            print("       ", lag, backward, target_auto, forward)

        for metric in [
            "offset50#mean#BetSameWinRate",
            "offset50#mean#SamePercentage",
            "BetSameGapTrend#"+grain,
        ]:
            print("      " + metric)

            for lag in range(0, 11):
                print(
                    "       ", lag,
                    df[metric].corr(df[target].shift(-lag))
                )

        print("     lag behavior")

        for lag in range(1, 11):
            future = df[target].shift(-lag)
            corr_future = df["BetSameGapTrend#"+grain].corr(future)

            past = df[target].shift(lag)
            corr_past = df["BetSameGapTrend#" + grain].corr(past)

            print("       ", lag, corr_future, " | ", -lag, corr_past)

    return df

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
        #"BetSameGapTrend#128": "BetSameGapTrend#128",
    }

    fields = [
        "End.absTotal",
        "BetSameGapTrend#4",
        "BetSameGapTrend#8",
        "BetSameGapTrend#16",
        "BetSameGapTrend#32",
        "BetSameGapTrend#64",
        "BetSameGapTrend#128",
        "offset50#mean#AnticipatedPercentage",
        "offset50#mean#BetSameWinRate",
        "offset50#mean#SamePercentage",
        *expressions.values(),
    ]

    process = "segment"
    process_args = [ "full", "by_total", "10B"]

    frames = {
        "Crypto 3": load_my_frame(
            executable,
            process,
            tracker_path / "crypto3.tkr",
            horizon,
            fields,
            process_args,
        ),
        "Random SD": load_my_frame(
            executable,
            process,
            tracker_path / "crypto_RandomSD.tkr",
            horizon,
            fields,
            process_args,
        ),
        "Quant": load_my_frame(
            executable,
            process,
            tracker_path / "Quant.tkr",
            horizon,
            fields,
            process_args,

        ),
        "Quant IDQE": load_my_frame(
            executable,
            process,
            tracker_path / "Quant_IDQE.tkr",
            horizon,
            fields,
            process_args,
        ),
    }

if __name__ == "__main__":
    main()
