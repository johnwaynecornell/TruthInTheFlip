from __future__ import annotations

import io
import subprocess
from pathlib import Path

import pandas as pd


def load_report(executable: str, arguments: list[str]) -> pd.DataFrame:
    result = subprocess.run(
        [executable, *arguments],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        check=False,
    )

    if result.returncode != 0:
        raise RuntimeError(
            result.stderr or
            f"TruthInTheFlip_Farm exited with code {result.returncode}"
        )

    return pd.read_csv(io.StringIO(result.stdout))


def divide_horizon(start: int, end: int, segment_count: int):
    span = end - start

    return [
        (
            start + span * i // segment_count,
            start + span * (i + 1) // segment_count,
        )
        for i in range(segment_count)
    ]

def tracker_selector(
    tracker_modifier: list[str],
    tracker: Path,
    boundary_type: str,
    begin,
    end,
) -> list[str]:
    return [
        "from",
        boundary_type,
        str(begin),

        "to",
        boundary_type,
        str(end),

        *tracker_modifier,

        "file",
        str(tracker),
    ]


def query_region(
    executable: str,
    tracker_modifier: list[str],
    tracker: Path,
    boundary_type: str,
    begin,
    end,
    process: str,
    fields: list[str],
    process_arguments: list[str],
) -> pd.DataFrame:
    selector = tracker_selector(
        tracker_modifier,
        tracker,
        boundary_type,
        begin,
        end,
    )

    return load_report(
        executable,
        [
            "csv",
            process,
            *selector,
            *process_arguments,
            *fields,
        ],
    )


def query_horizon_segments(
    executable: str,
    tracker: Path,
    tracker_modifier: list[str],
    horizon_start: int,
    horizon_end: int,
    segment_count: int,
    process: str,
    fields: list[str],
    process_arguments: list[str],
    boundary_type: str = "absTotal",


) -> pd.DataFrame:
    regions = divide_horizon(
        horizon_start,
        horizon_end,
        segment_count,
    )

    frames = []

    for segment_index, (begin, end) in enumerate(regions):
        frame = query_region(
            executable=executable,
            tracker=tracker,
            tracker_modifier=tracker_modifier,
            boundary_type=boundary_type,
            begin=begin,
            end=end,
            process=process,
            process_arguments=process_arguments,
            fields=fields,
        )

        frame.insert(0, "Region", segment_index)
        frame.insert(1, "RegionBegin", begin)
        frame.insert(2, "RegionEnd", end)

        frames.append(frame)

    if not frames:
        return pd.DataFrame()

    return pd.concat(
        frames,
        ignore_index=True,
    )