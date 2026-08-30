using JWCFarm;
using JWCFarm.Metrics;

namespace TruthInTheFlip.Farm.Tests;

public class MetricEvaluationSessionTests
{
    private sealed class FakeTracker
    {
        public double ZScore { get; init; }
        public double ZScoreHeads { get; init; }
    }

    private sealed class FakeSegment
    {
        public int Id { get; init; }
    }

    private sealed class MockProcess : FarmProcess
    {
        private readonly Type _statType;
        private readonly Type _inputType;
        private readonly FarmProcess? _inputProcess;
        private readonly IEnumerable<object>? _items;

        public MockProcess(Type statType, Type inputType, FarmProcess? inputProcess = null, IEnumerable<object>? items = null)
        {
            _statType     = statType;
            _inputType    = inputType;
            _inputProcess = inputProcess;
            _items        = items;
        }

        public override Type StatType  => _statType;
        public override Type InputType => _inputType;
        public override FarmProcess? InputProcess => _inputProcess;

        protected override IEnumerable<object> EnumerateItems(FarmContext context)
            => _items ?? Enumerable.Empty<object>();
    }

    private sealed class CounterState
    {
        public int Count { get; set; }
        public List<double> History { get; } = new();
    }

    private static MetricCatalogs CreateTestCatalogs()
    {
        var cats = new MetricCatalogs();

        cats.Catalogs[typeof(FakeTracker)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["ZScore"]      = new MetricDescriptor("ZScore", typeof(double), "ZScore", (_, t) => ((FakeTracker)t).ZScore),
                ["ZScoreHeads"] = new MetricDescriptor("ZScoreHeads", typeof(double), "ZScoreHeads", (_, t) => ((FakeTracker)t).ZScoreHeads),
                ["abs"]         = new MetricDescriptor("abs", typeof(double),
                    new List<MetricParameterDescriptor> { new("val", MetricParameterType.Scalar) },
                    "abs", (_, _, args) => Math.Abs((double)args[0]!)),
                ["mean"]        = new MetricDescriptor("mean", typeof(double),
                    new List<MetricParameterDescriptor> { new("values", MetricParameterType.Aggregate) },
                    "mean", (_, _, args) => ((List<double>)args[0]!).DefaultIfEmpty(double.NaN).Average()),
            }
        };

