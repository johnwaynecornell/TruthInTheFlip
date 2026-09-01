from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
import pandas as pd

from Loader import load_tracker_frame


#TARGET = "offset50#mean#AnticipatedPercentage"
TARGET = "offset50#mean#BetSameWinRate"

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
            "Walk-forward evaluation of causal anticipation-state metrics "
            "against an anticipated-edge persistence baseline."
        )
    )

    parser.add_argument(
        "--farm",
        default="TruthInTheFlip_Farm_Experimental",
        help="Experimental TruthInTheFlip Farm executable.",
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
        "--lag",
        type=int,
        default=1,
        help="Number of 10B segments ahead to predict.",
    )

    return parser.parse_args()


def linear_fit(x: np.ndarray, y: np.ndarray) -> np.ndarray:
    design = np.column_stack(
        [
            np.ones(len(x)),
            x,
        ]
    )

    coefficients, *_ = np.linalg.lstsq(
        design,
        y,
        rcond=None,
    )

    return coefficients


def linear_predict(
    coefficients: np.ndarray,
    x: np.ndarray,
) -> np.ndarray:
    design = np.column_stack(
        [
            np.ones(len(x)),
            x,
        ]
    )

    return design @ coefficients


def mse(actual: np.ndarray, predicted: np.ndarray) -> float:
    return float(
        np.mean(
            np.square(actual - predicted)
        )
    )


def correlation(
    actual: np.ndarray,
    predicted: np.ndarray,
) -> float:
    if len(actual) < 2:
        return float("nan")

    if np.std(actual) == 0 or np.std(predicted) == 0:
        return float("nan")

    return float(
        np.corrcoef(actual, predicted)[0, 1]
    )


def make_work_frame(
    df: pd.DataFrame,
    candidate: str,
    lag: int,
) -> pd.DataFrame:
    return pd.DataFrame(
        {
            "End.absTotal": df["End.absTotal"],
            "CurrentTarget": df[TARGET],
            "State": df[candidate],
            "FutureTarget": df[TARGET].shift(-lag),
        }
    ).dropna().reset_index(drop=True)

def residualize_from_train(
    train_x: np.ndarray,
    train_y: np.ndarray,
    test_x: np.ndarray,
    test_y: np.ndarray,
) -> np.ndarray:
    coefficients = linear_fit(
        train_x,
        train_y,
    )

    prediction = linear_predict(
        coefficients,
        test_x,
    )

    return test_y - prediction

def evaluate_fold(
    work: pd.DataFrame,
    train_end: int,
    test_end: int,
):
    train = work.iloc[:train_end]
    test = work.iloc[train_end:test_end]

    #
    # Persistence-only baseline:
    #
    # FutureTarget =
    #   a + b * CurrentTarget
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
    # The baseline residual is the portion of the future
    # BetSame edge that current BetSame persistence failed
    # to explain.
    #
    # This is evaluated only on the unseen test block.
    #
    actual = test["FutureTarget"].to_numpy()
    state = test["State"].to_numpy()

    #
    # Residual left after the persistence-only baseline.
    #
    baseline_residual = (
                actual
         - baseline_prediction
        )

    residual_corr = correlation(
            state,
            baseline_residual,
    )

    #
    # More stringent partial-correlation view:
    #
    # Remove the component of State explained by CurrentTarget
    # using a model fitted only on the training region.
    #
    current_train = train[["CurrentTarget"]].to_numpy()
    current_test = test[["CurrentTarget"]].to_numpy()

    state_residual = residualize_from_train(
            current_train,
            train["State"].to_numpy(),
            current_test,
            state,
    )

    partial_corr = correlation(
            state_residual,
            baseline_residual,
    )

    #
    # Candidate:
    #
    # FutureTarget =
    #   a
    #   + b * CurrentTarget
    #   + c * State
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

    baseline_mse = mse(
        actual,
        baseline_prediction,
    )

    candidate_mse = mse(
        actual,
        candidate_prediction,
    )

    improvement_pct = (
        (baseline_mse - candidate_mse)
        / baseline_mse
        * 100.0
        if baseline_mse != 0
        else float("nan")
    )

    return {
        "TrainRows": len(train),
        "TestRows": len(test),

        "TestBegin": int(test["End.absTotal"].iloc[0]),
        "TestEnd": int(test["End.absTotal"].iloc[-1]),

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

        "ResidualCorr": residual_corr,
        "PartialCorr": partial_corr,

        "StateCoefficient": float(
            candidate_coefficients[2]
        ),
    }


def walk_forward(
    df: pd.DataFrame,
    candidate: str,
    lag: int,
):
    work = make_work_frame(
        df,
        candidate,
        lag,
    )

    n = len(work)

    #
    # Expanding training:
    #
    #   0-50 -> 50-60
    #   0-60 -> 60-70
    #   0-70 -> 70-80
    #   0-80 -> 80-90
    #   0-90 -> 90-100
    #
    boundaries = [
        0.50,
        0.60,
        0.70,
        0.80,
        0.90,
        1.00,
    ]

    indices = [
        int(n * fraction)
        for fraction in boundaries
    ]

    folds = []

    for fold_index in range(5):
        train_end = indices[fold_index]
        test_end = indices[fold_index + 1]

        if train_end < 2:
            continue

        if test_end - train_end < 2:
            continue

        result = evaluate_fold(
            work,
            train_end,
            test_end,
        )

        result["Fold"] = fold_index + 1
        result["Metric"] = candidate
        result["Lag"] = lag

        folds.append(result)

    return pd.DataFrame(folds)


