#!/usr/bin/env python3

import argparse
import csv
import json
import math
import statistics
import sys
from pathlib import Path

"""
TruthInTheFlip_Farm json zip \
  .expand. WS 10B 100B : \
    tracker window by_total WS file Artifacts/Trackers/SamePersistence.NET1.rep2.tkr \
  .expand_end. \
  .END. \
  item_0.absTotal \
  item_1.absTotal \
  sub#item_0.AnticipatedPercentage,item_1.AnticipatedPercentage \
  > /tmp/samepersistence_rep2_scale_delta.ndjson
  
python Scripts/window_scale_delta.py \
  /tmp/samepersistence_rep2_scale_delta.ndjson \
  --summary-json \
  > /tmp/samepersistence_rep2_scale_delta.summary.json
  
python3 Scripts/scale_delta_null_runner.py   --trials 100 \
   --base-seed 12345 \
    --tracker Artifacts/Trackers/SamePersistence.NET1.rep2.tkr \
    --strategy-window 10B   \
    --output /tmp/scale_delta_null_100.ndjson
  
python Scripts/scale_delta_null_compare.py \
  /tmp/scale_delta_null_100.ndjson \
  /tmp/samepersistence_rep2_scale_delta.summary.json  
"""

# Frozen scale-delta comparison vector.
# Paths use "." only to descend into nested analyzer objects.
METRICS = [
    ("PctDeltaAbove0", "Pct delta > 0"),
    ("MeanDelta", "Mean delta"),
    ("NetArea", "Net area"),
    ("MaxCumulativeArea", "Max cumulative area"),
    ("MinCumulativeArea", "Min cumulative area"),
    ("MaxAbsCumulativeArea", "Max |cumulative|"),
    ("RunCount", "Run count"),
    ("LongestPositiveRun.length", "Longest + run"),
    ("LongestNegativeRun.length", "Longest - run"),
    ("LargestPositiveRunArea.area", "Largest + run area"),
    ("LargestNegativeRunArea.area", "Largest - run area"),
    ("ACF1", "ACF lag 1"),
    ("ACFHalfLag", "ACF half lag"),
    ("ACFZeroCrossingLag", "ACF zero-crossing lag"),
]

SECONDARY_METRICS = [
    ("MaxDelta", "Maximum delta"),
    ("MinDelta", "Minimum delta"),
]

GEOMETRY_FIELDS = [
    "Observations",
    "BeginAbsTotal",
    "EndAbsTotal",
    "RecordCadence",
    "InitialZeros",
    "FirstDivergenceAbsTotal",
]


def load_ndjson(path):
    rows = []
    seen_trials = set()

    with open(path, "r", encoding="utf-8") as f:
        for line_number, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue

            try:
                row = json.loads(line)
            except json.JSONDecodeError as e:
                raise ValueError(
                    f"{path}:{line_number}: invalid JSON: {e}"
                ) from e

            if not isinstance(row, dict):
                raise ValueError(
                    f"{path}:{line_number}: expected JSON object"
                )

            if "TrialIndex" in row:
                trial = row["TrialIndex"]
                if trial in seen_trials:
                    raise ValueError(
                        f"{path}:{line_number}: duplicate TrialIndex {trial}"
                    )
                seen_trials.add(trial)

            rows.append(row)

    if not rows:
        raise ValueError(f"{path}: no trial records")

    return rows


def load_json_object(path):
    with open(path, "r", encoding="utf-8") as f:
        value = json.load(f)

    # Accept either the analyzer's direct summary object or
    # {"summary": {...}} for convenience.
    if isinstance(value, dict) and isinstance(value.get("summary"), dict):
        value = value["summary"]

    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected a JSON object")

    return value


def get_path(obj, path):
    current = obj
    for part in path.split("."):
        if not isinstance(current, dict) or part not in current:
            raise KeyError(f"missing metric '{path}'")
        current = current[part]

    if current is None:
        raise ValueError(f"metric '{path}' is null")

    if not isinstance(current, (int, float)) or isinstance(current, bool):
        raise TypeError(
            f"metric '{path}' must be numeric, got {type(current).__name__}"
        )

    value = float(current)
    if not math.isfinite(value):
        raise ValueError(f"metric '{path}' is non-finite")

    return value


def quantile(values, q):
    """
    Linear sample quantile using the common (n - 1) * q interpolation rule.
    This matches NumPy's default 'linear' quantile convention.
    """
    if not 0.0 <= q <= 1.0:
        raise ValueError("q must be between 0 and 1")

    ordered = sorted(values)
    n = len(ordered)

    if n == 1:
        return ordered[0]

    position = (n - 1) * q
    lo = math.floor(position)
    hi = math.ceil(position)

    if lo == hi:
        return ordered[lo]

    fraction = position - lo
    return ordered[lo] * (1.0 - fraction) + ordered[hi] * fraction


def percentile_rank(values, observed):
    """
    Mid-rank empirical percentile:
        100 * (count(x < observed) + 0.5 * count(x == observed)) / n

    Mid-rank handling is useful for discrete metrics such as ACF lags.
    """
    below = sum(1 for value in values if value < observed)
    equal = sum(1 for value in values if value == observed)
    return 100.0 * (below + 0.5 * equal) / len(values)


