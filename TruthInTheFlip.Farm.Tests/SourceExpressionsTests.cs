using System.Reflection;
using JWCFarm;
using JWCFarm.Metrics;

namespace TruthInTheFlip.Farm.Tests;

/// <summary>
/// Tests for the SourceExpressions / MetricEvaluationContext extension seam.
///
/// Design rule:
///   declare dependencies during bind  (SourceExpressions)
///   consume dependencies during eval  (ctx.Get&lt;T&gt;("expr"))
/// </summary>
public class SourceExpressionsTests
{
    // ── fake domain types ──────────────────────────────────────────────────────

    /// <summary>Pretend Tracker – carries the raw metrics.</summary>
    private sealed class FakeTracker
    {
        public double ZScore     { get; init; }
        public double ZScoreHeads { get; init; }
    }

    /// <summary>Pretend Segment – an identity object used as the aggregate key.</summary>
    private sealed class FakeSegment { }

    // ── mock FarmProcess ───────────────────────────────────────────────────────

    private sealed class MockProcess : FarmProcess
    {
        private readonly Type _statType;
        private readonly Type _inputType;
        private readonly FarmProcess? _inputProcess;

        public MockProcess(Type statType, Type inputType, FarmProcess? inputProcess = null)
        {
            _statType     = statType;
            _inputType    = inputType;
            _inputProcess = inputProcess;
        }

        public override Type StatType  => _statType;
        public override Type InputType => _inputType;
        public override FarmProcess? InputProcess => _inputProcess;

        protected override IEnumerable<object> EnumerateItems(FarmContext context)
            => throw new NotSupportedException("MockProcess is for binding/projection tests only.");
    }

    // ── catalog helpers ────────────────────────────────────────────────────────

