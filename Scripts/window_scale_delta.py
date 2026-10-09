#!/usr/bin/env python3

import argparse
import itertools
import json
import math
import sys
from collections import Counter
from pathlib import Path

"""
TruthInTheFlip_Farm json zip \
  .expand. WS 10B 100B : \
    tracker window by_total WS file TRACKER.tkr \
  .expand_end. \
  .END. \
  item_0.absTotal \
  item_1.absTotal \
  sub#item_0.AnticipatedPercentage,item_1.AnticipatedPercentage \
  > /tmp/window_scale_delta.ndjson
"""

DEFAULT_FILE = "/tmp/window_scale_delta.ndjson"

DELTA_KEY = (
    "sub#item_0.AnticipatedPercentage,"
    "item_1.AnticipatedPercentage"
)

TOTAL_KEY_0 = "item_0.absTotal"
TOTAL_KEY_1 = "item_1.absTotal"


def load_ndjson(path):
    """
    Load observation totals and scale deltas from NDJSON.

    Validates:
      - Line-by-line JSON parsing
      - Alignment between item_0.absTotal and item_1.absTotal when both are present
      - Finite float delta values
      - Strictly monotonically increasing absTotal sequence
    """
    totals = []
    deltas = []

    with open(path, "r", encoding="utf-8") as f:
        for line_number, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue

            try:
                row = json.loads(line)
            except json.JSONDecodeError as e:
                raise ValueError(
                    f"Line {line_number}: invalid JSON: {e}"
                ) from e

            if TOTAL_KEY_0 not in row:
                raise KeyError(
                    f"Line {line_number}: missing expected field '{TOTAL_KEY_0}'"
                )

            total_0 = row[TOTAL_KEY_0]

            if TOTAL_KEY_1 in row:
                total_1 = row[TOTAL_KEY_1]
                if total_0 != total_1:
                    raise ValueError(
                        f"Line {line_number}: alignment violation: "
                        f"{TOTAL_KEY_0} ({total_0}) != {TOTAL_KEY_1} ({total_1})"
                    )

            if DELTA_KEY not in row:
                raise KeyError(
                    f"Line {line_number}: missing expected field '{DELTA_KEY}'"
                )

            delta_raw = row[DELTA_KEY]
            if delta_raw is None:
                continue

            delta = float(delta_raw)
            if not math.isfinite(delta):
                continue

            total = int(total_0)
            if totals and total <= totals[-1]:
                raise ValueError(
                    f"Line {line_number}: absTotal ({total}) is not strictly "
                    f"monotonically increasing (previous was {totals[-1]})"
                )

            totals.append(total)
            deltas.append(delta)

    return totals, deltas


def infer_record_cadence(totals):
    """
    Infer emitted-record cadence from absTotal differences.

    Returns the integer cadence if differences are positive and uniform,
    or None if non-uniform or insufficient data.
    """
    if len(totals) < 2:
        return None

    diffs = [b - a for a, b in zip(totals[:-1], totals[1:])]
    first_diff = diffs[0]

    if first_diff > 0 and all(d == first_diff for d in diffs):
        return first_diff

    return None


def sign_of(value):
    if value > 0:
        return 1
    if value < 0:
        return -1
    return 0


def cumulative_mean(values):
    result = []
    running = 0.0

    for i, value in enumerate(values, 1):
        running += value
        result.append(running / i)

    return result


def rolling_mean(values, window):
    result = [math.nan] * len(values)

    if not values:
        return result

    running = 0.0

    for i, value in enumerate(values):
        running += value

        if i >= window:
            running -= values[i - window]

        if i >= window - 1:
            result[i] = running / window

    return result


def cumulative_signed_area(values):
    positive = []
    negative = []

    pos = 0.0
    neg = 0.0

    for value in values:
        if value > 0:
            pos += value
        elif value < 0:
            neg += value

        positive.append(pos)
        negative.append(neg)

    return positive, negative