        cats.Catalogs[typeof(FakeSegment)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["abs"]  = new MetricDescriptor("abs", typeof(double),
                    new List<MetricParameterDescriptor> { new("val", MetricParameterType.Scalar) },
                    "abs", (_, _, args) => Math.Abs((double)args[0]!)),
                ["mean"] = new MetricDescriptor("mean", typeof(double),
                    new List<MetricParameterDescriptor> { new("values", MetricParameterType.Aggregate) },
                    "mean", (_, _, args) => ((List<double>)args[0]!).DefaultIfEmpty(double.NaN).Average()),
            }
        };

        return cats;
    }

    // 1. Existing pure metric evaluation still works.
    [Fact]
    public void PureMetricEvaluation_WorksThroughSession()
    {
        var cats = CreateTestCatalogs();
        var process = new MockProcess(typeof(FakeTracker), null);

        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "ZScore", "abs#ZScoreHeads");

        Assert.True(ok);
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var tracker = new FakeTracker { ZScore = 1.5, ZScoreHeads = -0.75 };

        double z = (double)projection!.Fields[0].Get(session, tracker);
        double absZh = (double)projection.Fields[1].Get(session, tracker);

        Assert.Equal(1.5, z);
        Assert.Equal(0.75, absZh);
    }

    // 2. Existing SourceExpressions / hidden dependencies still work.
    [Fact]
    public void SourceExpressions_HiddenDependencies_WorkThroughSession()
    {
        var cats = CreateTestCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["TrueZ"] = new MetricDescriptor(
            "TrueZ", typeof(double), "TrueZ",
            (ctx, _) => ctx.Get<double>("ZScore") - Math.Abs(ctx.Get<double>("ZScoreHeads")),
            sourceExpressions: new[] { "ZScore", "ZScoreHeads" });

        var process = new MockProcess(typeof(FakeTracker), null);
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "TrueZ");

        Assert.True(ok);
        var session = new MetricEvaluationSession(projection!);
        var tracker = new FakeTracker { ZScore = 2.5, ZScoreHeads = -1.0 };

        double trueZ = (double)projection!.Fields[0].Get(session, tracker);
        Assert.Equal(1.5, trueZ);
    }

    // 3. A MetricProjection can be reused for two separate evaluations.
    [Fact]
    public void MetricProjection_CanBeReusedForTwoSeparateEvaluations()
    {
        var cats = CreateTestCatalogs();
        var process = new MockProcess(typeof(FakeTracker), null);

        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "ZScore");

        Assert.True(ok);
        Assert.NotNull(projection);

        // Run evaluation session 1
        var session1 = new MetricEvaluationSession(projection!);
        var tracker1 = new FakeTracker { ZScore = 3.0 };
        Assert.Equal(3.0, (double)projection.Fields[0].Get(session1, tracker1));

        // Run evaluation session 2 with the same projection
        var session2 = new MetricEvaluationSession(projection);
        var tracker2 = new FakeTracker { ZScore = 4.0 };
        Assert.Equal(4.0, (double)projection.Fields[0].Get(session2, tracker2));
    }

    // 4. Session state accumulated during evaluation A is NOT visible in evaluation B.
    [Fact]
    public void SessionState_AccumulatedInSessionA_IsNotVisibleInSessionB()
    {
        var cats = CreateTestCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["SequenceIndex"] = new MetricDescriptor(
            "SequenceIndex", typeof(double), "Sequence counter",
            (ctx, _) =>
            {
                var state = ctx.GetState("seq_counter", () => new CounterState());
                state.Count++;
                return (double)state.Count;
            });

        var process = new MockProcess(typeof(FakeTracker), null);
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "SequenceIndex");

        Assert.True(ok);

        // Session A: evaluate 3 trackers -> counter should reach 3
        var sessionA = new MetricEvaluationSession(projection!);
        var t1 = new FakeTracker();
        var t2 = new FakeTracker();
        var t3 = new FakeTracker();

        Assert.Equal(1.0, (double)projection!.Fields[0].Get(sessionA, t1));
        Assert.Equal(2.0, (double)projection.Fields[0].Get(sessionA, t2));
        Assert.Equal(3.0, (double)projection.Fields[0].Get(sessionA, t3));

        // Session B: evaluate with same projection -> counter should start fresh at 1
        var sessionB = new MetricEvaluationSession(projection);
        Assert.Equal(1.0, (double)projection.Fields[0].Get(sessionB, t1));
        Assert.Equal(2.0, (double)projection.Fields[0].Get(sessionB, t2));
    }

    // 5. Session state IS shared across multiple products within one evaluation.
    [Fact]
    public void SessionState_IsSharedAcrossMultipleProducts_WithinOneSession()
    {
        var cats = CreateTestCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["RunningSum"] = new MetricDescriptor(
            "RunningSum", typeof(double), "Running sum",
            (ctx, t) =>
            {
                var tracker = (FakeTracker)t;
                var state = ctx.GetState("running_sum", () => new CounterState());
                state.History.Add(tracker.ZScore);
                return state.History.Sum();
            });

        var process = new MockProcess(typeof(FakeTracker), null);
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "RunningSum");

        Assert.True(ok);
        var session = new MetricEvaluationSession(projection!);

        var t1 = new FakeTracker { ZScore = 10.0 };
        var t2 = new FakeTracker { ZScore = 20.0 };
        var t3 = new FakeTracker { ZScore = 30.0 };

        Assert.Equal(10.0, (double)projection!.Fields[0].Get(session, t1));
        Assert.Equal(30.0, (double)projection.Fields[0].Get(session, t2));
        Assert.Equal(60.0, (double)projection.Fields[0].Get(session, t3));
    }

    // 6. Different state keys within one session remain independent.
    [Fact]
    public void DifferentStateKeys_WithinOneSession_RemainIndependent()
    {
        var cats = CreateTestCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["Counter1"] = new MetricDescriptor(
            "Counter1", typeof(double), "Counter 1",
            (ctx, _) =>
            {
                var state = ctx.GetState("key_alpha", () => new CounterState());
                return (double)++state.Count;
            });

        cats.Catalogs[typeof(FakeTracker)]!.Metrics["Counter2"] = new MetricDescriptor(
            "Counter2", typeof(double), "Counter 2",
            (ctx, _) =>
            {
                var state = ctx.GetState("key_beta", () => new CounterState());
                state.Count += 10;
                return (double)state.Count;
            });

        var process = new MockProcess(typeof(FakeTracker), null);
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "Counter1", "Counter2");

        Assert.True(ok);
        var session = new MetricEvaluationSession(projection!);
        var t = new FakeTracker();

        // Counter1 -> 1, Counter2 -> 10
        Assert.Equal(1.0, (double)projection!.Fields[0].Get(session, t));
        Assert.Equal(10.0, (double)projection.Fields[1].Get(session, t));

        // Counter1 -> 2, Counter2 -> 20
        Assert.Equal(2.0, (double)projection.Fields[0].Get(session, t));
        Assert.Equal(20.0, (double)projection.Fields[1].Get(session, t));
    }

    // 7. Independently bound paths / configurations do not accidentally share state when using distinct identities.
    [Fact]
    public void IndependentlyBoundPaths_DoNotAccidentallyShareState()
    {
        var cats = CreateTestCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["RollingTrend"] = new MetricDescriptor(
            "RollingTrend", typeof(double),
            new List<MetricParameterDescriptor> { new("window", MetricParameterType.Scalar) },
            "Rolling trend metric",
            (ctx, t, args) =>
            {
                int window = (int)(double)args[0]!;
                // Key state by the parameter / identity to ensure independent windows get independent state
                var state = ctx.GetState($"trend_{window}", () => new CounterState());
                state.History.Add(((FakeTracker)t).ZScore);
                while (state.History.Count > window)
                    state.History.RemoveAt(0);

                return state.History.Average();
            });

        var process = new MockProcess(typeof(FakeTracker), null);
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "RollingTrend#2", "RollingTrend#4");

        Assert.True(ok);
        var session = new MetricEvaluationSession(projection!);

        // Feed values: 10, 20, 30, 40
        var t1 = new FakeTracker { ZScore = 10.0 };
        var t2 = new FakeTracker { ZScore = 20.0 };
        var t3 = new FakeTracker { ZScore = 30.0 };
        var t4 = new FakeTracker { ZScore = 40.0 };

        projection!.Fields[0].Get(session, t1); // Trend#2: [10] -> 10
        projection.Fields[1].Get(session, t1); // Trend#4: [10] -> 10

        projection.Fields[0].Get(session, t2); // Trend#2: [10, 20] -> 15
        projection.Fields[1].Get(session, t2); // Trend#4: [10, 20] -> 15

        projection.Fields[0].Get(session, t3); // Trend#2: [20, 30] -> 25
        projection.Fields[1].Get(session, t3); // Trend#4: [10, 20, 30] -> 20

        double trend2_t4 = (double)projection.Fields[0].Get(session, t4); // Trend#2: [30, 40] -> 35
        double trend4_t4 = (double)projection.Fields[1].Get(session, t4); // Trend#4: [10, 20, 30, 40] -> 25

        Assert.Equal(35.0, trend2_t4);
        Assert.Equal(25.0, trend4_t4);
    }

    // 8. Existing aggregate metrics continue to pass with sessions and do not leak across sessions.
    [Fact]
    public void AggregateMetrics_IsolatedAcrossSessions()
    {
        var cats = CreateTestCatalogs();
        cats.Catalogs[typeof(FakeSegment)]!.Metrics["MeanZ"] = new MetricDescriptor(
            "MeanZ", typeof(double), "Mean Z",
            (ctx, _) => ctx.Get<double>("mean#ZScore"),
            sourceExpressions: new[] { "mean#ZScore" });

        var process = new MockProcess(typeof(FakeSegment), typeof(FakeTracker));
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeSegment), typeof(FakeTracker),
            out var projection, out _, "MeanZ");

        Assert.True(ok);
        Assert.NotNull(projection);

        var segment = new FakeSegment { Id = 1 };

        // Session 1: tracker values 1.0, 2.0, 3.0 -> Mean = 2.0
        var session1 = new MetricEvaluationSession(projection!);
        session1.Inspect(process, segment, new FakeTracker { ZScore = 1.0 });
        session1.Inspect(process, segment, new FakeTracker { ZScore = 2.0 });
        session1.Inspect(process, segment, new FakeTracker { ZScore = 3.0 });
        Assert.Equal(2.0, (double)projection.Fields[0].Get(session1, segment));

        // Session 2 on same projection and same segment object: tracker values 10.0, 20.0 -> Mean = 15.0
        var session2 = new MetricEvaluationSession(projection);
        session2.Inspect(process, segment, new FakeTracker { ZScore = 10.0 });
        session2.Inspect(process, segment, new FakeTracker { ZScore = 20.0 });
        Assert.Equal(15.0, (double)projection.Fields[0].Get(session2, segment));
    }

    // Type mismatch on GetState throws InvalidCastException
    [Fact]
    public void GetState_TypeMismatch_ThrowsInvalidCastException()
    {
        var projection = new MetricProjection();
        var session = new MetricEvaluationSession(projection);

        session.GetState("shared_key", () => new CounterState());

        Assert.Throws<InvalidCastException>(() =>
            session.GetState<List<string>>("shared_key", () => new List<string>()));
    }

    // FarmProcess.Execute creates fresh session per execution
    [Fact]
    public void FarmProcess_Execute_CreatesFreshSessionPerExecution()
    {
        var cats = CreateTestCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["EvalCounter"] = new MetricDescriptor(
            "EvalCounter", typeof(double), "Counter",
            (ctx, _) =>
            {
                var state = ctx.GetState("run_counter", () => new CounterState());
                return (double)++state.Count;
            });

        var items = new List<object>
        {
            new FakeTracker(),
            new FakeTracker()
        };

        var process = new MockProcess(typeof(FakeTracker), null, items: items);
        bool ok = process.BindFields(cats, new[] { "EvalCounter" }, out _);
        Assert.True(ok);

        var results1 = new List<double>();
        process.Actions = new ProcessActions(
            process: (ctx, item) =>
            {
                results1.Add((double)process.Projection!.Fields[0].Get(process.session_get(), item));
            });

        var farmCtx = new FarmContext();
        process.Execute(farmCtx);

        // First run: 1, 2
        Assert.Equal(new double[] { 1.0, 2.0 }, results1);

        // Second run on same process/projection instance: must start fresh at 1, 2
        var results2 = new List<double>();
        process.Actions = new ProcessActions(
            process: (ctx, item) =>
            {
                results2.Add((double)process.Projection!.Fields[0].Get(process.session_get(), item));
            });

        process.Execute(farmCtx);
        Assert.Equal(new double[] { 1.0, 2.0 }, results2);
    }
}