    private static MetricDescriptor Prop(
        string name, Type valueType,
        Func<MetricEvaluationContext, object, object?> getter,
        IReadOnlyList<string>? sourceExpressions = null)
        => new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = name,
            ValueType = valueType,
            Help = name,
            Getter = getter,
            SourceExpressions = sourceExpressions
        };

    private static MetricDescriptor AggMethod(
        string name, Type valueType,
        Func<MetricEvaluationContext, object, object?[], object?> invoke,
        IReadOnlyList<string>? sourceExpressions = null)
        => new MetricDescriptor(
            name, valueType,
            new List<MetricParameterDescriptor>
            {
                new MetricParameterDescriptor("values", MetricParameterType.Aggregate)
            },
            name,
            invoke)
        {
            SourceExpressions = sourceExpressions
        };

    private static MetricDescriptor ScalarMethod(
        string name, Type valueType,
        Func<MetricEvaluationContext, object, object?[], object?> invoke)
        => new MetricDescriptor(
            name, valueType,
            new List<MetricParameterDescriptor>
            {
                new MetricParameterDescriptor("value", MetricParameterType.Scalar)
            },
            name,
            invoke);

    // Catalogs with:
    //   FakeTracker: ZScore, ZScoreHeads, abs (scalar), mean (aggregate)
    //   FakeSegment: (extended by individual tests)
    private static MetricCatalogs CreateBaseCatalogs(
        Dictionary<string, MetricDescriptor>? segmentExtra = null)
    {
        var cats = new MetricCatalogs();

        cats.Catalogs[typeof(FakeTracker)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["ZScore"]      = Prop("ZScore",      typeof(double), (_, t) => ((FakeTracker)t).ZScore),
                ["ZScoreHeads"] = Prop("ZScoreHeads", typeof(double), (_, t) => ((FakeTracker)t).ZScoreHeads),
                ["abs"]         = ScalarMethod("abs", typeof(double),
                    (_, _, args) => Math.Abs((double)args[0]!)),
                ["mean"]        = AggMethod("mean", typeof(double),
                    (_, _, args) => ((List<double>)args[0]!).DefaultIfEmpty(double.NaN).Average()),
            }
        };

        var segMetrics = new Dictionary<string, MetricDescriptor>();
        if (segmentExtra != null)
            foreach (var kv in segmentExtra) segMetrics[kv.Key] = kv.Value;

        // add aggregate functions to segment catalog too
        segMetrics["mean"] = AggMethod("mean", typeof(double),
            (_, _, args) => ((List<double>)args[0]!).DefaultIfEmpty(double.NaN).Average());
        segMetrics["abs"]  = ScalarMethod("abs", typeof(double),
            (_, _, args) => Math.Abs((double)args[0]!));

        cats.Catalogs[typeof(FakeSegment)] = new MetricCatalog { Metrics = segMetrics };
        return cats;
    }

    // ── acceptance test 1: flat property SourceExpressions ────────────────────

    [Fact]
    public void TrueZ2_FlatPropertyDependencies_MatchDirectCalculation()
    {
        // TrueZ2 mirrors ZScore - abs(ZScoreHeads) via two declared sources.
        var trueZ2 = Prop("TrueZ2", typeof(double),
            (ctx, _) =>
            {
                double z  = ctx.Get<double>("ZScore");
                double zh = ctx.Get<double>("ZScoreHeads");
                return z - Math.Abs(zh);
            },
            sourceExpressions: ["ZScore", "ZScoreHeads"]);

        var cats = CreateBaseCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["TrueZ2"] = trueZ2;

        var process = new MockProcess(typeof(FakeTracker), typeof(FakeTracker));

        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "TrueZ2");

        Assert.True(ok);
        Assert.NotNull(projection);

        var tracker = new FakeTracker { ZScore = 1.5, ZScoreHeads = -0.3 };

        // Dependencies must be bound but NOT appear as output fields.
        Assert.Single(projection!.Fields);
        Assert.False(projection.ContainsDependency("TrueZ2"));
        Assert.True(projection.ContainsDependency("ZScore"));
        Assert.True(projection.ContainsDependency("ZScoreHeads"));

        double result = (double)projection.Fields[0].Get(projection, tracker);
        double expected = tracker.ZScore - Math.Abs(tracker.ZScoreHeads);

        Assert.Equal(expected, result, precision: 10);
    }

    [Fact]
    public void TrueZ2_Dependencies_NotInOutputFields()
    {
        var trueZ2 = Prop("TrueZ2", typeof(double),
            (ctx, _) => ctx.Get<double>("ZScore") - Math.Abs(ctx.Get<double>("ZScoreHeads")),
            sourceExpressions: ["ZScore", "ZScoreHeads"]);

        var cats = CreateBaseCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["TrueZ2"] = trueZ2;

        var process = new MockProcess(typeof(FakeTracker), null);

        MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "TrueZ2");

        // Only "TrueZ2" is a field; sources are hidden in Dependencies.
        Assert.Equal(1, projection!.Fields.Count);
        Assert.Equal("TrueZ2", projection.Fields[0].ToString());
        Assert.DoesNotContain("ZScore", projection.Fields.Select(f => f.ToString()));
    }

    // ── acceptance test 2: nested function SourceExpression ───────────────────

    [Fact]
    public void MeanAbsHeads_NestedFunctionDependency_BindsAndEvaluates()
    {
        // The external metric advertises a nested function expression as its source.
        var meanAbsHeads = Prop("MeanAbsHeads", typeof(double),
            (ctx, _) => ctx.Get<double>("mean#abs#ZScoreHeads"),
            sourceExpressions: ["mean#abs#ZScoreHeads"]);

        var cats = CreateBaseCatalogs(new Dictionary<string, MetricDescriptor>
        {
            ["MeanAbsHeads"] = meanAbsHeads
        });

        // Segment process: StatType=FakeSegment, InputType=FakeTracker, no InputProcess
        var process = new MockProcess(typeof(FakeSegment), typeof(FakeTracker));
        process.Projection = new MetricProjection(); // needed for InputProcess.Projection access

        bool ok = MetricBinder.Bind(process, cats, typeof(FakeSegment), typeof(FakeTracker),
            out var projection, out _, "MeanAbsHeads");

        Assert.True(ok);
        Assert.NotNull(projection);
        Assert.True(projection!.ContainsDependency("mean#abs#ZScoreHeads"),
            "Hidden dependency for 'mean#abs#ZScoreHeads' must be bound.");
        Assert.Equal(1, projection.Fields.Count);

        // Simulate Inspect calls for tracker records in the segment.
        var segment  = new FakeSegment();
        var tracker1 = new FakeTracker { ZScore = 1.0, ZScoreHeads = -0.4 };
        var tracker2 = new FakeTracker { ZScore = 0.8, ZScoreHeads =  0.2 };
        var tracker3 = new FakeTracker { ZScore = 1.2, ZScoreHeads = -0.6 };

        projection.Inspect(process, segment, tracker1);
        projection.Inspect(process, segment, tracker2);
        projection.Inspect(process, segment, tracker3);

        double result = (double)projection.Fields[0].Get(projection, segment);

        double expected =
            (Math.Abs(tracker1.ZScoreHeads) +
             Math.Abs(tracker2.ZScoreHeads) +
             Math.Abs(tracker3.ZScoreHeads)) / 3.0;

        Assert.Equal(expected, result, precision: 10);
    }

    // ── deduplication: same source expression in two fields ───────────────────

    [Fact]
    public void DuplicateSourceExpressions_BoundOnlyOnce()
    {
        var metr1 = Prop("M1", typeof(double),
            (ctx, _) => ctx.Get<double>("ZScore"),
            sourceExpressions: ["ZScore"]);

        var metr2 = Prop("M2", typeof(double),
            (ctx, _) => ctx.Get<double>("ZScore"),
            sourceExpressions: ["ZScore"]);

        var cats = CreateBaseCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["M1"] = metr1;
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["M2"] = metr2;

        var process = new MockProcess(typeof(FakeTracker), null);
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "M1", "M2");

        Assert.True(ok);
        Assert.True(projection!.ContainsDependency("ZScore"));
        // There should be exactly one bound path for "ZScore".
        Assert.Equal(1, projection.Dependencies.Count);
    }

    // ── cycle detection ───────────────────────────────────────────────────────

    [Fact]
    public void CycleDetection_SelfCycle_ReturnsBoundError()
    {
        var foo = Prop("Foo", typeof(double),
            (ctx, _) => ctx.Get<double>("Foo"),
            sourceExpressions: ["Foo"]);

        var cats = CreateBaseCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["Foo"] = foo;

        var process = new MockProcess(typeof(FakeTracker), null);
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out _, out var error, "Foo");

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("cycle", error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Foo", error.Message);
    }

    [Fact]
    public void CycleDetection_TransitiveCycle_ReturnsBoundError()
    {
        // Foo depends on Bar, Bar depends on Foo.
        var foo = Prop("Foo", typeof(double),
            (ctx, _) => ctx.Get<double>("Bar"),
            sourceExpressions: ["Bar"]);

        var bar = Prop("Bar", typeof(double),
            (ctx, _) => ctx.Get<double>("Foo"),
            sourceExpressions: ["Foo"]);

        var cats = CreateBaseCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["Foo"] = foo;
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["Bar"] = bar;

        var process = new MockProcess(typeof(FakeTracker), null);
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out _, out var error, "Foo");

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("cycle", error!.Message, StringComparison.OrdinalIgnoreCase);
        // The chain must mention both names.
        Assert.Contains("Foo", error.Message);
        Assert.Contains("Bar", error.Message);
    }

    // ── unadvertised source access throws clearly ─────────────────────────────

    [Fact]
    public void Get_UnadvertisedExpression_ThrowsKeyNotFound()
    {
        // Getter calls ctx.Get for an expression NOT in SourceExpressions.
        var metr = Prop("Bad", typeof(double),
            (ctx, _) => ctx.Get<double>("ZScore"), // not declared
            sourceExpressions: null);

        var cats = CreateBaseCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["Bad"] = metr;

        var process = new MockProcess(typeof(FakeTracker), null);
        MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "Bad");

        var tracker = new FakeTracker { ZScore = 1.0 };

        Assert.Throws<KeyNotFoundException>(
            () => projection!.Fields[0].Get(projection, tracker));
    }

    // ── canonical string key ──────────────────────────────────────────────────

    [Fact]
    public void Dependencies_KeyedByCanonicalExpressionString()
    {
        var metr = Prop("M", typeof(double),
            (ctx, _) => ctx.Get<double>("ZScore"),
            sourceExpressions: ["ZScore"]);

        var cats = CreateBaseCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["M"] = metr;

        var process = new MockProcess(typeof(FakeTracker), null);
        MetricBinder.Bind(process, cats, typeof(FakeTracker), null,
            out var projection, out _, "M");

        // The canonical key must exactly match the SourceExpression string.
        Assert.True(projection!.TryGetDependency("ZScore", out var path));
        Assert.Equal("ZScore", path!.ToString());
    }

    // ── aggregate parameter state is kept separate per parameter index ─────────

    [Fact]
    public void AggregateState_ParameterIndependent()
    {
        // pearson#ZScoreHeads,ZScoreHeads — both parameters sample from the same metric,
        // but they must accumulate into independent state slots (arg index 0 and 1).
        // "pearson" must be in the FakeSegment catalog (that is where binding starts).
        var pearsonDesc = new MetricDescriptor(
            "pearson", typeof(double),
            new List<MetricParameterDescriptor>
            {
                new MetricParameterDescriptor("x_values", MetricParameterType.Aggregate),
                new MetricParameterDescriptor("y_values", MetricParameterType.Aggregate),
            },
            "Pearson correlation",
            (_, _, args) =>
            {
                var xs = (List<double>)args[0]!;
                var ys = (List<double>)args[1]!;
                Assert.Equal(xs.Count, ys.Count);
                return 1.0; // result value not the point; state separation is
            });

        // pearson must live in the FakeSegment catalog (bind entry type = FakeSegment).
        var cats = CreateBaseCatalogs(new Dictionary<string, MetricDescriptor>
        {
            ["pearson"] = pearsonDesc
        });

        var process = new MockProcess(typeof(FakeSegment), typeof(FakeTracker));
        process.Projection = new MetricProjection();

        bool ok = MetricBinder.Bind(process, cats, typeof(FakeSegment), typeof(FakeTracker),
            out var projection, out _, "pearson#ZScoreHeads,ZScoreHeads");

        Assert.True(ok);

        var segment  = new FakeSegment();
        var tracker1 = new FakeTracker { ZScoreHeads = 0.5 };
        var tracker2 = new FakeTracker { ZScoreHeads = -0.2 };

        projection!.Inspect(process, segment, tracker1);
        projection.Inspect(process, segment, tracker2);

        // Retrieve the state for arg index 0 and 1 — must both have 2 items.
        var path   = projection.Fields[0];
        var xs     = projection.GetStatValues(segment, path, 0);
        var ys     = projection.GetStatValues(segment, path, 1);

        Assert.Equal(2, xs.Count);
        Assert.Equal(2, ys.Count);
        // They are independent lists (different keys even for same expression).
        Assert.NotSame(xs, ys);
    }

    // ── regression: existing Pearson expression still works ───────────────────

    [Fact]
    public void Regression_PearsonExpressionBindsCorrectly()
    {
        // "pearson" must be in the FakeSegment catalog (binding starts at FakeSegment).
        var cats = CreateBaseCatalogs(new Dictionary<string, MetricDescriptor>
        {
            ["pearson"] = new MetricDescriptor(
                "pearson", typeof(double),
                new List<MetricParameterDescriptor>
                {
                    new MetricParameterDescriptor("x_values", MetricParameterType.Aggregate),
                    new MetricParameterDescriptor("y_values", MetricParameterType.Aggregate),
                },
                "Pearson",
                (_, _, args) => 0.0) // value not tested here
        });

        var process = new MockProcess(typeof(FakeSegment), typeof(FakeTracker));
        process.Projection = new MetricProjection();

        bool ok = MetricBinder.Bind(process, cats, typeof(FakeSegment), typeof(FakeTracker),
            out var projection, out _, "pearson#ZScoreHeads,ZScore");

        Assert.True(ok);
        Assert.Single(projection!.Fields);
        Assert.Equal("pearson#ZScoreHeads,ZScore", projection.Fields[0].ToString());
    }

    // ── regression: nested arity parser (clamp#mean#abs) still works ──────────

    [Fact]
    public void Regression_NestedArityParserBindsCorrectly()
    {
        var cats = CreateBaseCatalogs();
        // add clamp (scalar, 3 params)
        cats.Catalogs[typeof(FakeSegment)]!.Metrics["clamp"] = new MetricDescriptor(
            "clamp", typeof(double),
            new List<MetricParameterDescriptor>
            {
                new("value", MetricParameterType.Scalar),
                new("min",   MetricParameterType.Scalar),
                new("max",   MetricParameterType.Scalar),
            },
            "clamp",
            (_, _, args) => Math.Clamp((double)args[0]!, (double)args[1]!, (double)args[2]!));

        var process = new MockProcess(typeof(FakeSegment), typeof(FakeTracker));
        process.Projection = new MetricProjection();

        // clamp(mean(abs(ZScoreHeads across trackers)), -1, 0)
        bool ok = MetricBinder.Bind(process, cats, typeof(FakeSegment), typeof(FakeTracker),
            out var projection, out _, "clamp#mean#abs#ZScoreHeads,-1,0");

        Assert.True(ok);
        Assert.Single(projection!.Fields);

        var segment  = new FakeSegment();
        var tracker1 = new FakeTracker { ZScoreHeads = -0.3 };
        var tracker2 = new FakeTracker { ZScoreHeads =  0.7 };
        var tracker3 = new FakeTracker { ZScoreHeads = -1.2 };

        projection.Inspect(process, segment, tracker1);
        projection.Inspect(process, segment, tracker2);
        projection.Inspect(process, segment, tracker3);

        double result   = (double)projection.Fields[0].Get(projection, segment);
        double meanAbs  = (Math.Abs(-0.3) + Math.Abs(0.7) + Math.Abs(-1.2)) / 3.0;
        double expected = Math.Clamp(meanAbs, -1.0, 0.0);

        Assert.Equal(expected, result, precision: 10);
    }

    // ── regression: SourceExpression dependency stored in correct projection ────
    //
    // When a descriptor is found inside a nested aggregate descent that crosses
    // an InputProcess boundary (e.g. mean#mean#NamedMethod at segment_agg level),
    // the SourceExpression dependency must be stored in the INNER process's
    // projection — not the outer one — because that is the projection the Getter's
    // MetricEvaluationContext will carry at evaluation time.
    //
    // Reproduces the bug reported when using:
    //   csv segment_agg ... mean#mean#NamedMethod
    // where NamedMethod declares a SourceExpression.

    private sealed class FakeSegmentAgg { }

    [Fact]
    public void SourceExpression_StoredInCorrectProjection_AcrossInputProcessBoundary()
    {
        // "namedMethod" lives in the Tracker catalog and declares a flat source.
        var namedMethod = Prop("namedMethod", typeof(double),
            (ctx, tracker) =>
            {
                double z  = ctx.Get<double>("ZScore");
                double zh = ctx.Get<double>("ZScoreHeads");
                return z - Math.Abs(zh);
            },
            sourceExpressions: ["ZScore", "ZScoreHeads"]);

        var cats = CreateBaseCatalogs();
        cats.Catalogs[typeof(FakeTracker)]!.Metrics["namedMethod"] = namedMethod;

        // Add the SegmentAgg level with "mean" forwarded into it.
        cats.Catalogs[typeof(FakeSegmentAgg)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["mean"] = AggMethod("mean", typeof(double),
                    (_, _, args) => ((List<double>)args[0]!).DefaultIfEmpty(double.NaN).Average())
            }
        };

        // Process hierarchy:
        //   SegmentAggProcess (StatType=FakeSegmentAgg, InputType=FakeSegment)
        //     InputProcess = SegmentProcess (StatType=FakeSegment, InputType=FakeTracker)

        var innerProcess = new MockProcess(typeof(FakeSegment), typeof(FakeTracker));
        innerProcess.Projection = new MetricProjection();

        var outerProcess = new MockProcess(typeof(FakeSegmentAgg), typeof(FakeSegment), innerProcess);

        // mean#mean#namedMethod at FakeSegmentAgg level:
        //   outer mean → descends to FakeSegment (innerProcess)
        //   inner mean → descends to FakeTracker (null process, inputType=FakeTracker)
        //   namedMethod → found at FakeTracker, declares SourceExpressions
        bool ok = MetricBinder.Bind(outerProcess, cats, typeof(FakeSegmentAgg), typeof(FakeSegment),
            out var projection, out var bindError, "mean#mean#namedMethod");

        Assert.True(ok, $"Bind failed: {bindError?.Message}");
        Assert.NotNull(projection);

        // The SourceExpression dependencies must be in innerProcess.Projection,
        // NOT in the outer projection, because the Getter's ctx.Projection at
        // evaluation time is innerProcess.Projection.
        Assert.True(innerProcess.Projection.ContainsDependency("ZScore"),
            "ZScore dependency must be in the inner process projection.");
        Assert.True(innerProcess.Projection.ContainsDependency("ZScoreHeads"),
            "ZScoreHeads dependency must be in the inner process projection.");
        Assert.False(projection!.ContainsDependency("ZScore"),
            "ZScore dependency must NOT be in the outer projection.");

        // Now verify end-to-end evaluation.
        // Simulate: two segment items each containing two tracker records.
        var seg1 = new FakeSegment();
        var t1a  = new FakeTracker { ZScore = 1.0, ZScoreHeads = -0.2 };
        var t1b  = new FakeTracker { ZScore = 0.8, ZScoreHeads =  0.1 };

        var seg2 = new FakeSegment();
        var t2a  = new FakeTracker { ZScore = 1.2, ZScoreHeads = -0.4 };

        // Inner Inspect: for each tracker record inside each segment, accumulate
        // aggregate state into innerProcess.Projection.
        innerProcess.Projection.Inspect(innerProcess, seg1, t1a);
        innerProcess.Projection.Inspect(innerProcess, seg1, t1b);
        innerProcess.Projection.Inspect(innerProcess, seg2, t2a);

        // Outer Inspect: for each segment, compute "mean#namedMethod" and accumulate
        // into the outer projection's aggregate state.
        projection.Inspect(outerProcess, new FakeSegmentAgg(), seg1);
        projection.Inspect(outerProcess, new FakeSegmentAgg(), seg2);

        // Evaluate — just check it does not throw.
        // (The exact numeric value isn't the focus; the projection-target bug would
        // have thrown KeyNotFoundException before we even get here.)
        var agg = new FakeSegmentAgg();

        // Re-run with a single aggregate object to get a retrievable result.
        var agg2       = new FakeSegmentAgg();
        var seg3       = new FakeSegment();
        var t3a        = new FakeTracker { ZScore = 2.0, ZScoreHeads = -1.0 };
        innerProcess.Projection.Inspect(innerProcess, seg3, t3a);
        projection.Inspect(outerProcess, agg2, seg3);

        double trueZ3a = t3a.ZScore - Math.Abs(t3a.ZScoreHeads); // 2.0 - 1.0 = 1.0

        // The outer mean should return the mean of [namedMethod(t3a)] = [1.0] = 1.0.
        double result = (double)projection.Fields[0].Get(projection, agg2);
        Assert.Equal(trueZ3a, result, precision: 10);
    }

    // ── field accessor for MockProcess.Projection (workaround for sealed setter) ──

    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init() { } // ensures the module is initialized
}

