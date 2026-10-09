#!/usr/bin/env python3
"""
Unit tests for scale_delta_null_runner.py
"""

import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

# Add Scripts to sys.path
scripts_dir = Path(__file__).resolve().parent
if str(scripts_dir) not in sys.path:
    sys.path.insert(0, str(scripts_dir))

from scale_delta_null_runner import (
    derive_trial_seed,
    splitmix64,
    load_completed_trials,
    build_farm_command,
    append_trial_record,
    write_metadata,
    validate_metadata,
    parse_args,
    find_farm_executable,
    find_analyzer_script,
)


class TestScaleDeltaNullRunner(unittest.TestCase):
    def test_splitmix64_basic(self):
        # Known SplitMix64 behaviors
        self.assertEqual(splitmix64(0), 16294208416658607535)

    def test_derive_trial_seed_golden_values(self):
        """
        Verify seed derivation matches C# NullTrialProcess.DeriveTrialSeed exactly.
        """
        # Trial 0 always returns base_seed directly
        self.assertEqual(derive_trial_seed(12345, 0), 12345)
        self.assertEqual(derive_trial_seed(20260925, 0), 20260925)
        self.assertEqual(derive_trial_seed(0, 0), 0)

        # Golden values computed from C# SplitMix64 / NullTrialProcess.DeriveTrialSeed
        golden_12345 = {
            0: 12345,
            1: 6049483646029349647,
            2: 219170649146347626,
            3: 3640559280666499587,
            4: 8514055225357022816,
        }
        for trial_idx, expected_seed in golden_12345.items():
            self.assertEqual(derive_trial_seed(12345, trial_idx), expected_seed)

        golden_20260925 = {
            0: 20260925,
            1: 13636450386094472426,
            2: 13199718855106048224,
            3: 15683506176519872913,
            4: 7721196546938000937,
        }
        for trial_idx, expected_seed in golden_20260925.items():
            self.assertEqual(derive_trial_seed(20260925, trial_idx), expected_seed)

        golden_zero = {
            0: 0,
            1: 5095610196844313600,
            2: 17160774760686499100,
            3: 5629846650018757432,
            4: 5085904676777434204,
        }
        for trial_idx, expected_seed in golden_zero.items():
            self.assertEqual(derive_trial_seed(0, trial_idx), expected_seed)

    def test_build_farm_command(self):
        cmd = build_farm_command(
            farm_bin="/path/to/Farm",
            tracker="Artifacts/Trackers/SamePersistence.NET1.rep2.tkr",
            strategy_window="10B",
            seed=12345,
            obs_windows=["10B", "100B"],
        )
        expected = [
            "/path/to/Farm",
            "json",
            "zip",
            ".expand.",
            "WS",
            "10B",
            "100B",
            ":",
            "tracker",
            "window",
            "by_total",
            "WS",
            "synthetic",
            "12345",
            "same_persistence_algorithmic",
            "file",
            "Artifacts/Trackers/SamePersistence.NET1.rep2.tkr",
            "10B",
            ".expand_end.",
            ".END.",
            "item_0.absTotal",
            "item_1.absTotal",
            "sub#item_0.AnticipatedPercentage,item_1.AnticipatedPercentage",
        ]
        self.assertEqual(cmd, expected)

    def test_resume_and_metadata_validation(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            out_file = Path(tmpdir) / "test_pop.ndjson"
            meta_file = Path(tmpdir) / "test_pop.meta.json"

            # Initially empty
            trials = load_completed_trials(out_file, base_seed=12345)
            self.assertEqual(len(trials), 0)

            # Append trial 0 and trial 1
            rec0 = {"TrialIndex": 0, "Seed": 12345, "MeanDelta": 0.001}
            rec1 = {"TrialIndex": 1, "Seed": derive_trial_seed(12345, 1), "MeanDelta": -0.002}
            append_trial_record(out_file, rec0)
            append_trial_record(out_file, rec1)

            trials = load_completed_trials(out_file, base_seed=12345)
            self.assertEqual(len(trials), 2)
            self.assertIn(0, trials)
            self.assertIn(1, trials)

            # Seed mismatch detection
            with self.assertRaises(ValueError) as ctx:
                load_completed_trials(out_file, base_seed=99999)
            self.assertIn("Seed mismatch", str(ctx.exception))

            # Duplicate trial index detection
            append_trial_record(out_file, rec0)
            with self.assertRaises(ValueError) as ctx:
                load_completed_trials(out_file, base_seed=12345)
            self.assertIn("Duplicate TrialIndex", str(ctx.exception))

            # Metadata write and validate
            write_metadata(
                meta_file,
                base_seed=12345,
                trial_count=5,
                tracker="Artifacts/Trackers/SamePersistence.NET1.rep2.tkr",
                strategy_window="10B",
                obs_windows=["10B", "100B"],
                farm_bin="/path/to/Farm",
                analyzer_script="/path/to/analyzer.py",
                completed_trials=2,
            )
            # Validation should pass
            validate_metadata(
                meta_file,
                base_seed=12345,
                tracker="Artifacts/Trackers/SamePersistence.NET1.rep2.tkr",
                strategy_window="10B",
                obs_windows=["10B", "100B"],
            )

            # Metadata mismatch detection
            with self.assertRaises(ValueError) as ctx:
                validate_metadata(
                    meta_file,
                    base_seed=99999,
                    tracker="Artifacts/Trackers/SamePersistence.NET1.rep2.tkr",
                    strategy_window="10B",
                    obs_windows=["10B", "100B"],
                )
            self.assertIn("baseSeed", str(ctx.exception))

    def test_parse_args_defaults(self):
        args = parse_args(["--output", "/tmp/out.ndjson"])
        self.assertEqual(args.trials, 5)
        self.assertEqual(args.base_seed, 12345)
        self.assertEqual(args.strategy_window, "10B")
        self.assertEqual(args.observational_windows, ["10B", "100B"])
        self.assertFalse(args.keep_trials)
        self.assertFalse(args.continue_on_error)
        self.assertEqual(args.output, "/tmp/out.ndjson")

    def test_parse_args_custom(self):
        args = parse_args([
            "--trials", "10",
            "--base-seed", "42",
            "--tracker", "my_tracker.tkr",
            "--strategy-window", "5B",
            "--farm", "/custom/farm",
            "--analyzer", "/custom/analyzer.py",
            "--output", "pop.ndjson",
            "--keep-trials",
            "--trials-dir", "/tmp/trials",
            "--observational-windows", "5B", "50B",
            "--continue-on-error",
        ])
        self.assertEqual(args.trials, 10)
        self.assertEqual(args.base_seed, 42)
        self.assertEqual(args.tracker, "my_tracker.tkr")
        self.assertEqual(args.strategy_window, "5B")
        self.assertEqual(args.farm, "/custom/farm")
        self.assertEqual(args.analyzer, "/custom/analyzer.py")
        self.assertEqual(args.output, "pop.ndjson")
        self.assertTrue(args.keep_trials)
        self.assertEqual(args.trials_dir, "/tmp/trials")
        self.assertEqual(args.observational_windows, ["5B", "50B"])
        self.assertTrue(args.continue_on_error)

    def test_find_tools(self):
        farm_path = find_farm_executable()
        self.assertTrue(farm_path.exists())
        analyzer_path = find_analyzer_script()
        self.assertTrue(analyzer_path.exists())


if __name__ == "__main__":
    unittest.main()
