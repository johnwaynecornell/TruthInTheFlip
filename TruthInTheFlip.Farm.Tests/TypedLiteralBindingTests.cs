using System.Globalization;
using System.Reflection;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public class TypedLiteralBindingTests
{
    public enum PriorityLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public sealed class TestRecord
    {
        [IsMetric("1.0")]
        [StringHelp("Int value")]
        public int IntVal { get; init; } = 42;

        [IsMetric("1.0")]
        [StringHelp("Double value")]
        public double DoubleVal { get; init; } = 3.14;

        [IsMetric("1.0")]
        [StringHelp("Method taking an int scalar")]
        public int TakeInt(int count) => count * 2;

        [IsMetric("1.0")]
        [StringHelp("Method taking a TimeSpan scalar")]
        public double TakeTimeSpan(TimeSpan span) => span.TotalMinutes;

        [IsMetric("1.0")]
        [StringHelp("Method taking DateTime")]
        public int TakeDateTime(DateTime dt) => dt.Year;

        [IsMetric("1.0")]
        [StringHelp("Method taking DateTimeOffset")]
        public int TakeDateTimeOffset(DateTimeOffset dto) => dto.Year;

        [IsMetric("1.0")]
        [StringHelp("Method taking enum")]
        public string TakeEnum(PriorityLevel level) => level.ToString();

        [IsMetric("1.0")]
        [StringHelp("Method taking string")]
        public string TakeString(string text) => $"Hello, {text}";

        [IsMetric("1.0")]
        [StringHelp("Method taking multiple typed scalars")]
        public string MultiParam(string tag, int count, TimeSpan span, PriorityLevel level, double ratio)
            => $"{tag}:{count}:{span.TotalSeconds}:{level}:{ratio.ToString(CultureInfo.InvariantCulture)}";

        [IsMetric("1.0")]
        [StringHelp("Method taking nullable int")]
        public int TakeNullableInt(int? val) => val ?? -1;

        [IsMetric("1.0")]
        [StringHelp("Method taking bool")]
        public string TakeBool(bool flag) => flag ? "YES" : "NO";

        [IsMetric("1.0")]
        [StringHelp("Method taking decimal")]
        public decimal TakeDecimal(decimal dec) => dec * 2;

        [IsMetric("1.0")]
        [StringHelp("Method taking float")]
        public float TakeFloat(float f) => f * 2f;

        [IsMetric("1.0")]
        [StringHelp("Method taking long")]
        public long TakeLong(long l) => l + 1;

        [IsMetric("1.0")]
        [StringHelp("Method taking Guid")]
        public string TakeGuid(Guid g) => g.ToString("D");

        [IsMetric("1.0")]
        [StringHelp("Static metric method taking an int scalar")]
        public static int StaticMultiply(TestRecord receiver, int multiplier) => receiver.IntVal * multiplier;

        [IsMetric("1.0")]
        [StringHelp("Method taking an aggregate")]
        public static double AggregateSum(TestRecord receiver, List<double> values) => values.Sum();
    }

    private static MetricCatalogs CreateReflectedCatalogs()
    {
        var catalogs = new MetricCatalogs();
        catalogs.Reflect = TruthInTheFlip_Fluent.DefaultReflect;
        return catalogs;
    }

    // 1. Existing numeric/double literals continue to bind and evaluate as before.
    [Fact]
    public void DoubleLiterals_ContinueToBindAndEvaluateAsBefore()
    {
        var cats = new MetricCatalogs();
        cats.Catalogs[typeof(TestRecord)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["scale"] = new MetricDescriptor(
                    "scale", typeof(double),
                    new List<MetricParameterDescriptor> { new("factor", MetricParameterType.Scalar) },
                    "scale", (_, _, args) => (double)args[0]! * 10.0)
            }
        };

        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error, "scale#2.5");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord();
        object result = projection!.Fields[0].Get(session, record);

        Assert.Equal(25.0, (double)result);
    }

    // 2. A reflected scalar int parameter receives an integer literal as an int.
    [Fact]
    public void ReflectedScalar_Int_ReceivesIntegerLiteralAsInt()
    {
        var cats = CreateReflectedCatalogs();

        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error, "TakeInt#21");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord();
        object result = projection!.Fields[0].Get(session, record);

        Assert.Equal(42, (int)result);
    }

    // 3. A reflected scalar TimeSpan can receive a literal such as 00:05:00.
    [Fact]
    public void ReflectedScalar_TimeSpan_ReceivesTimeSpanLiteral()
    {
        var cats = CreateReflectedCatalogs();

        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error, "TakeTimeSpan#00:05:00");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord();
        object result = projection!.Fields[0].Get(session, record);

        Assert.Equal(5.0, (double)result);
    }

    // 4. A reflected scalar DateTime or DateTimeOffset can be parsed with invariant semantics.
    [Fact]
    public void ReflectedScalar_DateTimeAndDateTimeOffset_ParsedWithInvariantSemantics()
    {
        var cats = CreateReflectedCatalogs();

        bool okDt = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projDt, out var errDt, "TakeDateTime#2026-09-03T11:00:00");
        Assert.True(okDt, errDt?.ToString());

        bool okDto = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projDto, out var errDto, "TakeDateTimeOffset#2026-09-03T11:00:00+00:00");
        Assert.True(okDto, errDto?.ToString());

        var sessionDt = new MetricEvaluationSession(projDt!);
        var sessionDto = new MetricEvaluationSession(projDto!);
        var record = new TestRecord();

        Assert.Equal(2026, (int)projDt!.Fields[0].Get(sessionDt, record));
        Assert.Equal(2026, (int)projDto!.Fields[0].Get(sessionDto, record));
    }

    // 5. A reflected enum parameter can parse an enum name.
    [Fact]
    public void ReflectedScalar_Enum_ParsesEnumName()
    {
        var cats = CreateReflectedCatalogs();

        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error, "TakeEnum#Critical");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord();
        object result = projection!.Fields[0].Get(session, record);

        Assert.Equal("Critical", (string)result);
    }

    // 6. A reflected string parameter receives the current argument token.
    [Fact]
    public void ReflectedScalar_String_ReceivesCurrentArgumentToken()
    {
        var cats = CreateReflectedCatalogs();

        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error, "TakeString#World");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord();
        object result = projection!.Fields[0].Get(session, record);

        Assert.Equal("Hello, World", (string)result);
    }

    // 7. If typed parsing fails, a scalar parameter can still bind an existing metric/property expression through the old parser.
    [Fact]
    public void ReflectedScalar_FallbackToMetricProperty_WhenLiteralParsingFails()
    {
        var cats = CreateReflectedCatalogs();

        // IntVal is a property returning int 42. "IntVal" fails int.TryParse, falls back to property binding.
        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error, "TakeInt#IntVal");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord { IntVal = 15 };
        object result = projection!.Fields[0].Get(session, record);

        Assert.Equal(30, (int)result); // 15 * 2
    }

    // 8. A manually constructed descriptor with ReflectedType == null retains the old behavior.
    [Fact]
    public void ManualDescriptor_WithNullReflectedType_RetainsOldBehavior()
    {
        var cats = new MetricCatalogs();
        cats.Catalogs[typeof(TestRecord)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["CustomScalar"] = new MetricDescriptor(
                    "CustomScalar", typeof(string),
                    new List<MetricParameterDescriptor>
                    {
                        new MetricParameterDescriptor("val", MetricParameterType.Scalar) // ReflectedType == null
                    },
                    "custom",
                    (_, _, args) => $"Value is {args[0]} ({args[0]?.GetType().Name})")
            }
        };

        // Old parser treats "123.45" as a double when ReflectedType is null
        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error, "CustomScalar#123.45");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord();
        object result = projection!.Fields[0].Get(session, record);

        Assert.Equal("Value is 123.45 (Double)", (string)result);
    }

    // 9. Multiple parameters correctly preserve comma/cursor handling.
    [Fact]
    public void MultipleParameters_PreserveCommaAndCursorHandling()
    {
        var cats = CreateReflectedCatalogs();

        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error,
            "MultiParam#sensor1,100,00:01:30,High,0.75");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord();
        object result = projection!.Fields[0].Get(session, record);

        Assert.Equal("sensor1:100:90:High:0.75", (string)result);
    }

    // 10. ReflectedType is faithfully populated on descriptors from reflection.
    [Fact]
    public void Reflection_PopulatesReflectedTypeOnParameterDescriptors()
    {
        var catalog = TruthInTheFlip_Fluent.DefaultReflect(typeof(TestRecord));
        Assert.NotNull(catalog);

        var takeIntDesc = catalog!.Metrics["TakeInt"];
        Assert.NotNull(takeIntDesc.Parameters);
        Assert.Equal(typeof(int), takeIntDesc.Parameters![0].ReflectedType);

        var takeSpanDesc = catalog.Metrics["TakeTimeSpan"];
        Assert.NotNull(takeSpanDesc.Parameters);
        Assert.Equal(typeof(TimeSpan), takeSpanDesc.Parameters![0].ReflectedType);

        var multiDesc = catalog.Metrics["MultiParam"];
        Assert.NotNull(multiDesc.Parameters);
        Assert.Equal(5, multiDesc.Parameters!.Count);
        Assert.Equal(typeof(string), multiDesc.Parameters[0].ReflectedType);
        Assert.Equal(typeof(int), multiDesc.Parameters[1].ReflectedType);
        Assert.Equal(typeof(TimeSpan), multiDesc.Parameters[2].ReflectedType);
        Assert.Equal(typeof(PriorityLevel), multiDesc.Parameters[3].ReflectedType);
        Assert.Equal(typeof(double), multiDesc.Parameters[4].ReflectedType);
    }

    // 11. Additional scalar types (bool, decimal, float, long, Guid, nullable).
    [Fact]
    public void AdditionalTypes_BindAndEvaluateCorrectly()
    {
        var cats = CreateReflectedCatalogs();

        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error,
            "TakeBool#true",
            "TakeDecimal#12.5",
            "TakeFloat#1.5",
            "TakeLong#99999999999",
            "TakeGuid#d3b07384-d113-40a2-aa59-541249b6b797",
            "TakeNullableInt#42",
            "TakeNullableInt#null");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord();

        Assert.Equal("YES", (string)projection!.Fields[0].Get(session, record));
        Assert.Equal(25.0m, (decimal)projection.Fields[1].Get(session, record));
        Assert.Equal(3.0f, (float)projection.Fields[2].Get(session, record));
        Assert.Equal(100000000000L, (long)projection.Fields[3].Get(session, record));
        Assert.Equal("d3b07384-d113-40a2-aa59-541249b6b797", (string)projection.Fields[4].Get(session, record));
        Assert.Equal(42, (int)projection.Fields[5].Get(session, record));
        Assert.Equal(-1, (int)projection.Fields[6].Get(session, record));
    }

    // 12. Static metric method receives typed literal scalar arguments.
    [Fact]
    public void StaticMetricMethod_ReceivesTypedLiteralScalar()
    {
        var cats = CreateReflectedCatalogs();

        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error, "StaticMultiply#3");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord { IntVal = 10 };
        object result = projection!.Fields[0].Get(session, record);

        Assert.Equal(30, (int)result); // 10 * 3
    }

    // 13. Aggregate parameter retains reflected type.
    [Fact]
    public void AggregateParameter_RetainsReflectedType()
    {
        var catalog = TruthInTheFlip_Fluent.DefaultReflect(typeof(TestRecord));
        Assert.NotNull(catalog);

        var aggDesc = catalog!.Metrics["AggregateSum"];
        Assert.NotNull(aggDesc.Parameters);
        Assert.Equal(MetricParameterType.Aggregate, aggDesc.Parameters![0].Type);
        Assert.Equal(typeof(List<double>), aggDesc.Parameters[0].ReflectedType);
    }

    // 14. Nested metric expression inside multi-parameter method call.
    [Fact]
    public void NestedMetricExpression_WithinMultiParameterCall_BindsAndEvaluates()
    {
        var cats = CreateReflectedCatalogs();

        bool ok = MetricBinder.Bind(null, cats, typeof(TestRecord), null,
            out var projection, out var error,
            "MultiParam#tag1,TakeInt#5,00:00:10,Low,2.0");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var record = new TestRecord();
        object result = projection!.Fields[0].Get(session, record);

        // TakeInt#5 evaluates to 10
        Assert.Equal("tag1:10:10:Low:2", (string)result);
    }
}
