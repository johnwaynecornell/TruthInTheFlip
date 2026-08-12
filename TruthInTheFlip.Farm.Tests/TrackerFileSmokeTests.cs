using System.Globalization;
using FluentCommandLine;
using JWCFarm;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public sealed class TrackerFileSmokeTests
{
    [Fact]
    public void CsvTracker_ReadsGeneratedTrackerFile()
    {
        string path = CreateTrackerFile();

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "file",
                path,
                "absTotal",
                "heads",
                "AnticipatedPercentage",
                "SamePercentage",
                "ZScore");

            string[] lines = NonEmptyLines(csv);

            Assert.Equal(
                "absTotal,heads,AnticipatedPercentage,SamePercentage,ZScore",
                lines[0]);

            Assert.Equal(5, lines.Length); // header + four records
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CsvSegment_ReadsGeneratedTrackerFile()
    {
        string path = CreateTrackerFile();

        try
        {
            string csv = RunFarm(
                "csv",
                "segment",
                "file",
                path,
                "by_total",
                "100",
                "Index",
                "EndTotal",
                "End.AnticipatedPercentage",
                "End.SamePercentage");

            string[] lines = NonEmptyLines(csv);

            Assert.Equal(
                "Index,EndTotal,End.AnticipatedPercentage,End.SamePercentage",
                lines[0]);

            Assert.True(lines.Length > 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Boundary_AbsTotal_From_IsInclusive()
    {
        string path = CreateTrackerFile();

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "from",
                "absTotal",
                "200",
                "file",
                path,
                "absTotal");

            AssertAbsTotals(csv, 200, 300, 400);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Boundary_AbsTotal_To_IsInclusive()
    {
        string path = CreateTrackerFile();

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "to",
                "absTotal",
                "300",
                "file",
                path,
                "absTotal");

            AssertAbsTotals(csv, 100, 200, 300);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Boundary_AbsTotal_FromAndTo_ComposeInclusively()
    {
        string path = CreateTrackerFile();

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "from",
                "absTotal",
                "200",
                "to",
                "absTotal",
                "300",
                "file",
                path,
                "absTotal");

            AssertAbsTotals(csv, 200, 300);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Boundary_AbsWallclock_FromAndTo_AreInclusive()
    {
        string path = CreateTrackerFile();

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "from",
                "absWallclock",
                "00:20:00",
                "to",
                "absWallclock",
                "00:30:00",
                "file",
                path,
                "absTotal");

            AssertAbsTotals(csv, 200, 300);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Boundary_AbsWallclockNs_FromAndTo_AreInclusive()
    {
        string path = CreateTrackerFile();

        try
        {
            long twentyMinutesNs =
                TimeSpan.FromMinutes(20).Ticks * 100;

            long thirtyMinutesNs =
                TimeSpan.FromMinutes(30).Ticks * 100;

            string csv = RunFarm(
                "csv",
                "tracker",
                "from",
                "absWallclockNs",
                twentyMinutesNs.ToString(CultureInfo.InvariantCulture),
                "to",
                "absWallclockNs",
                thirtyMinutesNs.ToString(CultureInfo.InvariantCulture),
                "file",
                path,
                "absTotal");

            AssertAbsTotals(csv, 200, 300);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Boundary_UtcEndTime_FromAndTo_AreInclusive()
    {
        string path = CreateTrackerFile();

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "from",
                "utcEndTime",
                "2026-08-11T12:20:00Z",
                "to",
                "utcEndTime",
                "2026-08-11T12:30:00Z",
                "file",
                path,
                "absTotal");

            AssertAbsTotals(csv, 200, 300);
        }
        finally
        {
            File.Delete(path);
        }
    }
    
    [Fact]
    public void Boundary_UtcBeginTime_FromAndTo_AreInclusive()
    {
        string path = CreateTrackerFile();

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "from",
                "utcBeginTime",
                "2026-08-11T12:10:00Z",
                "to",
                "utcBeginTime",
                "2026-08-11T12:20:00Z",
                "file",
                path,
                "absTotal");

            AssertAbsTotals(csv, 200, 300);
        }
        finally
        {
            File.Delete(path);
        }
    }
    
    [Fact]
    public void Boundary_RangeWithNoRecords_ProducesHeaderOnly()
    {
        string path = CreateTrackerFile();

        try
        {
            string csv = RunFarm(
                "csv",
                "tracker",
                "from",
                "absTotal",
                "500",
                "file",
                path,
                "absTotal");

            string[] lines = NonEmptyLines(csv);

            Assert.Single(lines);
            Assert.Equal("absTotal", lines[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertAbsTotals(
        string csv,
        params long[] expected)
    {
        string[] lines = NonEmptyLines(csv);

        Assert.Equal("absTotal", lines[0]);

        long[] actual = lines
            .Skip(1)
            .Select(line =>
                long.Parse(
                    line,
                    CultureInfo.InvariantCulture))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static string CreateTrackerFile()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_Farm_Test_{Guid.NewGuid():N}.tkr");


        TrackerStore store = TrackerStore.Default(path);
        Tracker tracker = (Tracker)store.LoadOrCreate(true);

        DateTimeOffset begin =
            new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

        tracker.total = 100;
        tracker.heads = 51;
        tracker.tails = tracker.total - tracker.heads;
        tracker.anticipated = 50;
        tracker.baseAnticipated = tracker.anticipated;
        tracker.anticipatedHeads = 25;
        tracker.anticipatedTails = 25;

        tracker.betSame = 50;
        tracker.anticipatedSame = 25;

        tracker.wallclockTimeNs =
            TimeSpan.FromMinutes(10).Ticks * 100;

        tracker.utcBeginTimeMs = begin.ToUnixTimeMilliseconds();
        tracker.utcEndTimeMs =
            begin.AddMinutes(10).ToUnixTimeMilliseconds();

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
        tracker.utcEndTimeMs   += 10 * 60 * 1000;

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
        tracker.utcEndTimeMs   += 10 * 60 * 1000;

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
        tracker.utcEndTimeMs   += 10 * 60 * 1000;

        store.Save(tracker, true);

        return path;
    }

    private static string[] NonEmptyLines(string text)
    {
        return text
            .Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries);
    }

    private static string RunFarm(params string[] args)
    {
        using var output =
            new StringWriter(CultureInfo.InvariantCulture);

        using var error =
            new StringWriter(CultureInfo.InvariantCulture);

        // Adapt these few lines to however Program currently exposes
        // environment creation + execution.
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