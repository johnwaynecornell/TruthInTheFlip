using System.Globalization;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public class NumericWideningAndTypeCompatibilityTests
{
    // ── test record models ────────────────────────────────────────────────────

    public sealed class ItemRecord
    {
        [IsMetric("1.0")]
        public string Name { get; init; } = "";

        [IsMetric("1.0")]
        public int IntCount { get; init; }

        [IsMetric("1.0")]
        public short ShortVal { get; init; }

        [IsMetric("1.0")]
        public byte ByteVal { get; init; }

        [IsMetric("1.0")]
        public float FloatVal { get; init; }

        [IsMetric("1.0")]
        public double DoubleVal { get; init; }

        [IsMetric("1.0")]
        public DateTime DateVal { get; init; }

        [IsMetric("1.0")]
        public object? DynVal { get; init; }
    }

    public sealed class ContainerRecord
    {
        [IsMetric("1.0")]
        public int Count { get; init; }

        [IsMetric("1.0")]
        public float Weight { get; init; }

        [IsMetric("1.0")]
        public string Title { get; init; } = "";

        [IsMetric("1.0")]
        public DateTime CreatedAt { get; init; }

        // Scalar methods
        [IsMetric("1.0")]
        public double MultiplyDouble(double x, double factor) => x * factor;

        [IsMetric("1.0")]
        public double AddDouble(double a, double b) => a + b;

        [IsMetric("1.0")]
        public long MultiplyLong(long a, long b) => a * b;

        [IsMetric("1.0")]
        public int AddInt(int a, int b) => a + b;

        // Aggregate methods
        [IsMetric("1.0")]
        public double MeanDouble(List<double> values) => values.Count == 0 ? 0.0 : values.Average();

        [IsMetric("1.0")]
        public long SumLong(List<long> values) => values.Sum();

        [IsMetric("1.0")]
        public int SumInt(List<int> values) => values.Sum();
    }

    private sealed class MockProcess : FarmProcess
    {
        private readonly Type _statType;
        private readonly Type _inputType;
        private readonly FarmProcess? _inputProcess;
        private readonly IEnumerable<object>? _items;

        public MockProcess(Type statType, Type inputType, FarmProcess? inputProcess = null, IEnumerable<object>? items = null)
        {
            _statType = statType;
            _inputType = inputType;
            _inputProcess = inputProcess;
            _items = items;
        }

        public override Type StatType => _statType;
        public override Type InputType => _inputType;
        public override FarmProcess? InputProcess => _inputProcess;

        protected override IEnumerable<object> EnumerateItems(FarmContext context)
            => _items ?? Enumerable.Empty<object>();
    }

    private static MetricCatalogs CreateCatalogs()
    {
        var catalogs = new MetricCatalogs();
        catalogs.Reflect = TruthInTheFlip_Fluent.DefaultReflect;
        return catalogs;
    }

    // ── 1. Scalar Numeric Widening Tests ──────────────────────────────────────

    [Fact]
    public void Scalar_IntPropertyToDoubleParameter_BindsAndWidensCorrectly()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // MultiplyDouble takes (double x, double factor). Count is int.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "MultiplyDouble#Count,2.5");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord { Count = 4 };

        var result = (double)projection.Fields[0].Get(session, container);
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void Scalar_FloatPropertyToDoubleParameter_BindsAndWidensCorrectly()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // MultiplyDouble takes (double x, double factor). Weight is float.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "MultiplyDouble#Weight,2.0");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord { Weight = 3.5f };

        var result = (double)projection.Fields[0].Get(session, container);
        Assert.Equal(7.0, result);
    }

    [Fact]
    public void Scalar_IntPropertyToLongParameter_BindsAndWidensCorrectly()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // MultiplyLong takes (long a, long b). Count is int, literal is 5.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "MultiplyLong#Count,5");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord { Count = 6 };

        var result = (long)projection.Fields[0].Get(session, container);
        Assert.Equal(30L, result);
    }

    // ── 2. Aggregate Numeric Widening Tests ───────────────────────────────────

    [Fact]
    public void Aggregate_IntPropertyToListDouble_BindsAndWidensCorrectly()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // MeanDouble expects List<double>, IntCount produces int.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "MeanDouble#IntCount");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        session.Inspect(process, container, new ItemRecord { IntCount = 10 });
        session.Inspect(process, container, new ItemRecord { IntCount = 20 });
        session.Inspect(process, container, new ItemRecord { IntCount = 30 });

        var result = (double)projection.Fields[0].Get(session, container);
        Assert.Equal(20.0, result);
    }

    [Fact]
    public void Aggregate_FloatPropertyToListDouble_BindsAndWidensCorrectly()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // MeanDouble expects List<double>, FloatVal produces float.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "MeanDouble#FloatVal");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        session.Inspect(process, container, new ItemRecord { FloatVal = 1.5f });
        session.Inspect(process, container, new ItemRecord { FloatVal = 4.5f });

        var result = (double)projection.Fields[0].Get(session, container);
        Assert.Equal(3.0, result);
    }

    [Fact]
    public void Aggregate_ShortAndBytePropertiesToListInt_BindsAndWidensCorrectly()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // SumInt expects List<int>, ShortVal produces short.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "SumInt#ShortVal");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        session.Inspect(process, container, new ItemRecord { ShortVal = 100 });
        session.Inspect(process, container, new ItemRecord { ShortVal = 200 });

        var result = (int)projection.Fields[0].Get(session, container);
        Assert.Equal(300, result);
    }

    [Fact]
    public void Aggregate_IntPropertyToListLong_BindsAndWidensCorrectly()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // SumLong expects List<long>, IntCount produces int.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "SumLong#IntCount");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        session.Inspect(process, container, new ItemRecord { IntCount = 1000 });
        session.Inspect(process, container, new ItemRecord { IntCount = 2000 });

        var result = (long)projection.Fields[0].Get(session, container);
        Assert.Equal(3000L, result);
    }

    // ── 3. Bind-time Type Validation Error Tests ──────────────────────────────

    [Fact]
    public void BindError_StringPropertyToDoubleScalar_ReportsStructuredError()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // MultiplyDouble expects (double, double), but Title is string.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "MultiplyDouble#Title,2.0");

        Assert.False(ok);
        Assert.Null(projection);
        Assert.NotNull(error);
        Assert.Equal(15, error!.Offset); // "MultiplyDouble#" has length 15
        Assert.Equal(5, error.Length);   // "Title" length
        Assert.Contains("Title", error.Expression);
        Assert.Contains("Double", error.Message);
        Assert.Contains("String", error.Message);
    }

    [Fact]
    public void BindError_DateTimePropertyToIntScalar_ReportsStructuredError()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // AddInt expects (int, int), but CreatedAt is DateTime.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "AddInt#CreatedAt,5");

        Assert.False(ok);
        Assert.Null(projection);
        Assert.NotNull(error);
        Assert.Equal(7, error!.Offset); // "AddInt#" has length 7
        Assert.Equal(9, error.Length);  // "CreatedAt" length
        Assert.Contains("CreatedAt", error.Expression);
        Assert.Contains("Int32", error.Message);
        Assert.Contains("DateTime", error.Message);
    }

    [Fact]
    public void BindError_NarrowingDoubleToIntScalar_ReportsStructuredError()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // AddInt expects (int, int), but Weight is float (narrowing conversion).
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "AddInt#Weight,5");

        Assert.False(ok);
        Assert.Null(projection);
        Assert.NotNull(error);
        Assert.Contains("Int32", error!.Message);
        Assert.Contains("Single", error.Message);
    }

    [Fact]
    public void BindError_StringPropertyToListDoubleAggregate_ReportsStructuredError()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // MeanDouble expects List<double>, but Name produces string.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "MeanDouble#Name");

        Assert.False(ok);
        Assert.Null(projection);
        Assert.NotNull(error);
        Assert.Equal(11, error!.Offset); // "MeanDouble#" length is 11
        Assert.Equal(4, error.Length);   // "Name" length
        Assert.Contains("MeanDouble", error.Message);
        Assert.Contains("Double", error.Message);
        Assert.Contains("String", error.Message);
    }

    [Fact]
    public void BindError_NarrowingDoublePropertyToListIntAggregate_ReportsStructuredError()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // SumInt expects List<int>, but DoubleVal produces double.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "SumInt#DoubleVal");

        Assert.False(ok);
        Assert.Null(projection);
        Assert.NotNull(error);
        Assert.Contains("SumInt", error!.Message);
        Assert.Contains("Int32", error.Message);
        Assert.Contains("Double", error.Message);
    }

    // ── 4. Dynamic & Legacy Passthrough Tests ─────────────────────────────────

    [Fact]
    public void DynamicObjectProperty_PermittedAtBindTime()
    {
        var catalogs = CreateCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // MeanDouble expects List<double>, DynVal is object.
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "MeanDouble#DynVal");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        // At runtime, passing widening ints works:
        session.Inspect(process, container, new ItemRecord { DynVal = 5 });
        session.Inspect(process, container, new ItemRecord { DynVal = 15 });

        var result = (double)projection.Fields[0].Get(session, container);
        Assert.Equal(10.0, result);
    }
}
