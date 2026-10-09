using FluentCommandLine;
using JWCFarm;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public sealed class SyntheticTrackerSourceTests
{
    private static string CreateTestTrackerFile(
        int recordCount = 10,
        long stepTotal = 200_000_000L,
        double historicalSameRate = 0.52,
        double historicalHeadsRate = 0.51)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_SyntheticSource_Test_{Guid.NewGuid():N}.tkr");

        TrackerStore store = TrackerStore.Default(path);
        Tracker tracker = (Tracker)store.LoadOrCreate(true);

        DateTimeOffset begin = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        for (int i = 1; i <= recordCount; i++)
        {
            tracker.total = i * stepTotal;
            tracker.heads = (long)(tracker.total * historicalHeadsRate);
            tracker.tails = tracker.total - tracker.heads;
            tracker.betHeads = (long)(tracker.total * 0.53);
            tracker.anticipated = (long)(tracker.total * 0.505);
            tracker.baseAnticipated = tracker.anticipated;
            tracker.anticipatedHeads = (long)(tracker.anticipated * 0.52);
            tracker.anticipatedTails = tracker.anticipated - tracker.anticipatedHeads;
            tracker.betSame = tracker.total;
            tracker.anticipatedSame = (long)(tracker.total * historicalSameRate);

            tracker.wallclockTimeNs = TimeSpan.FromMinutes(i * 10).Ticks * 100;
            tracker.utcBeginTimeMs = begin.AddMinutes((i - 1) * 10).ToUnixTimeMilliseconds();
            tracker.utcEndTimeMs = begin.AddMinutes(i * 10).ToUnixTimeMilliseconds();
            tracker.batchTotal = stepTotal;

            store.Save(tracker, true);
        }

        return path;
    }

    [Fact]
    public void Synthetic_ConditionalNull_SameSeed_ProducesRecordForRecordDeterministicIdentity()
    {
        string path = CreateTestTrackerFile(recordCount: 8, stepTotal: 200_000_000L);
        try
        {
            var historicalSource = TruthInTheFlip_Fluent.Tracker(path);
            var condition = ConditionalNullSpec.Conditioned(historicalSource);
            ulong seed = 987654321UL;

            var selectorA = TruthInTheFlip_Fluent.Synthetic(seed, condition);
            var selectorB = TruthInTheFlip_Fluent.Synthetic(seed, condition);

            using var streamA = selectorA.Source();
            using var streamB = selectorB.Source();

            var recordsA = streamA.Records.Cast<Tracker>().ToList();
            var recordsB = streamB.Records.Cast<Tracker>().ToList();

            Assert.NotEmpty(recordsA);
            Assert.Equal(recordsA.Count, recordsB.Count);

            for (int i = 0; i < recordsA.Count; i++)
            {
                var rA = recordsA[i];
                var rB = recordsB[i];

                Assert.Equal(rA.total, rB.total);
                Assert.Equal(rA.heads, rB.heads);
                Assert.Equal(rA.tails, rB.tails);
                Assert.Equal(rA.anticipated, rB.anticipated);
                Assert.Equal(rA.baseAnticipated, rB.baseAnticipated);
                Assert.Equal(rA.anticipatedHeads, rB.anticipatedHeads);
                Assert.Equal(rA.anticipatedTails, rB.anticipatedTails);
                Assert.Equal(rA.betHeads, rB.betHeads);
                Assert.Equal(rA.betSame, rB.betSame);
                Assert.Equal(rA.batchTotal, rB.batchTotal);
                Assert.Equal(rA.wallclockTimeNs, rB.wallclockTimeNs);
                Assert.Equal(rA.batchWallclockTimeNs, rB.batchWallclockTimeNs);
                Assert.Equal(rA.utcBeginTimeMs, rB.utcBeginTimeMs);
                Assert.Equal(rA.utcEndTimeMs, rB.utcEndTimeMs);
                Assert.Equal(rA.cumulativeTicks, rB.cumulativeTicks);
                Assert.Equal(rA.IsComplete, rB.IsComplete);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Synthetic_AlgorithmicNull_SameSeed_ProducesRecordForRecordDeterministicIdentity()
    {
        string path = CreateTestTrackerFile(recordCount: 8, stepTotal: 200_000_000L);
        try
        {
            var historicalSource = TruthInTheFlip_Fluent.Tracker(path);
            var condition = SamePersistenceAlgorithmicNullSpec.SamePersistenceAlgorithmic(historicalSource, new Count(1_000_000_000L));
            ulong seed = 123456789UL;

            var selectorA = TruthInTheFlip_Fluent.Synthetic(seed, condition);
            var selectorB = TruthInTheFlip_Fluent.Synthetic(seed, condition);

            using var streamA = selectorA.Source();
            using var streamB = selectorB.Source();

            var recordsA = streamA.Records.Cast<Tracker>().ToList();
            var recordsB = streamB.Records.Cast<Tracker>().ToList();

            Assert.NotEmpty(recordsA);
            Assert.Equal(recordsA.Count, recordsB.Count);

            for (int i = 0; i < recordsA.Count; i++)
            {
                var rA = recordsA[i];
                var rB = recordsB[i];

                Assert.Equal(rA.total, rB.total);
                Assert.Equal(rA.heads, rB.heads);
                Assert.Equal(rA.tails, rB.tails);
                Assert.Equal(rA.anticipated, rB.anticipated);
                Assert.Equal(rA.baseAnticipated, rB.baseAnticipated);
                Assert.Equal(rA.anticipatedHeads, rB.anticipatedHeads);
                Assert.Equal(rA.anticipatedTails, rB.anticipatedTails);
                Assert.Equal(rA.betHeads, rB.betHeads);
                Assert.Equal(rA.betSame, rB.betSame);
                Assert.Equal(rA.anticipatedSame, rB.anticipatedSame);
                Assert.Equal(rA.same, rB.same);
                Assert.Equal(rA.diff, rB.diff);
                Assert.Equal(rA.batchTotal, rB.batchTotal);
                Assert.Equal(rA.wallclockTimeNs, rB.wallclockTimeNs);
                Assert.Equal(rA.batchWallclockTimeNs, rB.batchWallclockTimeNs);
                Assert.Equal(rA.utcBeginTimeMs, rB.utcBeginTimeMs);
                Assert.Equal(rA.utcEndTimeMs, rB.utcEndTimeMs);
                Assert.Equal(rA.cumulativeTicks, rB.cumulativeTicks);
                Assert.Equal(rA.IsComplete, rB.IsComplete);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Synthetic_DifferentSeed_ProducesDifferentRealization_WhilePreservingCadence()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 200_000_000L);
        try
        {
            var historicalSource = TruthInTheFlip_Fluent.Tracker(path);
            var condition = SamePersistenceAlgorithmicNullSpec.SamePersistenceAlgorithmic(historicalSource, new Count(1_000_000_000L));

            var selectorA = TruthInTheFlip_Fluent.Synthetic(11111UL, condition);
            var selectorB = TruthInTheFlip_Fluent.Synthetic(99999UL, condition);

            using var streamA = selectorA.Source();
            using var streamB = selectorB.Source();

            var recordsA = streamA.Records.Cast<Tracker>().ToList();
            var recordsB = streamB.Records.Cast<Tracker>().ToList();

            Assert.Equal(recordsA.Count, recordsB.Count);

            bool hasDifference = false;
            for (int i = 0; i < recordsA.Count; i++)
            {
                var rA = recordsA[i];
                var rB = recordsB[i];

                // Geometry is strictly preserved
                Assert.Equal(rA.total, rB.total);
                Assert.Equal(rA.batchTotal, rB.batchTotal);
                Assert.Equal(rA.wallclockTimeNs, rB.wallclockTimeNs);
                Assert.Equal(rA.batchWallclockTimeNs, rB.batchWallclockTimeNs);
                Assert.Equal(rA.utcBeginTimeMs, rB.utcBeginTimeMs);
                Assert.Equal(rA.utcEndTimeMs, rB.utcEndTimeMs);
                Assert.Equal(rA.cumulativeTicks, rB.cumulativeTicks);

                // Stochastic counts diverge
                if (rA.heads != rB.heads || rA.anticipated != rB.anticipated)
                {
                    hasDifference = true;
                }
            }

            Assert.True(hasDifference, "Different seeds must produce different stochastic realizations.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Synthetic_WindowPairZip_PreservesRealizationAndDivergesAtWindowBoundary()
    {
        // 12 records * 200M = 2.4B total.
        // Window 1: 1.0B (5 records)
        // Window 2: 2.0B (10 records)
        string path = CreateTestTrackerFile(recordCount: 12, stepTotal: 200_000_000L);
        try
        {
            var historicalSource = TruthInTheFlip_Fluent.Tracker(path);
            var condition = SamePersistenceAlgorithmicNullSpec.SamePersistenceAlgorithmic(historicalSource, new Count(1_000_000_000L));
            ulong seed = 42UL;

            // Two branches using the same seed and null condition:
            var branch10B = TrackerWindows.Window(
                TrackerWindows.TrackerWindow.ByTotal(new Count(1_000_000_000L)),
                TruthInTheFlip_Fluent.Synthetic(seed, condition));

            var branch20B = TrackerWindows.Window(
                TrackerWindows.TrackerWindow.ByTotal(new Count(2_000_000_000L)),
                TruthInTheFlip_Fluent.Synthetic(seed, condition));

            var zipProcess = new ZipProcess(
                TruthInTheFlip_Fluent.TrackerReport(branch10B),
                TruthInTheFlip_Fluent.TrackerReport(branch20B));

            var zippedRecords = new List<ProcessArrayStats>();
            zipProcess.Actions = new ProcessActions(process: (_, item) => zippedRecords.Add((ProcessArrayStats)item));
            zipProcess.Execute(new FarmContext());
            Assert.Equal(12, zippedRecords.Count);

            for (int i = 0; i < zippedRecords.Count; i++)
            {
                var zipItem = zippedRecords[i];
                var t1 = (Tracker)zipItem.Items[0];
                var t2 = (Tracker)zipItem.Items[1];

                // Both branches must observe identical absolute total coordinates
                Assert.Equal(t1.Source.total, t2.Source.total);

                long currentTotal = t1.Source.total;
                if (currentTotal <= 1_000_000_000L)
                {
                    // Within 1.0B, both windows encompass the exact same history
                    Assert.Equal(t1.total, t2.total);
                    Assert.Equal(t1.anticipated, t2.anticipated);
                    Assert.Equal(t1.heads, t2.heads);
                    Assert.Equal(t1.tails, t2.tails);
                }
                else
                {
                    // Past 1.0B, the 1.0B window drops older history while 2.0B continues accumulating
                    // Realizations are from the same simulation path, but interval-relative metrics diverge
                    Assert.Equal(1_000_000_000L, t1.total);
                    Assert.True(t2.total > 1_000_000_000L);
                }
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Synthetic_FluentCommandLine_ParsesAndExecutesSuccessfully()
    {
        string path = CreateTestTrackerFile(recordCount: 6, stepTotal: 200_000_000L);
        try
        {
            FluentEnvironment env = new();
            env.AddModule<TruthInTheFlip_Fluent>();
            env.ServeTypes = new[] { typeof(FarmCommand) };

            List<string> commandLine =
            [
                "csv",
                "tracker",
                "synthetic",
                "12345",
                "same_persistence_algorithmic",
                "file",
                path,
                "10B",
                "absTotal",
                "heads",
                "tails",
                "anticipated"
            ];

            int index = 0;
            var parseResult = env.ParseOne(commandLine, ref index);

            Assert.NotNull(parseResult);
            Assert.IsAssignableFrom<FarmCommand>(parseResult.Result);
            Assert.Equal(commandLine.Count, index);

            var cmd = (FarmCommand)parseResult.Result;
            using var sw = new StringWriter();
            var ctx = new FarmContext { Output = sw };
            cmd.Execute(ctx);

            string output = sw.ToString();
            Assert.Contains("absTotal,heads,tails,anticipated", output);
            Assert.Contains("200000000", output);
            Assert.Contains("1200000000", output);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
