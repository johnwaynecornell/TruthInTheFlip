from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
import pandas as pd

from Loader import load_tracker_frame

TARGET = "offset50#mean#BetSameWinRate"

CANDIDATES = [
    "BetSameGapTrend#4",
    "BetSameGapTrend#8",
]

#
# The evaluator requests full 10B segments from Farm.
#
BLOCK_FLIPS = 10_000_000_000

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


def decision_sign(values: np.ndarray) -> np.ndarray:
    #
    # Zero is deterministically treated as Same.
    # Exact zero should be extremely uncommon, but keeping
    # the rule explicit makes the simulation reproducible.
    #
    return np.where(values >= 0.0, 1.0, -1.0)

def accuracy_z(
    correct_flips: float,
    total_flips: int,
) -> float:
    if total_flips <= 0:
        return float("nan")

    expected = total_flips * 0.5
    sigma = np.sqrt(total_flips * 0.25)

    return float(
        (correct_flips - expected)
        / sigma
    )


def edge_to_correct_flips(
    realized_edges: np.ndarray,
    flips_per_block: int,
) -> tuple[float, int]:
    #
    # realized_edges are percentage-point offsets from 50.
    #
    # Example:
    #
    #   edge = +0.00015
    #   accuracy = 50.00015%
    #
    accuracy_fraction = (
        0.5
        + realized_edges / 100.0
    )

    correct_flips = float(
        np.sum(
            accuracy_fraction
            * flips_per_block
        )
    )

    total_flips = int(
        len(realized_edges)
        * flips_per_block
    )

    return correct_flips, total_flips

def decision_metrics(
        prediction: np.ndarray,
        actual_edge: np.ndarray,
) -> dict:
    predicted_side = decision_sign(prediction)
    actual_side = decision_sign(actual_edge)

    block_correct = (
            predicted_side == actual_side
    )

    #
    # TARGET is offset50#mean#BetSameWinRate.
    #
    # If we choose Same:
    #     realized edge = actual edge
    #
    # If we choose Different:
    #     realized edge = -actual edge
    #
    realized_edge = (
            predicted_side
            * actual_edge
    )

    correct_flips, total_flips = edge_to_correct_flips(
            realized_edge,
            BLOCK_FLIPS,
    )

    z = accuracy_z(
            correct_flips,
            total_flips,
    )

    return {
        "BlockAccuracy": float(
            np.mean(block_correct)
        ),

        "CorrectBlocks": int(
            np.sum(block_correct)
        ),

        "RealizedEdge": float(
            np.mean(realized_edge)
        ),

        "RealizedAccuracy": float(
            50.0 + np.mean(realized_edge)
        ),

        "CorrectFlips": correct_flips,
        "TotalFlips": total_flips,
        "Z": z,
    }


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

    #
    # Convert both strictly out-of-sample predictions into
    # causal Same/Different decisions.
    #
    baseline_decision = decision_metrics(
        baseline_prediction,
        actual,
    )

    candidate_decision = decision_metrics(
        candidate_prediction,
        actual,
    )

    block_accuracy_delta = (
            candidate_decision["BlockAccuracy"]
            - baseline_decision["BlockAccuracy"]
    )

    realized_accuracy_delta = (
            candidate_decision["RealizedAccuracy"]
            - baseline_decision["RealizedAccuracy"]
    )

    realized_edge_delta = (
            candidate_decision["RealizedEdge"]
            - baseline_decision["RealizedEdge"]
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

        "BaselineCorrectBlocks":
            baseline_decision["CorrectBlocks"],

        "CandidateCorrectBlocks":
            candidate_decision["CorrectBlocks"],

        "BaselineBlockAccuracy":
            baseline_decision["BlockAccuracy"],

        "CandidateBlockAccuracy":
            candidate_decision["BlockAccuracy"],

        "BlockAccuracyDelta":
            block_accuracy_delta,

        "BaselineRealizedEdge":
            baseline_decision["RealizedEdge"],

        "CandidateRealizedEdge":
            candidate_decision["RealizedEdge"],

        "RealizedEdgeDelta":
            realized_edge_delta,

        "BaselineRealizedAccuracy":
            baseline_decision["RealizedAccuracy"],

        "CandidateRealizedAccuracy":
            candidate_decision["RealizedAccuracy"],

        "RealizedAccuracyDelta":
            realized_accuracy_delta,

        "BaselineCorrectFlips":
                baseline_decision["CorrectFlips"],

        "CandidateCorrectFlips":
                candidate_decision["CorrectFlips"],

        "TotalFlips":
                candidate_decision["TotalFlips"],

        "BaselineZ":
                baseline_decision["Z"],

        "CandidateZ":
                candidate_decision["Z"],

        "StateCoefficient": float(
            candidate_coefficients[2]
        ),
    }

