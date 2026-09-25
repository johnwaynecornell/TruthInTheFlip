using System.Globalization;
using FluentCommandLine;
using JWCFarm;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public sealed class ConditionalNullTests
{
    private static string CreateTestTrackerFile(int recordCount = 10, long stepTotal = 100)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_Null_Test_{Guid.NewGuid():N}.tkr");

        TrackerStore store = TrackerStore.Default(path);
        Tracker tracker = (Tracker)store.LoadOrCreate(true);

        DateTimeOffset begin = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        for (int i = 1; i <= recordCount; i++)
        {
            tracker.total = i * stepTotal;
            tracker.heads = (long)(tracker.total * 0.51);
            tracker.tails = tracker.total - tracker.heads;
            tracker.betHeads = (long)(tracker.total * 0.52);
            tracker.anticipated = (long)(tracker.total * 0.505);
            tracker.baseAnticipated = tracker.anticipated;
            tracker.anticipatedHeads = (long)(tracker.anticipated * 0.52);
            tracker.anticipatedTails = tracker.anticipated - tracker.anticipatedHeads;
            tracker.betSame = (long)(tracker.total * 0.50);
            tracker.anticipatedSame = (long)(tracker.total * 0.25);

            tracker.wallclockTimeNs = TimeSpan.FromMinutes(i * 10).Ticks * 100;
            tracker.utcBeginTimeMs = begin.AddMinutes((i - 1) * 10).ToUnixTimeMilliseconds();
            tracker.utcEndTimeMs = begin.AddMinutes(i * 10).ToUnixTimeMilliseconds();
            tracker.batchTotal = stepTotal;

            store.Save(tracker, true);
        }

        return path;
    }

    [Fact]
    public void SameSeed_ProducesIdenticalSyntheticSnapshots()
    {
        string path = CreateTestTrackerFile(recordCount: 8, stepTotal: 200);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);

            ulong seed = 20260925UL;

            var stream1 = spec.CreateSyntheticStream(seed);
            var records1 = stream1.Records.Cast<Tracker>().ToList();

            var stream2 = spec.CreateSyntheticStream(seed);
            var records2 = stream2.Records.Cast<Tracker>().ToList();

            Assert.Equal(records1.Count, records2.Count);
            Assert.True(records1.Count > 0);

            for (int i = 0; i < records1.Count; i++)
            {
                var r1 = records1[i];
                var r2 = records2[i];

                Assert.Equal(r1.total, r2.total);
                Assert.Equal(r1.heads, r2.heads);
                Assert.Equal(r1.tails, r2.tails);
                Assert.Equal(r1.anticipated, r2.anticipated);
                Assert.Equal(r1.anticipatedHeads, r2.anticipatedHeads);
                Assert.Equal(r1.anticipatedTails, r2.anticipatedTails);
                Assert.Equal(r1.betHeads, r2.betHeads);
                Assert.Equal(r1.betSame, r2.betSame);
                Assert.Equal(r1.utcEndTimeMs, r2.utcEndTimeMs);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSyntheticSnapshots()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 1000);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);

            var records1 = spec.CreateSyntheticStream(11111UL).Records.Cast<Tracker>().ToList();
            var records2 = spec.CreateSyntheticStream(99999UL).Records.Cast<Tracker>().ToList();

            Assert.Equal(records1.Count, records2.Count);

            // Verify that at least some records differ in heads / anticipated counts
            bool hasDifference = false;
            for (int i = 0; i < records1.Count; i++)
            {
                if (records1[i].heads != records2[i].heads ||
                    records1[i].anticipated != records2[i].anticipated)
                {
                    hasDifference = true;
                    break;
                }
            }

            Assert.True(hasDifference, "Different seeds should produce different synthetic outcomes.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SyntheticRecords_SatisfyAllAlgebraicInvariants()
    {
        string path = CreateTestTrackerFile(recordCount: 15, stepTotal: 500);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);

            var records = spec.CreateSyntheticStream(42UL).Records.Cast<Tracker>().ToList();
            Assert.Equal(15, records.Count);

            foreach (var r in records)
            {
                Assert.Equal(r.total, r.heads + r.tails);
                Assert.Equal(r.anticipated, r.anticipatedHeads + r.anticipatedTails);
                Assert.True(r.anticipated <= r.total, $"anticipated ({r.anticipated}) must be <= total ({r.total})");
                Assert.True(r.heads <= r.total, $"heads ({r.heads}) must be <= total ({r.total})");
                Assert.True(r.tails <= r.total, $"tails ({r.tails}) must be <= total ({r.total})");
                Assert.True(r.betHeads <= r.total, $"betHeads ({r.betHeads}) must be <= total ({r.total})");
                Assert.True(r.heads >= 0);
                Assert.True(r.tails >= 0);
                Assert.True(r.anticipated >= 0);
                Assert.True(r.anticipatedHeads >= 0);
                Assert.True(r.anticipatedTails >= 0);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IntervalDeltas_PreserveHistoricalDeltaTotalAndBetHeads()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 250);
        try
        {
            var store = TrackerStore.Default(path);
            var histRecords = store.Enumerate().Cast<Tracker>().ToList();

            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);
            var synRecords = spec.CreateSyntheticStream(12345UL).Records.Cast<Tracker>().ToList();

            Assert.Equal(histRecords.Count, synRecords.Count);

            long prevHistTotal = 0;
            long prevHistBetHeads = 0;
            long prevSynTotal = 0;
            long prevSynBetHeads = 0;

            for (int i = 0; i < histRecords.Count; i++)
            {
                long histDeltaTotal = histRecords[i].total - prevHistTotal;
                long histDeltaBetHeads = histRecords[i].betHeads - prevHistBetHeads;

                long synDeltaTotal = synRecords[i].total - prevSynTotal;
                long synDeltaBetHeads = synRecords[i].betHeads - prevSynBetHeads;

                Assert.Equal(histDeltaTotal, synDeltaTotal);
                Assert.Equal(histDeltaBetHeads, synDeltaBetHeads);

                prevHistTotal = histRecords[i].total;
                prevHistBetHeads = histRecords[i].betHeads;
                prevSynTotal = synRecords[i].total;
                prevSynBetHeads = synRecords[i].betHeads;
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GeneratedCorrectness_IsBoundedByCorrespondingBetCounts()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 100);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);
            var synRecords = spec.CreateSyntheticStream(777UL).Records.Cast<Tracker>().ToList();

            long prevTotal = 0;
            long prevBetHeads = 0;
            long prevAnticipatedHeads = 0;
            long prevAnticipatedTails = 0;

            foreach (var r in synRecords)
            {
                long deltaTotal = r.total - prevTotal;
                long deltaBetHeads = r.betHeads - prevBetHeads;
                long deltaBetTails = deltaTotal - deltaBetHeads;

                long deltaAnticipatedHeads = r.anticipatedHeads - prevAnticipatedHeads;
                long deltaAnticipatedTails = r.anticipatedTails - prevAnticipatedTails;

                Assert.InRange(deltaAnticipatedHeads, 0, deltaBetHeads);
                Assert.InRange(deltaAnticipatedTails, 0, deltaBetTails);

                prevTotal = r.total;
                prevBetHeads = r.betHeads;
                prevAnticipatedHeads = r.anticipatedHeads;
                prevAnticipatedTails = r.anticipatedTails;
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AbsoluteTotals_RemainMonotonicAndMatchHistoricalCadence()
    {
        string path = CreateTestTrackerFile(recordCount: 12, stepTotal: 300);
        try
        {
            var store = TrackerStore.Default(path);
            var histRecords = store.Enumerate().Cast<Tracker>().ToList();

            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);
            var synRecords = spec.CreateSyntheticStream(2026UL).Records.Cast<Tracker>().ToList();

            Assert.Equal(histRecords.Count, synRecords.Count);

            long lastTotal = 0;
            for (int i = 0; i < histRecords.Count; i++)
            {
                Assert.True(synRecords[i].total > lastTotal, "Synthetic totals must be strictly monotonic.");
                Assert.Equal(histRecords[i].total, synRecords[i].total);
                lastTotal = synRecords[i].total;
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SyntheticSelector_ComposesThroughTrackerWindow()
    {
        string path = CreateTestTrackerFile(recordCount: 20, stepTotal: 100);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);
            var synthetic = spec.CreateSyntheticSelector(999UL);

            var window = TrackerWindows.TrackerWindow.ByTotal(new Count(500));
            var windowed = TrackerWindows.Window(window, synthetic);

            var windowedRecords = windowed.Source().Records.Cast<Tracker>().ToList();
            Assert.Equal(20, windowedRecords.Count);

            // After reaching window capacity (total >= 500), relative total should be <= 500
            foreach (var w in windowedRecords)
            {
                Assert.True(w.total <= 500, $"Window total {w.total} should not exceed 500.");
                Assert.NotNull(w.Source);
                Assert.True(w.absTotal >= w.total);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SegmentStatsProcess_ConsumesSyntheticSelectorSuccessfully()
    {
        string path = CreateTestTrackerFile(recordCount: 30, stepTotal: 100);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);
            var synthetic = spec.CreateSyntheticSelector(555UL);

            var window = TrackerWindows.TrackerWindow.ByTotal(new Count(500));
            var windowed = TrackerWindows.Window(window, synthetic);
            var seg = TruthInTheFlip_Fluent.by_total(new Count(1000));

            var process = new SegmentStatsProcess(windowed, seg);
            var ctx = new FarmContext();

            List<SegmentStats> segments = new();
            process.Actions.Process = (c, rec) => segments.Add((SegmentStats)rec);
            process.Execute(ctx);

            Assert.True(segments.Count > 0, "Segments should be generated from synthetic stream.");

            var agg = new SegmentAggregate();
            foreach (var s in segments)
            {
                agg.Inspect(s);
                Assert.True(s.Count > 0);
                Assert.False(double.IsNaN(s.MeanTrueZ));
            }

            Assert.False(double.IsNaN(agg.EdgeExcursionScore));
            Assert.False(double.IsNaN(agg.EdgeSettlementScore));
            Assert.False(double.IsNaN(agg.EdgePersistenceIndex));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Conditioned_ThrowsFarmInputException_WhenSourceNotAccumulated()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 100);
        try
        {
            var rawSource = TruthInTheFlip_Fluent.Tracker(path);
            var windowedSource = TrackerWindows.Window(
                TrackerWindows.TrackerWindow.ByTotal(new Count(500)),
                rawSource);

            Assert.False(windowedSource.IsAccumulated);

            FarmInputException ex = Assert.Throws<FarmInputException>(() =>
            {
                ConditionalNullSpec.Conditioned(windowedSource);
            });

            Assert.Contains("accumulated historical tracker source", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StableFarmEnvironment_DoesNotExposeNullTrialOrConditioned()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 100);
        try
        {
            // Stable environment registers only TruthInTheFlip_Fluent
            var env = new FluentEnvironment();
            env.AddModule<TruthInTheFlip_Fluent>();
            env.ServeTypes = new[] { typeof(FarmCommand) };

            List<string> args = new()
            {
                "null_trial",
                "20260925",
                "conditioned",
                "file",
                path,
                "by_total",
                "500",
                "by_total",
                "1000"
            };

            int cursor = 0;
            var res = env.ParseOne(args, ref cursor);

            // In stable environment, null_trial is unrecognized
            Assert.Null(res);
            Assert.Equal(0, cursor);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FluentCommandLine_ParsesNullTrialCommandCorrectly()
    {
        string path = CreateTestTrackerFile(recordCount: 20, stepTotal: 100);
        try
        {
            // Experimental environment registers TruthInTheFlip_Fluent + experimental modules
            var env = new FluentEnvironment();
            env.AddModule<TruthInTheFlip_Fluent>();
            env.AddModule<ConditionalNullSpec>();
            env.AddModule<NullTrialCommand>();
            env.ServeTypes = new[] { typeof(FarmCommand) };

            List<string> args = new()
            {
                "null_trial",
                "20260925",
                "conditioned",
                "file",
                path,
                "by_total",
                "500",
                "by_total",
                "1000"
            };

            int cursor = 0;
            var res = env.ParseOne(args, ref cursor);

            Assert.NotNull(res);
            Assert.IsAssignableFrom<FarmCommand>(res.Result);
            Assert.Equal(args.Count, cursor);

            var cmd = (FarmCommand)res.Result!;
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using var errorOutput = new StringWriter(CultureInfo.InvariantCulture);
            var ctx = new FarmContext { Output = output, ErrorOutput = errorOutput };

            cmd.Execute(ctx);

            string outStr = output.ToString();
            Assert.Contains("=== Null Trial ===", outStr);
            Assert.Contains("Seed                  : 20260925", outStr);
            Assert.Contains("Edge Excursion Score", outStr);
            Assert.Contains("Edge Settlement Score", outStr);
            Assert.Contains("Edge Persistence Index", outStr);
            Assert.Contains("avgMeanA", outStr);
            Assert.Contains("avgMeanZHeads", outStr);
            Assert.Contains("Retained Anticipation", outStr);
            Assert.Contains("Settlement Adjusted A", outStr);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FastBinomialSampler_SanityAndBoundsTest()
    {
        var sampler1 = new FastBinomialSampler(12345UL);
        var sampler2 = new FastBinomialSampler(12345UL);

        long[] testN = [1, 10, 64, 100, 256, 1_000, 200_000_000L];

        foreach (long n in testN)
        {
            // Determinism on same seed
            long s1 = sampler1.Sample(n, 0.5);
            long s2 = sampler2.Sample(n, 0.5);
            Assert.Equal(s1, s2);

            // Bounded in [0, n]
            Assert.InRange(s1, 0, n);
        }

        // Statistical sanity for large batch size (M = 200,000,000) over 1000 samples
        long batchSize = 200_000_000L;
        int trials = 1000;
        double sum = 0;
        var sampler = new FastBinomialSampler(987654321UL);

        for (int i = 0; i < trials; i++)
        {
            long sample = sampler.Sample(batchSize, 0.5);
            Assert.InRange(sample, 0, batchSize);
            sum += sample;
        }

        double mean = sum / trials;
        double expectedMean = batchSize * 0.5; // 100,000,000
        double stdDev = 0.5 * Math.Sqrt(batchSize); // ~7071.0678
        double seOfMean = stdDev / Math.Sqrt(trials); // ~223.6

        // Mean should easily fall within 5 standard errors
        Assert.InRange(mean, expectedMean - 5 * seOfMean, expectedMean + 5 * seOfMean);
    }

    [Fact]
    public void NullTrialProcess_EmitsExactOrderedDeterministicTrials()
    {
        string path = CreateTestTrackerFile(recordCount: 20, stepTotal: 100);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);
            var window = TrackerWindows.TrackerWindow.ByTotal(new Count(500));
            var seg = TruthInTheFlip_Fluent.by_total(new Count(1000));

            int trialCount = 5;
            ulong baseSeed = 20260925UL;

            var process1 = new NullTrialProcess(trialCount, baseSeed, spec, window, seg);
            using var output1 = new StringWriter();
            var ctx1 = new FarmContext { Output = output1, ErrorOutput = output1 };

            List<NullTrialStats> items1 = new();
            process1.Actions.Process = (c, item) => items1.Add((NullTrialStats)item);
            process1.Execute(ctx1);

            Assert.Equal(trialCount, items1.Count);
            for (int i = 0; i < trialCount; i++)
            {
                Assert.Equal(i, items1[i].TrialIndex);
                if (i == 0)
                    Assert.Equal(baseSeed, items1[i].Seed);
                else
                    Assert.Equal(NullTrialProcess.DeriveTrialSeed(baseSeed, i), items1[i].Seed);
            }

            // Determinism check: second execution produces identical metrics
            var process2 = new NullTrialProcess(trialCount, baseSeed, spec, window, seg);
            List<NullTrialStats> items2 = new();
            process2.Actions.Process = (c, item) => items2.Add((NullTrialStats)item);
            process2.Execute(ctx1);

            Assert.Equal(items1.Count, items2.Count);
            for (int i = 0; i < trialCount; i++)
            {
                Assert.Equal(items1[i].Seed, items2[i].Seed);
                Assert.Equal(items1[i].EdgeExcursionScore, items2[i].EdgeExcursionScore);
                Assert.Equal(items1[i].EdgeSettlementScore, items2[i].EdgeSettlementScore);
                Assert.Equal(items1[i].EdgePersistenceIndex, items2[i].EdgePersistenceIndex);
                Assert.Equal(items1[i].RetainedAnticipation, items2[i].RetainedAnticipation);
                Assert.Equal(items1[i].SettlementAdjustedAnticipation, items2[i].SettlementAdjustedAnticipation);
            }

            // Variation check: different baseSeed produces different population
            var process3 = new NullTrialProcess(trialCount, 99999999UL, spec, window, seg);
            List<NullTrialStats> items3 = new();
            process3.Actions.Process = (c, item) => items3.Add((NullTrialStats)item);
            process3.Execute(ctx1);

            Assert.NotEqual(items1[0].Seed, items3[0].Seed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NullTrial_MatchesFirstTrialOfNullTrials_ForSameSeed()
    {
        string path = CreateTestTrackerFile(recordCount: 20, stepTotal: 100);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new ConditionalNullSpec(source);
            var window = TrackerWindows.TrackerWindow.ByTotal(new Count(500));
            var seg = TruthInTheFlip_Fluent.by_total(new Count(1000));
            ulong seed = 20260925UL;

            // Single trial run
            var schedule = spec.MaterializeSchedule();
            using var sw = new StringWriter();
            var ctx = new FarmContext { Output = sw, ErrorOutput = sw };

            var (aggSingle, segCountSingle, _) = NullTrialCommand.RunTrial(
                schedule, seed, window, seg, ctx);

            // NullTrialProcess run
            var process = new NullTrialProcess(3, seed, spec, window, seg);
            List<NullTrialStats> trials = new();
            process.Actions.Process = (c, item) => trials.Add((NullTrialStats)item);
            process.Execute(ctx);

            var trial0 = trials[0];

            Assert.Equal(seed, trial0.Seed);
            Assert.Equal(segCountSingle, trial0.SegmentCount);
            Assert.Equal(aggSingle.EdgeExcursionScore, trial0.EdgeExcursionScore);
            Assert.Equal(aggSingle.EdgeSettlementScore, trial0.EdgeSettlementScore);
            Assert.Equal(aggSingle.EdgePersistenceIndex, trial0.EdgePersistenceIndex);
            Assert.Equal(aggSingle.AvgMeanA, trial0.AvgMeanA);
            Assert.Equal(aggSingle.AvgEndA, trial0.AvgEndA);
            Assert.Equal(aggSingle.AvgMeanZHeads, trial0.AvgMeanZHeads);
            Assert.Equal(aggSingle.AvgEndZHeads, trial0.AvgEndZHeads);
            Assert.Equal(aggSingle.RetainedAnticipation, trial0.RetainedAnticipation);
            Assert.Equal(aggSingle.SettlementAdjustedAnticipation, trial0.SettlementAdjustedAnticipation);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Schedule_IsMaterializedOnce_AcrossMultipleTrials()
    {
        string path = CreateTestTrackerFile(recordCount: 15, stepTotal: 100);
        try
        {
            int streamOpenCount = 0;
            var rawSource = TruthInTheFlip_Fluent.Tracker(path);

            var countingSource = new TrackerSelector(() =>
            {
                streamOpenCount++;
                return rawSource.Source();
            }, isAccumulated: true);

            var spec = new ConditionalNullSpec(countingSource);
            var window = TrackerWindows.TrackerWindow.ByTotal(new Count(500));
            var seg = TruthInTheFlip_Fluent.by_total(new Count(1000));

            var process = new NullTrialProcess(10, 20260925UL, spec, window, seg);
            using var sw = new StringWriter();
            var ctx = new FarmContext { Output = sw, ErrorOutput = sw };

            List<NullTrialStats> trials = new();
            process.Actions.Process = (c, item) => trials.Add((NullTrialStats)item);
            process.Execute(ctx);

            Assert.Equal(10, trials.Count);
            // Must open underlying stream exactly once to materialize the schedule
            Assert.Equal(1, streamOpenCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EmpiricalMetricSummary_CalculatesPercentileAndCorrectedTailsCorrectly()
    {
        List<double> nullPop = [10.0, 20.0, 30.0, 40.0, 50.0];

        // Case 1: Median observed value (30.0)
        var sMid = EmpiricalMetricSummary.Compute("TestMetric", 30.0, nullPop);
        Assert.Equal(30.0, sMid.NullMean, precision: 6);
        Assert.Equal(30.0, sMid.NullMedian, precision: 6);
        Assert.Equal(15.811388, sMid.NullStdDev, precision: 4);
        Assert.Equal(60.0, sMid.Percentile, precision: 6); // 3 of 5 <= 30
        Assert.Equal(4.0 / 6.0, sMid.LowerTail, precision: 6); // (3 + 1) / (5 + 1) = 4/6
        Assert.Equal(4.0 / 6.0, sMid.UpperTail, precision: 6); // (3 + 1) / (5 + 1) = 4/6

        // Case 2: Observed strictly lower than all null values (5.0)
        var sLow = EmpiricalMetricSummary.Compute("TestMetric", 5.0, nullPop);
        Assert.Equal(0.0, sLow.Percentile, precision: 6); // 0 of 5 <= 5
        Assert.Equal(1.0 / 6.0, sLow.LowerTail, precision: 6); // (0 + 1) / (5 + 1) = 1/6 (> 0)
        Assert.Equal(6.0 / 6.0, sLow.UpperTail, precision: 6); // (5 + 1) / (5 + 1) = 6/6

        // Case 3: Observed strictly higher than all null values (55.0)
        var sHigh = EmpiricalMetricSummary.Compute("TestMetric", 55.0, nullPop);
        Assert.Equal(100.0, sHigh.Percentile, precision: 6); // 5 of 5 <= 55
        Assert.Equal(6.0 / 6.0, sHigh.LowerTail, precision: 6); // (5 + 1) / (5 + 1) = 6/6
        Assert.Equal(1.0 / 6.0, sHigh.UpperTail, precision: 6); // (0 + 1) / (5 + 1) = 1/6 (> 0)

        // Percentiles linear interpolation check
        Assert.Equal(12.0, sMid.NullP05, precision: 6); // 10 + 0.05 * 4 * 10 = 12
        Assert.Equal(48.0, sMid.NullP95, precision: 6); // 50 - 0.05 * 4 * 10 = 48
    }

    [Fact]
    public void FluentCommandLine_ParsesNullTrialsAndNullReport_InExperimentalEnvironment()
    {
        string path = CreateTestTrackerFile(recordCount: 20, stepTotal: 100);
        try
        {
            var env = new FluentEnvironment();
            env.AddModule<TruthInTheFlip_Fluent>();
            env.AddModule<ConditionalNullSpec>();
            env.AddModule<NullTrialCommand>();
            env.AddModule<NullTrialProcess>();
            env.AddModule<NullReportCommand>();
            env.ServeTypes = new[] { typeof(FarmCommand) };

            // 1. null_report command
            List<string> reportArgs = new()
            {
                "null_report",
                "3",
                "20260925",
                "conditioned",
                "file",
                path,
                "by_total",
                "500",
                "by_total",
                "1000"
            };

            int cursor1 = 0;
            var resReport = env.ParseOne(reportArgs, ref cursor1);

            Assert.NotNull(resReport);
            Assert.IsAssignableFrom<FarmCommand>(resReport.Result);
            Assert.Equal(reportArgs.Count, cursor1);

            var cmdReport = (FarmCommand)resReport.Result!;
            using var swReport = new StringWriter(CultureInfo.InvariantCulture);
            var ctxReport = new FarmContext { Output = swReport, ErrorOutput = swReport };
            cmdReport.Execute(ctxReport);

            string reportText = swReport.ToString();
            Assert.Contains("=== Null Distribution Report ===", reportText);
            Assert.Contains("Trials                : 3", reportText);
            Assert.Contains("EdgeExcursionScore", reportText);
            Assert.Contains("EdgeSettlementScore", reportText);
            Assert.Contains("RetainedAnticipation", reportText);
            Assert.Contains("SettlementAdjustedAnticipation", reportText);

            // 2. csv null_trials command
            List<string> csvArgs = new()
            {
                "csv",
                "null_trials",
                "3",
                "20260925",
                "conditioned",
                "file",
                path,
                "by_total",
                "500",
                "by_total",
                "1000",
                "TrialIndex",
                "Seed",
                "EdgeExcursionScore",
                "SettlementAdjustedAnticipation"
            };

            int cursor2 = 0;
            var resCsv = env.ParseOne(csvArgs, ref cursor2);

            Assert.NotNull(resCsv);
            Assert.IsAssignableFrom<FarmCommand>(resCsv.Result);
            Assert.Equal(csvArgs.Count, cursor2);

            var cmdCsv = (FarmCommand)resCsv.Result!;
            using var swCsv = new StringWriter(CultureInfo.InvariantCulture);
            var ctxCsv = new FarmContext { Output = swCsv, ErrorOutput = swCsv };
            cmdCsv.Execute(ctxCsv);

            string csvText = swCsv.ToString();
            string[] lines = csvText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(4, lines.Length); // 1 header + 3 rows
            Assert.Contains("TrialIndex,Seed,EdgeExcursionScore,SettlementAdjustedAnticipation", lines[0]);
            Assert.StartsWith("0,20260925,", lines[1]);

            // 3. json null_trials command
            List<string> jsonArgs = new()
            {
                "json",
                "null_trials",
                "2",
                "20260925",
                "conditioned",
                "file",
                path,
                "by_total",
                "500",
                "by_total",
                "1000",
                "TrialIndex",
                "Seed",
                "EdgeExcursionScore"
            };

            int cursor3 = 0;
            var resJson = env.ParseOne(jsonArgs, ref cursor3);

            Assert.NotNull(resJson);
            var cmdJson = (FarmCommand)resJson.Result!;
            using var swJson = new StringWriter(CultureInfo.InvariantCulture);
            var ctxJson = new FarmContext { Output = swJson, ErrorOutput = swJson };
            cmdJson.Execute(ctxJson);

            string jsonText = swJson.ToString();
            string[] jsonLines = jsonText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, jsonLines.Length);
            Assert.Contains("\"TrialIndex\":0", jsonLines[0]);
            Assert.Contains("\"Seed\":20260925", jsonLines[0]);
            Assert.Contains("\"EdgeExcursionScore\":", jsonLines[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StableFarmEnvironment_DoesNotExposeNullTrialsOrNullReport()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 100);
        try
        {
            var env = new FluentEnvironment();
            env.AddModule<TruthInTheFlip_Fluent>();
            env.ServeTypes = new[] { typeof(FarmCommand) };

            List<string> args1 = new()
            {
                "null_report", "5", "20260925", "conditioned", "file", path, "by_total", "500", "by_total", "1000"
            };
            int cursor1 = 0;
            var res1 = env.ParseOne(args1, ref cursor1);
            Assert.Null(res1);
            Assert.Equal(0, cursor1);

            List<string> args2 = new()
            {
                "csv", "null_trials", "5", "20260925", "conditioned", "file", path, "by_total", "500", "by_total", "1000", "TrialIndex"
            };
            int cursor2 = 0;
            var res2 = env.ParseOne(args2, ref cursor2);
            Assert.Null(res2);
            Assert.Equal(0, cursor2);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
