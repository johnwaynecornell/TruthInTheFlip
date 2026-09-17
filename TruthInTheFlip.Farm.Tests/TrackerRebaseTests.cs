using System.Globalization;
using FluentCommandLine;
using JWCFarm;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public sealed class TrackerRebaseTests
{
    [Fact]
    public void BasicRebase_FirstRecordBecomesBaseline_SubsequentRecordsAreRelative()
    {
        string path = CreateTrackerFile();

        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var rebased = TrackerSelector.Rebase(source);

            using var stream = rebased.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            // Original file has 4 records (totals: 100, 200, 300, 400)
            // Rebased should have 3 records (totals: 100, 200, 300)
            Assert.Equal(3, records.Count);

            Assert.Equal(100, records[0].total); // 200 - 100
            Assert.Equal(200, records[1].total); // 300 - 100
            Assert.Equal(300, records[2].total); // 400 - 100

            // Check Source and From metadata
            Assert.NotNull(records[0].Source);
            Assert.NotNull(records[0].From);
            Assert.Equal(200, records[0].Source.total);
            Assert.Equal(100, records[0].From!.total);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BaselineRecord_IsNotEmitted()
    {
        string path = CreateTrackerFile();

        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var rebased = TrackerSelector.Rebase(source);

            using var stream = rebased.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            // First output record must be 200 - 100 = 100, NOT 100 - 100 = 0
            Assert.Equal(100, records[0].total);
            Assert.DoesNotContain(records, r => r.total == 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EmptySource_ProducesEmptyRebasedSourceCleanly()
    {
        string path = CreateEmptyTrackerFile();

        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var rebased = TrackerSelector.Rebase(source);

            using var stream = rebased.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            Assert.Empty(records);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleRecordSource_ProducesEmptyRebasedSourceCleanly()
    {
        string path = CreateSingleRecordTrackerFile();

        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var rebased = TrackerSelector.Rebase(source);

            using var stream = rebased.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            Assert.Empty(records);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MultipleAdditiveTrackerFields_AgreeWithUtilTSubtract()
    {
        string path = CreateTrackerFile();

        try
        {
            TrackerStore store = TrackerStore.Default(path);
            var originalRecords = store.Enumerate().Cast<Tracker>().ToList();
            int[] ver = TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version!)!;

            var source = TruthInTheFlip_Fluent.Tracker(path);
            var rebased = TrackerSelector.Rebase(source);

            using var stream = rebased.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            Tracker baseline = originalRecords[0];

            for (int i = 0; i < records.Count; i++)
            {
                Tracker raw = originalRecords[i + 1];
                Tracker expected = UtilT.Subtract(store, ver, raw, baseline);
                Tracker actual = records[i];

                Assert.Equal(expected.total, actual.total);
                Assert.Equal(expected.heads, actual.heads);
                Assert.Equal(expected.tails, actual.tails);
                Assert.Equal(expected.anticipated, actual.anticipated);
                Assert.Equal(expected.baseAnticipated, actual.baseAnticipated);
                Assert.Equal(expected.anticipatedHeads, actual.anticipatedHeads);
                Assert.Equal(expected.anticipatedTails, actual.anticipatedTails);
                Assert.Equal(expected.cumulativeTicks, actual.cumulativeTicks);
                Assert.Equal(expected.betHeads, actual.betHeads);
                Assert.Equal(expected.betSame, actual.betSame);
                Assert.Equal(expected.anticipatedSame, actual.anticipatedSame);
                Assert.Equal(expected.wallclockTimeNs, actual.wallclockTimeNs);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MetadataAndTimeSemantics_FollowUtilTSubtract()
    {
        string path = CreateTrackerFile();

        try
        {
            TrackerStore store = TrackerStore.Default(path);
            var originalRecords = store.Enumerate().Cast<Tracker>().ToList();

            var source = TruthInTheFlip_Fluent.Tracker(path);
            var rebased = TrackerSelector.Rebase(source);

            using var stream = rebased.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            Tracker baseline = originalRecords[0];
            Tracker rawSecond = originalRecords[1];
            Tracker rebasedSecond = records[0];

            // utcBeginTime of the rebased window starts when baseline ended
            Assert.Equal(baseline.utcEndTimeMs, rebasedSecond.utcBeginTimeMs);
            // utcEndTime is when the current state ended
            Assert.Equal(rawSecond.utcEndTimeMs, rebasedSecond.utcEndTimeMs);
            // wallclock duration is elapsed duration between baseline and current
            Assert.Equal(rawSecond.wallclockTimeNs - baseline.wallclockTimeNs, rebasedSecond.wallclockTimeNs);
            // batchTotal matches the current record's batch total
            Assert.Equal(rawSecond.batchTotal, rebasedSecond.batchTotal);
            // IsComplete is preserved
            Assert.True(rebasedSecond.IsComplete);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WindowThenRebase_RemovesInheritedOriginCorrectly()
    {
        string path = CreateTrackerFile();

        try
        {
            // Apply a rolling window by total of 200, then rebase
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var windowed = TrackerWindows.Window(
                TrackerWindows.TrackerWindow.ByTotal(new Count(200)),
                source);
            var rebased = TrackerSelector.Rebase(windowed);

            using var stream = rebased.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            // Windowed records for totals [100, 200, 300, 400] with window size 200:
            // win[0] (raw 100): 100
            // win[1] (raw 200): 200
            // win[2] (raw 300): 200 (300 - 100)
            // win[3] (raw 400): 200 (400 - 200)
            // Rebasing uses win[0] (100) as baseline:
            // rebased[0] = win[1] - win[0] = 200 - 100 = 100
            // rebased[1] = win[2] - win[0] = 200 - 100 = 100
            // rebased[2] = win[3] - win[0] = 200 - 100 = 100
            Assert.Equal(3, records.Count);
            Assert.Equal(100, records[0].total);
            Assert.Equal(100, records[1].total);
            Assert.Equal(100, records[2].total);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RebaseThenJoin_ProducesMonotonicallyContinuousTotalsAcrossBoundary()
    {
        string path1 = CreateTrackerFile();
        string path2 = CreateTrackerFile();

        try
        {
            var s1 = TrackerSelector.Rebase(TruthInTheFlip_Fluent.Tracker(path1));
            var s2 = TrackerSelector.Rebase(TruthInTheFlip_Fluent.Tracker(path2));

            var joined = TrackerSelector.Join(s1, s2);

            using var stream = joined.Source();
            var records = stream.Records.Cast<Tracker>().ToList();

            // s1 rebased: [100, 200, 300] (terminal offset = 300)
            // s2 rebased: [100, 200, 300] + offset 300 = [400, 500, 600]
            Assert.Equal(6, records.Count);
            long[] totals = records.Select(r => r.total).ToArray();
            Assert.Equal(new long[] { 100, 200, 300, 400, 500, 600 }, totals);
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void ConcatRebaseViaCLI_ProducesExpectedSequence()
    {
        string path1 = CreateTrackerFile();
        string path2 = CreateTrackerFile();

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
            Assert.Equal(new long[] { 100, 200, 300, 400, 500, 600 }, actualTotals);
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void Rebase_LazyEvaluation_DoesNotOpenStreamUntilEnumerated()
    {
        string path = CreateTrackerFile();
        try
        {
            int openedCount = 0;
            var fakeSelector = new TrackerSelector(() =>
            {
                openedCount++;
                return TruthInTheFlip_Fluent.OpenTrackerStream(path);
            });

            var rebased = TrackerSelector.Rebase(fakeSelector);
            Assert.Equal(0, openedCount);

            using var stream = rebased.Source();
            Assert.Equal(1, openedCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateEmptyTrackerFile()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_Farm_Test_Empty_{Guid.NewGuid():N}.tkr");

        TrackerStore store = TrackerStore.Default(path);
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            writer.Write(TrackerStore.latest);
            writer.Write(true);
        }
        return path;
    }

    private static string CreateSingleRecordTrackerFile()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_Farm_Test_Single_{Guid.NewGuid():N}.tkr");

        TrackerStore store = TrackerStore.Default(path);
        Tracker tracker = (Tracker)store.LoadOrCreate(true);

        DateTimeOffset begin = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        tracker.total = 100;
        tracker.heads = 51;
        tracker.tails = 49;
        tracker.anticipated = 50;
        tracker.baseAnticipated = 50;
        tracker.anticipatedHeads = 25;
        tracker.anticipatedTails = 25;
        tracker.betSame = 50;
        tracker.anticipatedSame = 25;
        tracker.wallclockTimeNs = TimeSpan.FromMinutes(10).Ticks * 100;
        tracker.utcBeginTimeMs = begin.ToUnixTimeMilliseconds();
        tracker.utcEndTimeMs = begin.AddMinutes(10).ToUnixTimeMilliseconds();

        store.Save(tracker, true);
        return path;
    }

    private static string CreateTrackerFile()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_Farm_Test_{Guid.NewGuid():N}.tkr");

        TrackerStore store = TrackerStore.Default(path);
        Tracker tracker = (Tracker)store.LoadOrCreate(true);

        DateTimeOffset begin = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

        tracker.total = 100;
        tracker.heads = 51;
        tracker.tails = tracker.total - tracker.heads;
        tracker.anticipated = 50;
        tracker.baseAnticipated = tracker.anticipated;
        tracker.anticipatedHeads = 25;
        tracker.anticipatedTails = 25;
        tracker.betSame = 50;
        tracker.anticipatedSame = 25;
        tracker.wallclockTimeNs = TimeSpan.FromMinutes(10).Ticks * 100;
        tracker.utcBeginTimeMs = begin.ToUnixTimeMilliseconds();
        tracker.utcEndTimeMs = begin.AddMinutes(10).ToUnixTimeMilliseconds();

        store.Save(tracker, true);

        tracker.total = 200;
        tracker.heads = 103;
        tracker.tails = tracker.total - tracker.heads;
        tracker.anticipated = 101;
        tracker.baseAnticipated = tracker.anticipated;
        tracker.anticipatedHeads = 50;
        tracker.anticipatedTails = 51;
        tracker.betSame = 101;
        tracker.anticipatedSame = 51;
        tracker.wallclockTimeNs = TimeSpan.FromMinutes(20).Ticks * 100;
        tracker.utcBeginTimeMs += 10 * 60 * 1000;
        tracker.utcEndTimeMs += 10 * 60 * 1000;

        store.Save(tracker, true);

        tracker.total = 300;
        tracker.heads = 151;
        tracker.tails = tracker.total - tracker.heads;
        tracker.anticipated = 152;
        tracker.baseAnticipated = tracker.anticipated;
        tracker.anticipatedHeads = 100;
        tracker.anticipatedTails = 52;
        tracker.betSame = 153;
        tracker.anticipatedSame = 78;
        tracker.wallclockTimeNs = TimeSpan.FromMinutes(30).Ticks * 100;
        tracker.utcBeginTimeMs += 10 * 60 * 1000;
        tracker.utcEndTimeMs += 10 * 60 * 1000;

        store.Save(tracker, true);

        tracker.total = 400;
        tracker.heads = 198;
        tracker.tails = tracker.total - tracker.heads;
        tracker.anticipated = 203;
        tracker.baseAnticipated = tracker.anticipated;
        tracker.anticipatedHeads = 120;
        tracker.anticipatedTails = 83;
        tracker.betSame = 202;
        tracker.anticipatedSame = 103;
        tracker.wallclockTimeNs = TimeSpan.FromMinutes(40).Ticks * 100;
        tracker.utcBeginTimeMs += 10 * 60 * 1000;
        tracker.utcEndTimeMs += 10 * 60 * 1000;

        store.Save(tracker, true);

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
