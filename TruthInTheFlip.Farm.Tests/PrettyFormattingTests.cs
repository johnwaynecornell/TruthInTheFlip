using System.Globalization;
using FluentCommandLine;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public class PrettyFormattingTests
{
    private sealed class SampleItem
    {
        public double Number { get; init; }
        public string? Text { get; init; }
        public bool Flag { get; init; }
        public DateTime UtcDate { get; init; }
        public DateTimeOffset OffsetDate { get; init; }
        public TimeSpan Duration { get; init; }
        public long Count { get; init; }
    }

    private sealed class TestProcess : FarmProcess
    {
        private readonly IEnumerable<object> items;

        public TestProcess(IEnumerable<object> items)
        {
            this.items = items;
        }

        public override Type StatType => typeof(SampleItem);
        public override Type? InputType => null;

        protected override IEnumerable<object> EnumerateItems(FarmContext context)
            => items;
    }

    [Fact]
    public void PrettyOut_HandlesNull_ReturnsNullString()
    {
        Assert.Equal("null", TruthInTheFlip_Fluent.PrettyOut(null));
    }

    [Fact]
    public void PrettyOut_UsesInvariantCulture_ForNumericTypesUnderDifferentCultures()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.Equal("1234.5", TruthInTheFlip_Fluent.PrettyOut(1234.5d));
            Assert.Equal("1234.5", TruthInTheFlip_Fluent.PrettyOut(1234.5f));
            Assert.Equal("1234.5", TruthInTheFlip_Fluent.PrettyOut(1234.5m));
            Assert.Equal("9876543210", TruthInTheFlip_Fluent.PrettyOut(9876543210L));
            Assert.Equal("42", TruthInTheFlip_Fluent.PrettyOut(42));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void PrettyOut_FormatsBooleans_InLowerCase()
    {
        Assert.Equal("true", TruthInTheFlip_Fluent.PrettyOut(true));
        Assert.Equal("false", TruthInTheFlip_Fluent.PrettyOut(false));
    }

    [Fact]
    public void PrettyOut_FormatsDateTimeAndDateTimeOffset_InUtcIsoFormat()
    {
        DateTime dt = new DateTime(2026, 8, 11, 19, 30, 45, 123, DateTimeKind.Utc);
        DateTimeOffset dto = new DateTimeOffset(2026, 8, 11, 21, 30, 45, 123, TimeSpan.FromHours(2));

        Assert.Equal("2026-08-11T19:30:45.123Z", TruthInTheFlip_Fluent.PrettyOut(dt));
        Assert.Equal("2026-08-11T19:30:45.123Z", TruthInTheFlip_Fluent.PrettyOut(dto));
    }

    [Fact]
    public void PrettyOut_FormatsTimeSpan_InInvariantConstantFormat()
    {
        TimeSpan ts = new TimeSpan(1, 2, 3, 4, 5);
        Assert.Equal("1.02:03:04.0050000", TruthInTheFlip_Fluent.PrettyOut(ts));
    }

    [Fact]
    public void PrettyCommand_FormatsMultipleRecordsAndNulls_Correctly()
    {
        var items = new List<SampleItem>
        {
            new()
            {
                Number = 1.25,
                Text = "first",
                Flag = true,
                Count = 100
            },
            new()
            {
                Number = 2.50,
                Text = null,
                Flag = false,
                Count = 200
            }
        };

        var process = new TestProcess(items);
        var env = new FluentEnvironment();
        var catalogs = new MetricCatalogs();
        var catalog = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>()
        };

        catalog.Metrics["Number"] = new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "Number",
            ValueType = typeof(double),
            Getter = (_, obj) => ((SampleItem)obj).Number
        };
        catalog.Metrics["Text"] = new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "Text",
            ValueType = typeof(string),
            Getter = (_, obj) => ((SampleItem)obj).Text
        };
        catalog.Metrics["Flag"] = new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "Flag",
            ValueType = typeof(bool),
            Getter = (_, obj) => ((SampleItem)obj).Flag
        };
        catalog.Metrics["Count"] = new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "Count",
            ValueType = typeof(long),
            Getter = (_, obj) => ((SampleItem)obj).Count
        };

        catalogs.Reflect = t => t == typeof(SampleItem) ? catalog : null;
        env.Context.Set(catalogs);

        using var scope = FluentEnvironmentScope.Enter(env);

        var cmd = TruthInTheFlip_Fluent.pretty(process, "Number", "Text", "Flag", "Count");

        using var writer = new StringWriter();
        var farmContext = new FarmContext { Output = writer };
        cmd.Execute(farmContext);

        string output = writer.ToString();
        string[] lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(10, lines.Length);
        Assert.Equal("[1/2]", lines[0]);
        Assert.Equal("    Number = 1.25", lines[1]);
        Assert.Equal("    Text = first", lines[2]);
        Assert.Equal("    Flag = true", lines[3]);
        Assert.Equal("    Count = 100", lines[4]);
        Assert.Equal("[2/2]", lines[5]);
        Assert.Equal("    Number = 2.5", lines[6]);
        Assert.Equal("    Text = null", lines[7]);
        Assert.Equal("    Flag = false", lines[8]);
        Assert.Equal("    Count = 200", lines[9]);
    }

    [Fact]
    public void PrettyCommand_EmptyProcess_ProducesNoOutput()
    {
        var process = new TestProcess(Enumerable.Empty<SampleItem>());
        var env = new FluentEnvironment();
        var catalogs = new MetricCatalogs();
        var catalog = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>()
        };

        catalog.Metrics["Number"] = new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "Number",
            ValueType = typeof(double),
            Getter = (_, obj) => ((SampleItem)obj).Number
        };

        catalogs.Reflect = t => t == typeof(SampleItem) ? catalog : null;
        env.Context.Set(catalogs);

        using var scope = FluentEnvironmentScope.Enter(env);

        var cmd = TruthInTheFlip_Fluent.pretty(process, "Number");

        using var writer = new StringWriter();
        var farmContext = new FarmContext { Output = writer };
        cmd.Execute(farmContext);

        Assert.Empty(writer.ToString());
    }

    [Fact]
    public void PrettyCommand_E2E_TrackerFile_FormatsCorrectly()
    {
        string path = CreateTrackerFile();
        try
        {
            string output = RunFarm("pretty", "tracker", "file", path, "total", "heads");
            string[] lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // 4 records in tracker file -> [1/4], 2 fields, [2/4], 2 fields, etc. -> 4 * 3 = 12 lines
            Assert.Equal(12, lines.Length);
            Assert.Equal("[1/4]", lines[0]);
            Assert.Equal("    total = 100", lines[1]);
            Assert.Equal("    heads = 51", lines[2]);
            Assert.Equal("[2/4]", lines[3]);
            Assert.Equal("    total = 200", lines[4]);
            Assert.Equal("    heads = 103", lines[5]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTrackerFile()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_Farm_PrettyTest_{Guid.NewGuid():N}.tkr");

        TrackerStore store = TrackerStore.Default(path);
        Tracker tracker = (Tracker)store.LoadOrCreate(true);

        DateTimeOffset begin = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

        tracker.total = 100;
        tracker.heads = 51;
        tracker.tails = 49;
        tracker.anticipated = 50;
        tracker.wallclockTimeNs = TimeSpan.FromMinutes(10).Ticks * 100;
        tracker.utcBeginTimeMs = begin.ToUnixTimeMilliseconds();
        tracker.utcEndTimeMs = begin.AddMinutes(10).ToUnixTimeMilliseconds();
        store.Save(tracker, true);

        tracker.total = 200;
        tracker.heads = 103;
        tracker.tails = 97;
        tracker.anticipated = 101;
        tracker.wallclockTimeNs = TimeSpan.FromMinutes(20).Ticks * 100;
        tracker.utcBeginTimeMs += 10 * 60 * 1000;
        tracker.utcEndTimeMs += 10 * 60 * 1000;
        store.Save(tracker, true);

        tracker.total = 300;
        tracker.heads = 151;
        tracker.tails = 149;
        tracker.anticipated = 152;
        tracker.wallclockTimeNs = TimeSpan.FromMinutes(30).Ticks * 100;
        tracker.utcBeginTimeMs += 10 * 60 * 1000;
        tracker.utcEndTimeMs += 10 * 60 * 1000;
        store.Save(tracker, true);

        tracker.total = 400;
        tracker.heads = 198;
        tracker.tails = 202;
        tracker.anticipated = 203;
        tracker.wallclockTimeNs = TimeSpan.FromMinutes(40).Ticks * 100;
        tracker.utcBeginTimeMs += 10 * 60 * 1000;
        tracker.utcEndTimeMs += 10 * 60 * 1000;
        store.Save(tracker, true);

        return path;
    }

    private static string RunFarm(params string[] args)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        FluentEnvironment env = new();
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
