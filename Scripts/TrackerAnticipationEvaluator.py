from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
import pandas as pd

from Loader import load_tracker_frame


TARGET = "offset50#mean#AnticipatedPercentage"

CANDIDATES = [
    "BetSameGapTrend#4",
    "BetSameGapTrend#8",
]


def parse_args():
    script_dir = Path(__file__).resolve().parent

    default_tracker_dir = (
        script_dir.parent
        / "Artifacts"
        / "Trackers"
    )

    parser = argparse.ArgumentParser(
        description=(
            "Evaluate whether causal anticipation-state metrics add "
            "predictive information beyond current anticipated-edge persistence."
        )
    )

    parser.add_argument(
        "--farm",
        default="TruthInTheFlip_Farm_Experimental",
        help="TruthInTheFlip Farm executable.",
    )

    parser.add_argument(
        "--tracker-dir",
        type=Path,
        default=default_tracker_dir,
        help="Directory containing tracker files.",
    )

    parser.add_argument(
        "--horizon",
        default="8903400M",
        help="Maximum absolute total to include.",
    )

    parser.add_argument(
        "--train-fraction",
        type=float,
        default=0.70,
        help="Chronological fraction used for training.",
    )

    parser.add_argument(
        "--lag",
        type=int,
        default=1,
        help="Number of 10B segments into the future to predict.",
    )

    return parser.parse_args()


def linear_fit(x: np.ndarray, y: np.ndarray):
    """
    Ordinary least-squares regression with an intercept.
    """

    design = np.column_stack([
        np.ones(len(x)),
        x,
    ])

    coefficients, *_ = np.linalg.lstsq(
        design,
        y,
        rcond=None,
    )

    return coefficients


def linear_predict(
    coefficients: np.ndarray,
    x: np.ndarray,
):
    design = np.column_stack([
        np.ones(len(x)),
        x,
    ])

    return design @ coefficients


def mse(actual, predicted):
    return float(
        np.mean(
            np.square(actual - predicted)
        )
    )


def correlation(actual, predicted):
    if len(actual) < 2:
        return float("nan")

    return float(
        np.corrcoef(actual, predicted)[0, 1]
    )


def evaluate_candidate(
    df: pd.DataFrame,
    candidate: str,
    lag: int,
    train_fraction: float,
):
    #
    # Every row contains information known at t.
    # shift(-lag) supplies the later target.
    #
    work = pd.DataFrame({
        "CurrentTarget": df[TARGET],
        "State": df[candidate],
        "FutureTarget": df[TARGET].shift(-lag),
    }).dropna()

    split = int(len(work) * train_fraction)

    if split < 2 or len(work) - split < 2:
        raise ValueError(
            f"Not enough rows for train/test split: {len(work)}"
        )

    train = work.iloc[:split]
    test = work.iloc[split:]

    #
    # Baseline:
    #
    #   FutureTarget =
    #       a + b * CurrentTarget
    #
    baseline_coefficients = linear_fit(
        train[["CurrentTarget"]].to_numpy(),
        train["FutureTarget"].to_numpy(),
    )

    baseline_prediction = linear_predict(
        baseline_coefficients,
        test[["CurrentTarget"]].to_numpy(),
    )

    #
    # Candidate:
    #
    #   FutureTarget =
    #       a
    #       + b * CurrentTarget
    #       + c * State
    #
    candidate_coefficients = linear_fit(
        train[
            [
                "CurrentTarget",
                "State",
            ]
        ].to_numpy(),
        train["FutureTarget"].to_numpy(),
    )

    candidate_prediction = linear_predict(
        candidate_coefficients,
        test[
            [
                "CurrentTarget",
                "State",
            ]
        ].to_numpy(),
    )

    actual = test["FutureTarget"].to_numpy()

    baseline_mse = mse(
        actual,
        baseline_prediction,
    )

    candidate_mse = mse(
        actual,
        candidate_prediction,
    )

    if baseline_mse == 0:
        improvement_pct = float("nan")
    else:
        improvement_pct = (
            (baseline_mse - candidate_mse)
            / baseline_mse
            * 100.0
        )

    return {
        "Metric": candidate,
        "Rows": len(work),
        "TrainRows": len(train),
        "TestRows": len(test),

        "BaselineMSE": baseline_mse,
        "CandidateMSE": candidate_mse,
        "ImprovementPct": improvement_pct,

        "BaselineCorr": correlation(
            actual,
            baseline_prediction,
        ),

        "CandidateCorr": correlation(
            actual,
            candidate_prediction,
        ),

        #
        # Coefficient belonging specifically to
        # BetSameGapTrend in the candidate model.
        #
        "StateCoefficient": candidate_coefficients[2],
    }


def load_tracker(
    executable,
    tracker_path,
    horizon,
):
    fields = [
        "End.absTotal",
        TARGET,
        *CANDIDATES,
    ]

    df = load_tracker_frame(
        executable=executable,
        process="segment",
        path=tracker_path,
        horizon=[
            "full",
            "to",
            "absTotal",
            horizon,
        ],
        fields=fields,
        process_args=[
            "full",
            "by_total",
            "10B",
        ],
    )

    return (
        df
        .sort_values("End.absTotal")
        .reset_index(drop=True)
    )


def main():
    args = parse_args()

    trackers = {
        "Crypto 3": "crypto3.tkr",
        "Random SD": "crypto_RandomSD.tkr",
        "Quant": "Quant.tkr",
        "Quant IDQE": "Quant_IDQE.tkr",
    }

    results = []

    for tracker_name, filename in trackers.items():
        path = args.tracker_dir / filename

        print()
        print("=" * 72)
        print(tracker_name)
        print(path)
        print("=" * 72)

        df = load_tracker(
            args.farm,
            path,
            args.horizon,
        )

        for candidate in CANDIDATES:
            result = evaluate_candidate(
                df=df,
                candidate=candidate,
                lag=args.lag,
                train_fraction=args.train_fraction,
            )

            result["Tracker"] = tracker_name
            result["Lag"] = args.lag

            results.append(result)

            print()
            print(candidate)
            print(
                f"  rows             : "
                f"{result['Rows']}"
            )
            print(
                f"  train / test     : "
                f"{result['TrainRows']} / "
                f"{result['TestRows']}"
            )
            print(
                f"  baseline MSE     : "
                f"{result['BaselineMSE']:.12g}"
            )
            print(
                f"  candidate MSE    : "
                f"{result['CandidateMSE']:.12g}"
            )
            print(
                f"  improvement      : "
                f"{result['ImprovementPct']:+.6f}%"
            )
            print(
                f"  baseline corr    : "
                f"{result['BaselineCorr']:+.6f}"
            )
            print(
                f"  candidate corr   : "
                f"{result['CandidateCorr']:+.6f}"
            )
            print(
                f"  state coefficient: "
                f"{result['StateCoefficient']:+.12g}"
            )

    report = pd.DataFrame(results)

    columns = [
        "Tracker",
        "Metric",
        "Lag",
        "TrainRows",
        "TestRows",
        "BaselineMSE",
        "CandidateMSE",
        "ImprovementPct",
        "BaselineCorr",
        "CandidateCorr",
        "StateCoefficient",
    ]

    report = report[columns]

    print()
    print("=" * 72)
    print("SUMMARY")
    print("=" * 72)
    print(
        report.to_string(
            index=False,
            float_format=lambda x: f"{x:.8g}",
        )
    )


if __name__ == "__main__":
    main()