using System.Globalization;
using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public class WrapProcessTests
{
    private sealed class ItemRecord
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public int TrialIndex { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public double Factor { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public double AvgPctAAtLeast50 { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public double AvgMeanA { get; set; }
    }

    private sealed class MockChildProcess : FarmProcess
    {
        private readonly IEnumerable<object> _items;
        private readonly bool _throwOnEnumerate;

        public MockChildProcess(IEnumerable<object> items, bool throwOnEnumerate = false)
        {
            _items = items;
            _throwOnEnumerate = throwOnEnumerate;
        }

        public override Type StatType => typeof(ItemRecord);
        public override Type InputType => typeof(ItemRecord);

        protected override IEnumerable<object> EnumerateItems(FarmContext context)
        {
            if (_throwOnEnumerate)
                throw new InvalidOperationException("Simulated child failure");

            foreach (var item in _items)
            {
                yield return item;
            }
        }
    }

    private static MetricCatalogs CreateCatalogs()
    {
        var catalogs = new MetricCatalogs();
        catalogs.Reflect = TruthInTheFlip_Fluent.DefaultReflect;
        return catalogs;
    }

    [Fact]
    public void WrapProcess_Properties_ExposeExpectedTypesAndReferences()
    {
        var child = new MockChildProcess(Array.Empty<object>());
        var wrap = new WrapProcess(child);

        Assert.Same(child, wrap.Child);
        Assert.Same(child, wrap.InputProcess);
        Assert.Equal(typeof(WrapStats), wrap.StatType);
        Assert.Equal(typeof(ItemRecord), wrap.InputType);
    }

    [Fact]
    public void WrapProcess_YieldsExactlyOneOuterItem()
    {
        var items = new List<ItemRecord>
        {
            new() { Factor = 10.0 },
            new() { Factor = 20.0 },
            new() { Factor = 30.0 }
        };

        var child = new MockChildProcess(items);
        var wrap = new WrapProcess(child);

        var catalogs = CreateCatalogs();
        bool ok = wrap.BindFields(catalogs, new[] { "mean#Factor" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<object>();
        wrap.Actions = new ProcessActions(process: (_, item) => produced.Add(item));
        wrap.Execute(new FarmContext());

        Assert.Single(produced);
        Assert.IsType<WrapStats>(produced[0]);
    }

    [Fact]
    public void WrapProcess_MeanAndMax_AggregatesChildValuesCorrectly()
    {
        var items = new List<ItemRecord>
        {
            new() { Factor = 10.0, AvgMeanA = 48.0 },
            new() { Factor = 20.0, AvgMeanA = 52.0 },
            new() { Factor = 30.0, AvgMeanA = 50.0 }
        };

        var child = new MockChildProcess(items);
        var wrap = new WrapProcess(child);

        var catalogs = CreateCatalogs();
        bool ok = wrap.BindFields(catalogs, new[] { "mean#Factor", "max#Factor", "min#Factor", "sum#Factor" }, out var error);
        Assert.True(ok, error?.ToString());

        WrapStats? resultStats = null;
        wrap.Actions = new ProcessActions(process: (_, item) => resultStats = (WrapStats)item);
        wrap.Execute(new FarmContext());

        Assert.NotNull(resultStats);
        var session = wrap.session_get();
        var projection = wrap.projection_get();

        double mean = (double)projection.Fields[0].Get(session, resultStats);
        double max = (double)projection.Fields[1].Get(session, resultStats);
        double min = (double)projection.Fields[2].Get(session, resultStats);
        double sum = (double)projection.Fields[3].Get(session, resultStats);

        Assert.Equal(20.0, mean);
        Assert.Equal(30.0, max);
        Assert.Equal(10.0, min);
        Assert.Equal(60.0, sum);
    }

    [Fact]
    public void WrapProcess_NumericCompatibility_IntegerMetricCanBeAggregatedWithMaxAndMean()
    {
        var items = new List<ItemRecord>
        {
            new() { TrialIndex = 0 },
            new() { TrialIndex = 1 },
            new() { TrialIndex = 2 },
            new() { TrialIndex = 9 }
        };

        var child = new MockChildProcess(items);
        var wrap = new WrapProcess(child);

        var catalogs = CreateCatalogs();
        bool ok = wrap.BindFields(catalogs, new[] { "max#TrialIndex", "mean#TrialIndex" }, out var error);
        Assert.True(ok, error?.ToString());

        WrapStats? resultStats = null;
        wrap.Actions = new ProcessActions(process: (_, item) => resultStats = (WrapStats)item);
        wrap.Execute(new FarmContext());

        Assert.NotNull(resultStats);
        var session = wrap.session_get();
        var projection = wrap.projection_get();

        double maxTrial = (double)projection.Fields[0].Get(session, resultStats);
        double meanTrial = (double)projection.Fields[1].Get(session, resultStats);

        Assert.Equal(9.0, maxTrial);
        Assert.Equal(3.0, meanTrial);
    }

    [Fact]
    public void WrapProcess_MotivatingValues_CalculatesMeanApproximately49_6148()
    {
        var motivatingValues = new double[]
        {
            49.32514880952381,
            47.203125,
            50.88392857142857,
            49.921875,
            50.979910714285715,
            49.997767857142854,
            52.15029761904761,
            48.76860119047619,
            48.55357142857143,
            48.363839285714285
        };

        var items = motivatingValues.Select(v => new ItemRecord { AvgPctAAtLeast50 = v }).ToList();

        var child = new MockChildProcess(items);
        var wrap = new WrapProcess(child);

        var catalogs = CreateCatalogs();
        bool ok = wrap.BindFields(catalogs, new[] { "mean#AvgPctAAtLeast50" }, out var error);
        Assert.True(ok, error?.ToString());

        WrapStats? resultStats = null;
        wrap.Actions = new ProcessActions(process: (_, item) => resultStats = (WrapStats)item);
        wrap.Execute(new FarmContext());

        Assert.NotNull(resultStats);
        var session = wrap.session_get();
        var projection = wrap.projection_get();

        double meanResult = (double)projection.Fields[0].Get(session, resultStats);

        // Motivating reference expectation: approximately 49.6148
        Assert.Equal(49.6148, meanResult, precision: 4);
    }

    [Fact]
    public void WrapProcess_PreservesAndRestoresChildActions_OnSuccessfulExecution()
    {
        var items = new List<ItemRecord>
        {
            new() { Factor = 1.0 },
            new() { Factor = 2.0 }
        };

        bool childBeginCalled = false;
        bool childEndCalled = false;
        var childProcessedItems = new List<object>();

        var originalChildActions = new ProcessActions(
            begin: _ => childBeginCalled = true,
            process: (_, item) => childProcessedItems.Add(item),
            end: _ => childEndCalled = true
        );

        var child = new MockChildProcess(items)
        {
            Actions = originalChildActions
        };

        var wrap = new WrapProcess(child);
        var catalogs = CreateCatalogs();
        wrap.BindFields(catalogs, new[] { "sum#Factor" }, out _);

        wrap.Execute(new FarmContext());

        // Child lifecycle callbacks were executed
        Assert.True(childBeginCalled);
        Assert.True(childEndCalled);
        Assert.Equal(2, childProcessedItems.Count);

        // Child Actions property is restored to the original instance
        Assert.Same(originalChildActions, child.Actions);
    }

    [Fact]
    public void WrapProcess_PropagatesChildExceptionAndRestoresActions()
    {
        bool childAbortCalled = false;
        Exception? caughtAbortEx = null;

        var originalChildActions = new ProcessActions(
            abort: (_, ex) =>
            {
                childAbortCalled = true;
                caughtAbortEx = ex;
            }
        );

        var child = new MockChildProcess(Array.Empty<object>(), throwOnEnumerate: true)
        {
            Actions = originalChildActions
        };

        var wrap = new WrapProcess(child);
        var catalogs = CreateCatalogs();
        wrap.BindFields(catalogs, new[] { "sum#Factor" }, out _);

        bool wrapAbortCalled = false;
        wrap.Actions = new ProcessActions(abort: (_, _) => wrapAbortCalled = true);

        var ex = Assert.Throws<InvalidOperationException>(() => wrap.Execute(new FarmContext()));
        Assert.Equal("Simulated child failure", ex.Message);

        Assert.True(childAbortCalled);
        Assert.Same(ex, caughtAbortEx);
        Assert.True(wrapAbortCalled);

        // Child actions reference must be restored even after exception
        Assert.Same(originalChildActions, child.Actions);
    }

    [Fact]
    public void PrettyWrap_FormattingE2E_OutputsFormattedSingleRecord()
    {
        var items = new List<ItemRecord>
        {
            new() { TrialIndex = 0, Factor = 10.0, AvgMeanA = 48.0, AvgPctAAtLeast50 = 49.3251488 },
            new() { TrialIndex = 1, Factor = 20.0, AvgMeanA = 52.0, AvgPctAAtLeast50 = 50.8839285 }
        };

        var child = new MockChildProcess(items);
        var wrap = new WrapProcess(child);

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        var env = new FluentEnvironment();
        env.AddModule<TruthInTheFlip_Fluent>();

        using var scope = FluentEnvironmentScope.Enter(env);

        var cmd = TruthInTheFlip_Fluent.pretty(wrap, "max#TrialIndex", "mean#Factor", "mean#AvgPctAAtLeast50");

        var context = new FarmContext
        {
            Output = output,
            ErrorOutput = error
        };

        cmd.Execute(context);

        string result = output.ToString();
        Assert.Contains("[1/1]", result);
        Assert.Contains("max#TrialIndex = 1", result);
        Assert.Contains("mean#Factor = 15", result);
        Assert.Contains("mean#AvgPctAAtLeast50 = 50.10453865", result);
    }

    [Fact]
    public void CsvWrap_FormattingE2E_OutputsCsvHeaderAndSingleDataRow()
    {
        var items = new List<ItemRecord>
        {
            new() { TrialIndex = 0, Factor = 10.0 },
            new() { TrialIndex = 1, Factor = 20.0 }
        };

        var child = new MockChildProcess(items);
        var wrap = new WrapProcess(child);

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var env = new FluentEnvironment();
        env.AddModule<TruthInTheFlip_Fluent>();

        using var scope = FluentEnvironmentScope.Enter(env);

        var cmd = TruthInTheFlip_Fluent.csv(wrap, "max#TrialIndex", "mean#Factor");
        cmd.Execute(new FarmContext { Output = output });

        string[] lines = output.ToString().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("max#TrialIndex,mean#Factor", lines[0]);
        Assert.Equal("1,15", lines[1]);
    }

    [Fact]
    public void JsonWrap_FormattingE2E_OutputsSingleJsonLine()
    {
        var items = new List<ItemRecord>
        {
            new() { TrialIndex = 0, Factor = 10.0 },
            new() { TrialIndex = 1, Factor = 20.0 }
        };

        var child = new MockChildProcess(items);
        var wrap = new WrapProcess(child);

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var env = new FluentEnvironment();
        env.AddModule<TruthInTheFlip_Fluent>();

        using var scope = FluentEnvironmentScope.Enter(env);

        var cmd = TruthInTheFlip_Fluent.json(wrap, "max#TrialIndex", "mean#Factor");
        cmd.Execute(new FarmContext { Output = output });

        string json = output.ToString().Trim();
        Assert.StartsWith("{", json);
        Assert.EndsWith("}", json);
        Assert.Contains("\"max#TrialIndex\":1", json);
        Assert.Contains("\"mean#Factor\":15", json);
    }

    [Fact]
    public void FluentParsing_WrapCommand_ComposesWithPrettyAndNullTrials()
    {
        string trackerPath = CreateTrackerFile();
        try
        {
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using var error = new StringWriter(CultureInfo.InvariantCulture);

            var env = new FluentEnvironment();
            env.AddModule<TruthInTheFlip_Fluent>();
            env.ServeTypes = new[] { typeof(FarmCommand) };

            string[] args = new[]
            {
                "pretty",
                "wrap",
                "null_trials", "3", "123456",
                "same_persistence_algorithmic",
                "file", trackerPath,
                "10B",
                "by_total", "10B",
                "by_total", "10B",
                "max#TrialIndex",
                "mean#AvgMeanA",
                "mean#AvgPctAAtLeast50"
            };

            int cursor = 0;
            var res = env.ParseOne(args, ref cursor);
            Assert.NotNull(res?.Result);
            Assert.Equal(args.Length, cursor);

            var cmd = (FarmCommand)res.Result;
            var context = new FarmContext
            {
                Output = output,
                ErrorOutput = error
            };

            cmd.Execute(context);

            string text = output.ToString();
            Assert.Contains("[1/1]", text);
            Assert.Contains("max#TrialIndex = 2", text);
            Assert.Contains("mean#AvgMeanA =", text);
            Assert.Contains("mean#AvgPctAAtLeast50 =", text);
        }
        finally
        {
            File.Delete(trackerPath);
        }
    }

    private static string CreateTrackerFile()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_WrapTest_{Guid.NewGuid():N}.tkr");

        TrackerStore store = TrackerStore.Default(path);
        Tracker tracker = (Tracker)store.LoadOrCreate(true);

        DateTimeOffset begin = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

        for (int i = 1; i <= 5; i++)
        {
            tracker.total = i * 100;
            tracker.heads = i * 50;
            tracker.tails = i * 50;
            tracker.anticipated = i * 50;
            tracker.wallclockTimeNs = TimeSpan.FromMinutes(i * 10).Ticks * 100;
            tracker.utcBeginTimeMs = begin.ToUnixTimeMilliseconds();
            tracker.utcEndTimeMs = begin.AddMinutes(i * 10).ToUnixTimeMilliseconds();
            store.Save(tracker, true);
        }

        return path;
    }
}