def summarize_metric(rows, observed, path, label):
    values = [get_path(row, path) for row in rows]
    obs = get_path(observed, path)

    return {
        "Metric": path,
        "Label": label,
        "Observed": obs,
        "NullMean": statistics.fmean(values),
        "NullMedian": statistics.median(values),
        "NullMin": min(values),
        "P05": quantile(values, 0.05),
        "P25": quantile(values, 0.25),
        "P75": quantile(values, 0.75),
        "P95": quantile(values, 0.95),
        "NullMax": max(values),
        "ObservedPercentile": percentile_rank(values, obs),
    }


def geometry_report(rows, observed):
    result = {}

    for field in GEOMETRY_FIELDS:
        values = []
        for row in rows:
            if field not in row:
                raise KeyError(f"null population missing geometry field '{field}'")
            values.append(row[field])

        unique = []
        for value in values:
            if value not in unique:
                unique.append(value)

        observed_value = observed.get(field)

        result[field] = {
            "NullUniform": len(unique) == 1,
            "NullValue": unique[0] if len(unique) == 1 else None,
            "NullDistinctValues": unique if len(unique) != 1 else None,
            "Observed": observed_value,
            "MatchesObserved": (
                len(unique) == 1
                and observed_value is not None
                and unique[0] == observed_value
            ),
        }

    return result


def fmt(value):
    if value is None:
        return "-"

    value = float(value)
    av = abs(value)

    if av != 0 and (av < 1e-4 or av >= 1e7):
        return f"{value:.6e}"

    if value.is_integer() and av < 1e12:
        return f"{int(value):,}"

    return f"{value:.9g}"


def print_human(result):
    print(f"Null trials: {result['TrialCount']}")
    print()

    geometry = result["Geometry"]
    geometry_ok = all(
        item["NullUniform"] and item["MatchesObserved"]
        for item in geometry.values()
    )

    print(
        "Geometry:    "
        + ("all frozen structural invariants match"
           if geometry_ok
           else "CHECK REQUIRED")
    )

    if not geometry_ok:
        for field, item in geometry.items():
            if not item["NullUniform"] or not item["MatchesObserved"]:
                print(
                    f"  {field}: observed={item['Observed']!r} "
                    f"null={item['NullValue']!r} "
                    f"uniform={item['NullUniform']}"
                )

    print()
    print(
        f"{'Metric':<26} {'Observed':>14} {'Mean':>14} {'Median':>14} "
        f"{'P05':>14} {'P25':>14} {'P75':>14} {'P95':>14} {'Pctile':>9}"
    )
    print("-" * 137)

    for row in result["Metrics"]:
        print(
            f"{row['Label']:<26} "
            f"{fmt(row['Observed']):>14} "
            f"{fmt(row['NullMean']):>14} "
            f"{fmt(row['NullMedian']):>14} "
            f"{fmt(row['P05']):>14} "
            f"{fmt(row['P25']):>14} "
            f"{fmt(row['P75']):>14} "
            f"{fmt(row['P95']):>14} "
            f"{row['ObservedPercentile']:8.2f}%"
        )


def write_csv(result, file):
    fieldnames = [
        "Metric",
        "Label",
        "Observed",
        "NullMean",
        "NullMedian",
        "NullMin",
        "P05",
        "P25",
        "P75",
        "P95",
        "NullMax",
        "ObservedPercentile",
    ]

    writer = csv.DictWriter(file, fieldnames=fieldnames)
    writer.writeheader()
    writer.writerows(result["Metrics"])


def main(argv=None):
    parser = argparse.ArgumentParser(
        description=(
            "Compare one frozen window-scale-delta observation against "
            "an empirical null population."
        )
    )
    parser.add_argument(
        "null_population",
        type=Path,
        help="NDJSON produced by scale_delta_null_runner.py",
    )
    parser.add_argument(
        "observed_summary",
        type=Path,
        help=(
            "JSON produced by window_scale_delta.py --summary-json "
            "for the observed tracker"
        ),
    )
    parser.add_argument(
        "--include-secondary",
        action="store_true",
        help="Also include MaxDelta and MinDelta.",
    )

    mode = parser.add_mutually_exclusive_group()
    mode.add_argument(
        "--json",
        action="store_true",
        help="Emit one machine-readable JSON comparison object.",
    )
    mode.add_argument(
        "--csv",
        action="store_true",
        help="Emit comparison rows as CSV.",
    )

    args = parser.parse_args(argv)

    try:
        rows = load_ndjson(args.null_population)
        observed = load_json_object(args.observed_summary)

        metric_specs = list(METRICS)
        if args.include_secondary:
            metric_specs.extend(SECONDARY_METRICS)

        result = {
            "TrialCount": len(rows),
            "NullPopulation": str(args.null_population),
            "ObservedSummary": str(args.observed_summary),
            "Geometry": geometry_report(rows, observed),
            "Metrics": [
                summarize_metric(rows, observed, path, label)
                for path, label in metric_specs
            ],
        }

    except (OSError, ValueError, TypeError, KeyError, json.JSONDecodeError) as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1

    if args.json:
        json.dump(result, sys.stdout, indent=2)
        print()
    elif args.csv:
        write_csv(result, sys.stdout)
    else:
        print_human(result)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