def sign_runs(totals, deltas):
    runs = []

    current_sign = None
    start = None
    area = 0.0
    peak = None

    def finish(end_index):
        nonlocal current_sign, start, area, peak

        if current_sign is None:
            return

        runs.append(
            {
                "sign": current_sign,
                "start_index": start,
                "end_index": end_index,
                "length": end_index - start + 1,
                "start_total": totals[start],
                "end_total": totals[end_index],
                "area": area,
                "peak": peak,
            }
        )

        current_sign = None
        start = None
        area = 0.0
        peak = None

    for i, value in enumerate(deltas):
        sign = sign_of(value)

        if sign == 0:
            finish(i - 1)
            continue

        if current_sign is None:
            current_sign = sign
            start = i
            area = value
            peak = value
            continue

        if sign != current_sign:
            finish(i - 1)

            current_sign = sign
            start = i
            area = value
            peak = value
            continue

        area += value

        if current_sign > 0:
            peak = max(peak, value)
        else:
            peak = min(peak, value)

    finish(len(deltas) - 1)

    return runs


def autocorrelation(values, max_lag=2000):
    """
    Conventional normalized sample autocorrelation.

    Lag 0 is exactly 1.
    """
    n = len(values)

    if n == 0:
        return []

    max_lag = min(max_lag, n - 1)

    try:
        import numpy as np

        v = np.asarray(values, dtype=np.float64)
        mean = np.mean(v)
        centered = v - mean
        denominator = np.sum(centered * centered)

        if denominator == 0:
            return [(lag, math.nan) for lag in range(max_lag + 1)]

        result = [(0, 1.0)]
        for lag in range(1, max_lag + 1):
            numerator = np.dot(centered[:-lag], centered[lag:])
            result.append((lag, float(numerator / denominator)))

        return result
    except ImportError:
        mean = sum(values) / n
        centered = [x - mean for x in values]
        denominator = sum(x * x for x in centered)

        if denominator == 0:
            return [(lag, math.nan) for lag in range(max_lag + 1)]

        result = []
        for lag in range(max_lag + 1):
            numerator = 0.0
            for i in range(n - lag):
                numerator += centered[i] * centered[i + lag]
            result.append((lag, numerator / denominator))

        return result


def first_lag_below(acf, threshold):
    for lag, value in acf:
        if lag == 0 or not math.isfinite(value):
            continue

        if value < threshold:
            return lag, value

    return None


def first_zero_crossing(acf):
    previous = None

    for lag, value in acf:
        if lag == 0 or not math.isfinite(value):
            continue

        if previous is not None and previous > 0 and value <= 0:
            return lag, value

        previous = value

    return None


def _format_run_summary(run):
    if run is None:
        return None
    return {
        "length": run["length"],
        "startAbsTotal": run["start_total"],
        "endAbsTotal": run["end_total"],
        "area": run["area"],
    }


