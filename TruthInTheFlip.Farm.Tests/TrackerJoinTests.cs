using System.Globalization;
using FluentCommandLine;
using JWCFarm;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public class TrackerJoinTests
{
    [Fact]
    public void JoinRejectsWindowedTrackerSources()
    {
        var accumulated = new TrackerSelector(
            () => throw new InvalidOperationException(
                "The guard should run before opening the source."));

        var windowed = TrackerWindows.Window(
            TrackerWindows.TrackerWindow.ByTotal(new Count(100)),
            accumulated);

        var joined = TrackerSelector.Join(
            accumulated,
            windowed);

        FarmInputException error =
            Assert.Throws<FarmInputException>(
                () => joined.Source());

        Assert.Contains(
            "only accumulated tracker sources",
            error.Message);
    }
    
    [Fact]
    public void WindowProducesIntervalRelativeSelector()
    {
        var source = new TrackerSelector(
            () => throw new Exception());

        var windowed = TrackerWindows.Window(
            TrackerWindows.TrackerWindow.ByTotal(new Count(100)),
            source);

        Assert.True(source.IsAccumulated);
        Assert.False(windowed.IsAccumulated);
    }

    [Fact]
    public void Join_NormalizesFirstSourceAsWellAsLaterSources_ClearingInheritedSourceAndFrom()
    {
        string path1 = CreateTrackerFile(100, 200, 300);
        string path2 = CreateTrackerFile(1000, 1050, 1150);

        try
        {
            var rebasedA = TrackerSelector.Rebase(TruthInTheFlip_Fluent.Tracker(path1));
            var rebasedB = TrackerSelector.Rebase(TruthInTheFlip_Fluent.Tracker(path2));

            // Confirm pre-join state of rebasedA: records have From != null and Source != self
            using (var streamA = rebasedA.Source())
            {
                var recordsA = streamA.Records.Cast<Tracker>().ToList();
                Assert.NotEmpty(recordsA);
                foreach (var r in recordsA)
                {
                    Assert.NotNull(r.From);
                    Assert.NotSame(r, r.Source);
                }
            }

            var joined = TrackerSelector.Join(rebasedA, rebasedB);

            using var stream = joined.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            Assert.Equal(4, records.Count);

            // Guard against prior asymmetry: first source records AND later source records
            // MUST be normalized such that Source == self and From == null.
            for (int i = 0; i < records.Count; i++)
            {
                Tracker r = records[i];
                Assert.Same(r, r.Source);
                Assert.Null(r.From);
            }
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void ConcatRebaseA_RebaseB_TotalsAreContinuousAndRecordsNormalized()
    {
        string path1 = CreateTrackerFile(100, 200, 300);
        string path2 = CreateTrackerFile(1000, 1060, 1140);

        try
        {
            var sourceA = TrackerSelector.Rebase(TruthInTheFlip_Fluent.Tracker(path1));
            var sourceB = TrackerSelector.Rebase(TruthInTheFlip_Fluent.Tracker(path2));

            // rebase A: [200 - 100 = 100, 300 - 100 = 200]
            // rebase B: [1060 - 1000 = 60, 1140 - 1000 = 140]
            // concat rebase A rebase B: [100, 200, 200 + 60 = 260, 200 + 140 = 340]
            var joined = TrackerSelector.Join(sourceA, sourceB);

            using var stream = joined.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            Assert.Equal(4, records.Count);

            long[] totals = records.Select(r => r.total).ToArray();
            Assert.Equal(new long[] { 100, 200, 260, 340 }, totals);

            // Totals are strictly increasing and continuous across source boundaries
            for (int i = 1; i < totals.Length; i++)
            {
                Assert.True(totals[i] > totals[i - 1]);
            }

            // Every joined output record has Source == self and From == null
            foreach (Tracker r in records)
            {
                Assert.Same(r, r.Source);
                Assert.Null(r.From);
            }
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void Rebase_ConcatRebaseA_RebaseB_FixedBaselineAndContinuousJoinedSpace()
    {
        string path1 = CreateTrackerFile(100, 200, 300);
        string path2 = CreateTrackerFile(1000, 1060, 1140);

        try
        {
            var sourceA = TrackerSelector.Rebase(TruthInTheFlip_Fluent.Tracker(path1));
            var sourceB = TrackerSelector.Rebase(TruthInTheFlip_Fluent.Tracker(path2));
            var joined = TrackerSelector.Join(sourceA, sourceB);
            var rebasedJoined = TrackerSelector.Rebase(joined);

            using var stream = rebasedJoined.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            // Joined sequence has 4 records with totals: [100, 200, 260, 340]
            // Rebasing uses the first record (total = 100) as the fixed baseline and does NOT emit it.
            // Emitted records: [200 - 100 = 100, 260 - 100 = 160, 340 - 100 = 240]
            Assert.Equal(3, records.Count);
            Assert.Equal(new long[] { 100, 160, 240 }, records.Select(r => r.total).ToArray());

            Tracker baseline = records[0].From!;
            Assert.NotNull(baseline);
            Assert.Equal(100, baseline.total);

            // Check invariants for every emitted rebased record
            for (int i = 0; i < records.Count; i++)
            {
                Tracker r = records[i];

                // result.total == result.Source.total - result.From.total
                Assert.NotNull(r.Source);
                Assert.NotNull(r.From);
                Assert.Equal(r.total, r.Source.total - r.From!.total);

                // From remains the same fixed baseline for the full rebased sequence
                Assert.Same(baseline, r.From);
                Assert.Equal(100, r.From.total);
            }

            // Source.total advances continuously in the synthetic joined coordinate space [200, 260, 340]
            Assert.Equal(200, records[0].Source.total);
            Assert.Equal(260, records[1].Source.total);
            Assert.Equal(340, records[2].Source.total);
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void Rebase_NoSyntheticZero_EmitsOnlySubsequentDifferences()
    {
        string path = CreateTrackerFile(100, 200, 300);

        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var rebased = TrackerSelector.Rebase(source);

            using var stream = rebased.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            // Rebasing [100, 200, 300] produces [100, 200], not [0, 100, 200]
            Assert.Equal(2, records.Count);
            Assert.Equal(new long[] { 100, 200 }, records.Select(r => r.total).ToArray());
            Assert.DoesNotContain(records, r => r.total == 0);

            // Check metadata
            Assert.Equal(200, records[0].Source.total);
            Assert.Equal(100, records[0].From!.total);
            Assert.Equal(300, records[1].Source.total);
            Assert.Equal(100, records[1].From!.total);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CLI_ConcatRebaseA_RebaseB_SmokeAndRegression()
    {
        string path1 = CreateTrackerFile(100, 200, 300);
        string path2 = CreateTrackerFile(1000, 1060, 1140);

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "concat",
                "rebase",
                "file",
                path1,
                "rebase",
                "file",
                path2,
                "total");

            string[] lines = NonEmptyLines(csv);
            Assert.Equal("total", lines[0]);

            long[] actualTotals = lines.Skip(1).Select(long.Parse).ToArray();
            Assert.Equal(new long[] { 100, 200, 260, 340 }, actualTotals);
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void CLI_Rebase_ConcatRebaseA_RebaseB_SmokeAndRegression()
    {
        string path1 = CreateTrackerFile(100, 200, 300);
        string path2 = CreateTrackerFile(1000, 1060, 1140);

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "rebase",
                "concat",
                "rebase",
                "file",
                path1,
                "rebase",
                "file",
                path2,
                "total");

            string[] lines = NonEmptyLines(csv);
            Assert.Equal("total", lines[0]);

            long[] actualTotals = lines.Skip(1).Select(long.Parse).ToArray();
            Assert.Equal(new long[] { 100, 160, 240 }, actualTotals);
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void CLI_Rebase_NoSyntheticZero_SmokeAndRegression()
    {
        string path = CreateTrackerFile(100, 200, 300);

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "rebase",
                "file",
                path,
                "total");

            string[] lines = NonEmptyLines(csv);
            Assert.Equal("total", lines[0]);

            long[] actualTotals = lines.Skip(1).Select(long.Parse).ToArray();
            Assert.Equal(new long[] { 100, 200 }, actualTotals);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTrackerFile(params long[] totals)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_Farm_Test_{Guid.NewGuid():N}.tkr");

        TrackerStore store = TrackerStore.Default(path);
        Tracker tracker = (Tracker)store.LoadOrCreate(true);

        DateTimeOffset begin = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

        for (int i = 0; i < totals.Length; i++)
        {
            tracker.total = totals[i];
            tracker.heads = totals[i] / 2;
            tracker.tails = totals[i] - tracker.heads;
            tracker.anticipated = totals[i] / 2;
            tracker.baseAnticipated = tracker.anticipated;
            tracker.anticipatedHeads = tracker.heads / 2;
            tracker.anticipatedTails = tracker.baseAnticipated - tracker.anticipatedHeads;
            tracker.betSame = totals[i] / 2;
            tracker.anticipatedSame = tracker.betSame / 2;
            tracker.wallclockTimeNs = TimeSpan.FromMinutes((i + 1) * 10).Ticks * 100;
            tracker.utcBeginTimeMs = begin.AddMinutes(i * 10).ToUnixTimeMilliseconds();
            tracker.utcEndTimeMs = begin.AddMinutes((i + 1) * 10).ToUnixTimeMilliseconds();

            store.Save(tracker, true);
        }

        return path;
    }

    private static string[] NonEmptyLines(string text)
    {
        return text.Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static string RunFarm(params string[] args)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        FluentEnvironment env = new FluentEnvironment();
        env.AddModule<TruthInTheFlip_Fluent>();

        env.ServeTypes = new[] { typeof(FarmCommand) };

        int cursor = 0;
        var res = env.ParseOne(args, ref cursor);

        FarmCommand command = (FarmCommand)res.Result;

        var context = new FarmContext
        {
            Output = output,
            ErrorOutput = error
        };

        command.Execute(context);

        return output.ToString();
    }
}