def cumulative_decision_metrics(
    folds: pd.DataFrame,
) -> dict:
    total_flips = int(
        folds["TotalFlips"].sum()
    )

    baseline_correct = float(
        folds["BaselineCorrectFlips"].sum()
    )

    candidate_correct = float(
        folds["CandidateCorrectFlips"].sum()
    )

    baseline_accuracy = (
        baseline_correct
        / total_flips
        * 100.0
    )

    candidate_accuracy = (
        candidate_correct
        / total_flips
        * 100.0
    )

    return {
        "TotalEvaluatedFlips": total_flips,

        "BaselineCorrectFlips":
            baseline_correct,

        "CandidateCorrectFlips":
            candidate_correct,

        "BaselineAccuracy":
            baseline_accuracy,

        "CandidateAccuracy":
            candidate_accuracy,

        "AccuracyDelta":
            candidate_accuracy
            - baseline_accuracy,

        "BaselineZ": accuracy_z(
            baseline_correct,
            total_flips,
        ),

        "CandidateZ": accuracy_z(
            candidate_correct,
            total_flips,
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
    block_deltas = folds["BlockAccuracyDelta"]
    accuracy_deltas = folds["RealizedAccuracyDelta"]
    edges = folds["CandidateRealizedEdge"]
    cumulative = cumulative_decision_metrics(folds)
    return {
        "Tracker": tracker,
        "Metric": candidate,

        "Folds": len(folds),
        "PositiveBlockDecisionFolds": int(
            (block_deltas > 0).sum()
        ),

        "PositiveRealizedFolds": int(
            (accuracy_deltas > 0).sum()
        ),

        "MeanBaselineBlockAccuracy": float(
            folds["BaselineBlockAccuracy"].mean()
        ),

        "MeanCandidateBlockAccuracy": float(
            folds["CandidateBlockAccuracy"].mean()
        ),

        "MeanBlockAccuracyDelta": float(
            block_deltas.mean()
        ),

        "MeanBaselineRealizedAccuracy": float(
            folds["BaselineRealizedAccuracy"].mean()
        ),

        "MeanCandidateRealizedAccuracy": float(
            folds["CandidateRealizedAccuracy"].mean()
        ),

        "MeanRealizedAccuracyDelta": float(
            accuracy_deltas.mean()
        ),

        "MedianRealizedAccuracyDelta": float(
            accuracy_deltas.median()
        ),

        "WorstRealizedAccuracyDelta": float(
            accuracy_deltas.min()
        ),

        "BestRealizedAccuracyDelta": float(
            accuracy_deltas.max()
        ),

        "CandidateAbove50Folds": int(
            (folds["CandidateRealizedAccuracy"] > 50.0).sum()
        ),


        "TotalEvaluatedFlips":
               cumulative["TotalEvaluatedFlips"],

        "CumulativeBaselineAccuracy":
               cumulative["BaselineAccuracy"],

        "CumulativeCandidateAccuracy":
                cumulative["CandidateAccuracy"],

       "CumulativeAccuracyDelta":
               cumulative["AccuracyDelta"],

        "CumulativeBaselineZ":
                cumulative["BaselineZ"],

        "CumulativeCandidateZ":
                cumulative["CandidateZ"],

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
            f"blocks={int(row['TestRows'])} "
            f"block={row['BaselineBlockAccuracy']:.4f}"
            f"->{row['CandidateBlockAccuracy']:.4f} "
            f"delta={row['RealizedAccuracyDelta']:+.8f}pp "
            f"Z={row['BaselineZ']:+.4f}"
            f"->{row['CandidateZ']:+.4f}"
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
        "BaselineCorrectBlocks",
        "CandidateCorrectBlocks",
        "BaselineBlockAccuracy",
        "CandidateBlockAccuracy",
        "BlockAccuracyDelta",
        "BaselineRealizedEdge",
        "CandidateRealizedEdge",
        "RealizedEdgeDelta",
        "BaselineRealizedAccuracy",
        "CandidateRealizedAccuracy",
        "RealizedAccuracyDelta",
        "BaselineCorrectFlips",
        "CandidateCorrectFlips",
        "TotalFlips",
        "BaselineZ",
        "CandidateZ",
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