def summarize(totals, deltas, acf):
    """
    Produce unified analytical summary metrics over totals, deltas, and acf.

    Returns a dict containing:
      - 'summary': dictionary of frozen summary metrics suitable for JSON reporting
      - Supporting data structures for human reports and plots.
    """
    n = len(deltas)
    if n == 0:
        empty_summary = {
            "Observations": 0,
            "BeginAbsTotal": None,
            "EndAbsTotal": None,
            "RecordCadence": None,
            "InitialZeros": 0,
            "FirstDivergenceAbsTotal": None,
            "PositivePoints": 0,
            "NegativePoints": 0,
            "ZeroPoints": 0,
            "PctDeltaAbove0": None,
            "MeanDelta": None,
            "MaxDelta": None,
            "MaxDeltaAbsTotal": None,
            "MinDelta": None,
            "MinDeltaAbsTotal": None,
            "PositiveArea": 0.0,
            "NegativeArea": 0.0,
            "NetArea": 0.0,
            "MaxCumulativeArea": None,
            "MaxCumulativeAreaAbsTotal": None,
            "MinCumulativeArea": None,
            "MinCumulativeAreaAbsTotal": None,
            "MaxAbsCumulativeArea": None,
            "MaxAbsCumulativeAreaAbsTotal": None,
            "RunCount": 0,
            "LongestPositiveRun": None,
            "LongestNegativeRun": None,
            "LargestPositiveRunArea": None,
            "LargestNegativeRunArea": None,
            "ACF1": None,
            "ACFHalfLag": None,
            "ACFHalfCoordinateSpan": None,
            "ACFZeroCrossingLag": None,
            "ACFZeroCrossingCoordinateSpan": None,
        }
        return {
            "summary": empty_summary,
            "runs": [],
            "positive_runs": [],
            "negative_runs": [],
            "ten_longest_runs": [],
            "ten_largest_area_runs": [],
            "positive_area_series": [],
            "negative_area_series": [],
            "cumulative_area": [],
            "cumulative_mean": [],
        }

    cadence = infer_record_cadence(totals)

    signs = [sign_of(x) for x in deltas]
    counts = Counter(signs)

    initial_zero_count = 0
    for value in deltas:
        if value == 0:
            initial_zero_count += 1
        else:
            break

    first_divergence_total = (
        totals[initial_zero_count] if initial_zero_count < n else None
    )

    nonzero_points = counts[1] + counts[-1]
    pct_above_0 = (
        (counts[1] / nonzero_points * 100.0) if nonzero_points > 0 else 0.0
    )

    max_value = max(deltas)
    min_value = min(deltas)
    max_index = deltas.index(max_value)
    min_index = deltas.index(min_value)

    mean = sum(deltas) / n

    total_positive_area = sum(x for x in deltas if x > 0)
    total_negative_area = sum(x for x in deltas if x < 0)
    net_area = total_positive_area + total_negative_area

    # Cumulative area series
    cum_area = list(itertools.accumulate(deltas))
    max_cum_area = max(cum_area)
    min_cum_area = min(cum_area)
    max_cum_idx = cum_area.index(max_cum_area)
    min_cum_idx = cum_area.index(min_cum_area)

    # Maximum absolute cumulative excursion (preserving signed value)
    abs_max_idx = max(range(n), key=lambda i: abs(cum_area[i]))
    max_abs_cum_area = abs(cum_area[abs_max_idx])
    max_abs_cum_abs_total = totals[abs_max_idx]

    # Sign runs
    runs = sign_runs(totals, deltas)
    positive_runs = [r for r in runs if r["sign"] > 0]
    negative_runs = [r for r in runs if r["sign"] < 0]

    longest_positive = max(
        positive_runs,
        key=lambda r: r["length"],
        default=None,
    )
    longest_negative = max(
        negative_runs,
        key=lambda r: r["length"],
        default=None,
    )
    largest_positive_area = max(
        positive_runs,
        key=lambda r: r["area"],
        default=None,
    )
    largest_negative_area = min(
        negative_runs,
        key=lambda r: r["area"],
        default=None,
    )

    ten_longest = sorted(
        runs,
        key=lambda r: r["length"],
        reverse=True,
    )[:10]

    ten_largest_area = sorted(
        runs,
        key=lambda r: abs(r["area"]),
        reverse=True,
    )[:10]

    # ACF metrics
    acf_1 = acf[1][1] if len(acf) > 1 else None

    lag_05 = first_lag_below(acf, 0.5)
    acf_half_lag = lag_05[0] if lag_05 else None
    acf_half_val = lag_05[1] if lag_05 else None
    acf_half_span = (
        (acf_half_lag * cadence)
        if (acf_half_lag is not None and cadence is not None)
        else None
    )

    zero_cross = first_zero_crossing(acf)
    acf_zero_lag = zero_cross[0] if zero_cross else None
    acf_zero_val = zero_cross[1] if zero_cross else None
    acf_zero_span = (
        (acf_zero_lag * cadence)
        if (acf_zero_lag is not None and cadence is not None)
        else None
    )

    pos_area_series, neg_area_series = cumulative_signed_area(deltas)
    cum_mean_series = cumulative_mean(deltas)

    summary = {
        "Observations": n,
        "BeginAbsTotal": totals[0],
        "EndAbsTotal": totals[-1],
        "RecordCadence": cadence,
        "InitialZeros": initial_zero_count,
        "FirstDivergenceAbsTotal": first_divergence_total,
        "PositivePoints": counts[1],
        "NegativePoints": counts[-1],
        "ZeroPoints": counts[0],
        "PctDeltaAbove0": pct_above_0,
        "MeanDelta": mean,
        "MaxDelta": max_value,
        "MaxDeltaAbsTotal": totals[max_index],
        "MinDelta": min_value,
        "MinDeltaAbsTotal": totals[min_index],
        "PositiveArea": total_positive_area,
        "NegativeArea": total_negative_area,
        "NetArea": net_area,
        "MaxCumulativeArea": max_cum_area,
        "MaxCumulativeAreaAbsTotal": totals[max_cum_idx],
        "MinCumulativeArea": min_cum_area,
        "MinCumulativeAreaAbsTotal": totals[min_cum_idx],
        "MaxAbsCumulativeArea": max_abs_cum_area,
        "MaxAbsCumulativeAreaAbsTotal": max_abs_cum_abs_total,
        "RunCount": len(runs),
        "LongestPositiveRun": _format_run_summary(longest_positive),
        "LongestNegativeRun": _format_run_summary(longest_negative),
        "LargestPositiveRunArea": _format_run_summary(largest_positive_area),
        "LargestNegativeRunArea": _format_run_summary(largest_negative_area),
        "ACF1": acf_1,
        "ACFHalfLag": acf_half_lag,
        "ACFHalfCoordinateSpan": acf_half_span,
        "ACFZeroCrossingLag": acf_zero_lag,
        "ACFZeroCrossingCoordinateSpan": acf_zero_span,
    }

    return {
        "summary": summary,
        "runs": runs,
        "positive_runs": positive_runs,
        "negative_runs": negative_runs,
        "longest_positive": longest_positive,
        "longest_negative": longest_negative,
        "largest_positive_area": largest_positive_area,
        "largest_negative_area": largest_negative_area,
        "ten_longest_runs": ten_longest,
        "ten_largest_area_runs": ten_largest_area,
        "acf_half": (acf_half_lag, acf_half_val) if lag_05 else None,
        "acf_zero_crossing": (acf_zero_lag, acf_zero_val) if zero_cross else None,
        "positive_area_series": pos_area_series,
        "negative_area_series": neg_area_series,
        "cumulative_area": cum_area,
        "cumulative_mean": cum_mean_series,
    }