def summarize_folds(
    tracker: str,
    candidate: str,
    folds: pd.DataFrame,
):
    improvements = folds["ImprovementPct"]
    residuals = folds["ResidualCorr"]
    partials = folds["PartialCorr"]

    return {
        "Tracker": tracker,
        "Metric": candidate,

        "Folds": len(folds),
        "PositiveFolds": int(
            (improvements > 0).sum()
        ),

        "PositiveResidualFolds": int(
            (residuals > 0).sum()
        ),

        "PositivePartialFolds": int(
            (partials > 0).sum()
        ),

        "MeanImprovementPct": float(
            improvements.mean()
        ),

        "MedianImprovementPct": float(
            improvements.median()
        ),

        "WorstImprovementPct": float(
            improvements.min()
        ),

        "BestImprovementPct": float(
            improvements.max()
        ),
        "MeanResidualCorr": float(
                    residuals.mean()
        ),

        "MedianResidualCorr": float(
                    residuals.median()
        ),

        "WorstResidualCorr": float(
                    residuals.min()
        ),

        "BestResidualCorr": float(
                    residuals.max()
        ),
        "MeanPartialCorr": float(
                partials.mean()
        ),

        "MedianPartialCorr": float(
                partials.median()
        ),

        "WorstPartialCorr": float(
                partials.min()
        ),

        "BestPartialCorr": float(
                partials.max()
        ),


        #
        # Useful secondary summary:
        #
        "MeanBaselineCorr": float(
            folds["BaselineCorr"].mean()
        ),

        "MeanCandidateCorr": float(
            folds["CandidateCorr"].mean()
        ),
    }


def load_tracker(
    executable: str,
    tracker_path: Path,
    horizon: str,
):
    fields = [
        "End.absTotal",
        TARGET,
        *CANDIDATES,
    ]

    frame = load_tracker_frame(
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
        frame
        .sort_values("End.absTotal")
        .reset_index(drop=True)
    )


def print_fold_report(
    tracker: str,
    candidate: str,
    folds: pd.DataFrame,
):
    print()
    print(candidate)

    for _, row in folds.iterrows():
        print(
            f"  fold {int(row['Fold'])}: "
            f"train={int(row['TrainRows'])} "
            f"test={int(row['TestRows'])} "
            f"improvement={row['ImprovementPct']:+.6f}% "
            f"corr={row['BaselineCorr']:+.6f}"
            f"->{row['CandidateCorr']:+.6f} "
            f"residual={row['ResidualCorr']:+.6f} "
            + f"partial={row['PartialCorr']:+.6f}"
        )


def main():
    args = parse_args()

    #
    # Quant2 intentionally absent.
    #
    # It is reserved as validation data and should not be
    # introduced while candidate methodology is still being
    # developed.
    #
    trackers = {
        "Crypto 3": "crypto3.tkr",
        "Random SD": "crypto_RandomSD.tkr",
        "Quant": "Quant.tkr",
        "Quant IDQE": "Quant_IDQE.tkr",
    }

    all_folds = []
    summaries = []

    for tracker_name, filename in trackers.items():
        tracker_path = (
            args.tracker_dir
            / filename
        )

        print()
        print("=" * 78)
        print(tracker_name)
        print(tracker_path)
        print("=" * 78)

        df = load_tracker(
            executable=args.farm,
            tracker_path=tracker_path,
            horizon=args.horizon,
        )

        for candidate in CANDIDATES:
            folds = walk_forward(
                df=df,
                candidate=candidate,
                lag=args.lag,
            )

            folds.insert(
                0,
                "Tracker",
                tracker_name,
            )

            all_folds.append(folds)

            print_fold_report(
                tracker_name,
                candidate,
                folds,
            )

            summaries.append(
                summarize_folds(
                    tracker_name,
                    candidate,
                    folds,
                )
            )

    fold_report = pd.concat(
        all_folds,
        ignore_index=True,
    )

    summary_report = pd.DataFrame(
        summaries
    )

    print()
    print("=" * 78)
    print("WALK-FORWARD SUMMARY")
    print("=" * 78)

    print(
        summary_report.to_string(
            index=False,
            float_format=lambda x: f"{x:.8g}",
        )
    )

    print()
    print("=" * 78)
    print("ALL FOLDS")
    print("=" * 78)

    fold_columns = [
        "Tracker",
        "Metric",
        "Fold",
        "Lag",
        "TrainRows",
        "TestRows",
        "TestBegin",
        "TestEnd",
        "BaselineMSE",
        "CandidateMSE",
        "ImprovementPct",
        "BaselineCorr",
        "CandidateCorr",
        "ResidualCorr",
        "PartialCorr",
        "StateCoefficient",
    ]

    print(
        fold_report[
            fold_columns
        ].to_string(
            index=False,
            float_format=lambda x: f"{x:.8g}",
        )
    )


if __name__ == "__main__":
    main()