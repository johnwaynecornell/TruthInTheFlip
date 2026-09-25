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
    public void FluentCommandLine_ParsesNullTrialCommandCorrectly()
    {
        string path = CreateTestTrackerFile(recordCount: 20, stepTotal: 100);
        try
        {
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
}