def describe(metrics, file=sys.stdout):
    """
    Format and print the human-readable summary report.
    """
    s = metrics["summary"]

    if s["Observations"] == 0:
        print("No usable observations.", file=file)
        return

    print(f"Observations:       {s['Observations']:,}", file=file)
    print(f"Begin absTotal:     {s['BeginAbsTotal']:,}", file=file)
    print(f"End absTotal:       {s['EndAbsTotal']:,}", file=file)

    if s["RecordCadence"] is not None:
        print(f"Record cadence:     {s['RecordCadence']:,}", file=file)
    else:
        print("Record cadence:     non-uniform", file=file)

    print(file=file)

    print(f"Initial zeros:      {s['InitialZeros']:,}", file=file)
    if s["FirstDivergenceAbsTotal"] is not None:
        print(
            "First divergence:   "
            f"absTotal {s['FirstDivergenceAbsTotal']:,}",
            file=file,
        )

    print(file=file)
    print(f"Positive points:    {s['PositivePoints']:,}", file=file)
    print(f"Negative points:    {s['NegativePoints']:,}", file=file)
    print(f"Zero points:        {s['ZeroPoints']:,}", file=file)
    print(f"Pct delta > 0:      {s['PctDeltaAbove0']:.6f}%", file=file)
    print(f"Mean delta:         {s['MeanDelta']:.15g}", file=file)

    print(file=file)
    print(
        f"Maximum delta:      {s['MaxDelta']:.15g} "
        f"at absTotal {s['MaxDeltaAbsTotal']:,}",
        file=file,
    )
    print(
        f"Minimum delta:      {s['MinDelta']:.15g} "
        f"at absTotal {s['MinDeltaAbsTotal']:,}",
        file=file,
    )

    print(file=file)
    print(f"Positive area:      {s['PositiveArea']:.15g}", file=file)
    print(f"Negative area:      {s['NegativeArea']:.15g}", file=file)
    print(f"Net area:           {s['NetArea']:.15g}", file=file)

    print(file=file)
    print(
        f"Max cumulative area: {s['MaxCumulativeArea']:.15g} "
        f"at absTotal {s['MaxCumulativeAreaAbsTotal']:,}",
        file=file,
    )
    print(
        f"Min cumulative area: {s['MinCumulativeArea']:.15g} "
        f"at absTotal {s['MinCumulativeAreaAbsTotal']:,}",
        file=file,
    )
    print(
        f"Max |cumulative|:    {s['MaxAbsCumulativeArea']:.15g} "
        f"at absTotal {s['MaxAbsCumulativeAreaAbsTotal']:,}",
        file=file,
    )

    print(file=file)
    lp = metrics.get("longest_positive")
    if lp:
        print(
            "Longest + run:      "
            f"{lp['length']:,} records "
            f"({lp['start_total']:,} -> "
            f"{lp['end_total']:,}) "
            f"area={lp['area']:.12g}",
            file=file,
        )

    ln = metrics.get("longest_negative")
    if ln:
        print(
            "Longest - run:      "
            f"{ln['length']:,} records "
            f"({ln['start_total']:,} -> "
            f"{ln['end_total']:,}) "
            f"area={ln['area']:.12g}",
            file=file,
        )

    lpa = metrics.get("largest_positive_area")
    if lpa:
        print(
            "Largest + area:     "
            f"{lpa['area']:.12g} "
            f"over {lpa['length']:,} records "
            f"({lpa['start_total']:,} -> "
            f"{lpa['end_total']:,})",
            file=file,
        )

    lna = metrics.get("largest_negative_area")
    if lna:
        print(
            "Largest - area:     "
            f"{lna['area']:.12g} "
            f"over {lna['length']:,} records "
            f"({lna['start_total']:,} -> "
            f"{lna['end_total']:,})",
            file=file,
        )

    print(file=file)
    print(f"Total nonzero runs: {s['RunCount']:,}", file=file)

    print(file=file)
    if s["ACF1"] is not None:
        print(f"ACF lag 1:          {s['ACF1']:.9f}", file=file)

    acf_half = metrics.get("acf_half")
    if acf_half:
        lag, value = acf_half
        print(
            f"ACF below 0.5:      lag {lag:,} "
            f"(acf={value:.6f})",
            file=file,
        )
        if s["ACFHalfCoordinateSpan"] is not None:
            print(
                "                     coordinate span "
                f"≈ {s['ACFHalfCoordinateSpan']:,}",
                file=file,
            )
        else:
            print(
                "                     coordinate span: non-uniform cadence",
                file=file,
            )
    else:
        print("ACF below 0.5:      not reached", file=file)

    acf_zero = metrics.get("acf_zero_crossing")
    if acf_zero:
        lag, value = acf_zero
        print(
            f"ACF zero crossing:  lag {lag:,} "
            f"(acf={value:.6f})",
            file=file,
        )
        if s["ACFZeroCrossingCoordinateSpan"] is not None:
            print(
                "                     coordinate span "
                f"≈ {s['ACFZeroCrossingCoordinateSpan']:,}",
                file=file,
            )
        else:
            print(
                "                     coordinate span: non-uniform cadence",
                file=file,
            )
    else:
        print("ACF zero crossing:  not reached", file=file)

    print(file=file)
    print("Ten longest sign runs:", file=file)

    for run in metrics["ten_longest_runs"]:
        sign = "+" if run["sign"] > 0 else "-"
        print(
            f"  {sign} {run['length']:6,d} records  "
            f"area={run['area']: .10g}  "
            f"{run['start_total']:15,d} -> "
            f"{run['end_total']:15,d}",
            file=file,
        )

    print(file=file)
    print("Ten largest runs by |area|:", file=file)

    for run in metrics["ten_largest_area_runs"]:
        sign = "+" if run["sign"] > 0 else "-"
        print(
            f"  {sign} area={run['area']: .10g}  "
            f"{run['length']:6,d} records  "
            f"{run['start_total']:15,d} -> "
            f"{run['end_total']:15,d}",
            file=file,
        )


def plot_series(totals, deltas, acf, metrics):
    """
    Generate plots from series and summary metrics.
    """
    import matplotlib.pyplot as plt

    cumulative = metrics["cumulative_mean"]
    positive_area = metrics["positive_area_series"]
    negative_area = metrics["negative_area_series"]
    net_area = metrics["cumulative_area"]

    rolling_windows = [10, 50, 500]

    plt.figure()
    plt.plot(totals, deltas)
    plt.axhline(0)
    plt.xlabel("absTotal")
    plt.ylabel("10B - 100B AnticipatedPercentage")
    plt.title("Scale Delta")
    plt.tight_layout()

    plt.figure()
    plt.plot(totals, cumulative)
    plt.axhline(0)
    plt.xlabel("absTotal")
    plt.ylabel("Cumulative mean delta")
    plt.title("Cumulative Cancellation")
    plt.tight_layout()

    for window in rolling_windows:
        values = rolling_mean(deltas, window)

        plt.figure()
        plt.plot(totals, values)
        plt.axhline(0)
        plt.xlabel("absTotal")
        plt.ylabel(f"Rolling mean delta ({window} records)")
        plt.title(f"Scale Delta — Rolling Mean ({window})")
        plt.tight_layout()

    plt.figure()

    lags = [lag for lag, _ in acf]
    values = [value for _, value in acf]

    plt.plot(lags, values)
    plt.axhline(0)
    plt.axhline(0.5)
    plt.xlabel("Lag (records)")
    plt.ylabel("Autocorrelation")
    plt.title("Scale Delta Autocorrelation")
    plt.tight_layout()

    plt.figure()
    plt.plot(totals, positive_area, label="Positive area")
    plt.plot(totals, negative_area, label="Negative area")
    plt.xlabel("absTotal")
    plt.ylabel("Cumulative delta area")
    plt.title("Positive and Negative Cumulative Area")
    plt.legend()
    plt.tight_layout()

    plt.figure()
    plt.plot(totals, net_area, label="Net cumulative area")
    plt.axhline(0, color="gray", linestyle="--", alpha=0.7)

    s = metrics["summary"]
    if s["MaxCumulativeArea"] is not None and s["MinCumulativeArea"] is not None:
        plt.plot(
            s["MaxCumulativeAreaAbsTotal"],
            s["MaxCumulativeArea"],
            "ro",
            label=f"Max excursion ({s['MaxCumulativeArea']:.4g})",
        )
        plt.plot(
            s["MinCumulativeAreaAbsTotal"],
            s["MinCumulativeArea"],
            "bo",
            label=f"Min excursion ({s['MinCumulativeArea']:.4g})",
        )

    plt.xlabel("absTotal")
    plt.ylabel("Cumulative signed delta")
    plt.title("Net Cumulative Scale-Delta Area")
    plt.legend()
    plt.tight_layout()

    plt.show()


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Scale-delta analyzer for rolling-window tracker comparisons."
    )
    parser.add_argument(
        "path",
        nargs="?",
        default=DEFAULT_FILE,
        help=f"Path to input NDJSON file (default: {DEFAULT_FILE})",
    )
    parser.add_argument(
        "--summary-json",
        action="store_true",
        help="Emit frozen summary metrics as a single JSON object to stdout.",
    )
    parser.add_argument(
        "--no-plot",
        action="store_true",
        help="Do not display interactive plot windows.",
    )

    args = parser.parse_args(argv)

    input_path = Path(args.path)
    if not input_path.exists():
        print(f"Error: input file '{input_path}' not found.", file=sys.stderr)
        sys.exit(1)

    try:
        totals, deltas = load_ndjson(input_path)
    except Exception as e:
        print(f"Error reading '{input_path}': {e}", file=sys.stderr)
        sys.exit(1)

    acf = autocorrelation(deltas, max_lag=2000)
    metrics = summarize(totals, deltas, acf)

    if args.summary_json:
        print(json.dumps(metrics["summary"], indent=2))
        return

    describe(metrics)

    if not args.no_plot:
        plot_series(totals, deltas, acf, metrics)


if __name__ == "__main__":
    main()
