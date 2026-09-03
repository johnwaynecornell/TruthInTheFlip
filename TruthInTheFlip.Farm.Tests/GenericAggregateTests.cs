using System.Collections;
using System.Reflection;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public class GenericAggregateTests
{
    public sealed class ItemRecord
    {
        [IsMetric("1.0")]
        [StringHelp("Item name")]
        public string Name { get; init; } = "";

        [IsMetric("1.0")]
        [StringHelp("Item score")]
        public int Score { get; init; }

        [IsMetric("1.0")]
        [StringHelp("Item factor")]
        public double Factor { get; init; }

        [IsMetric("1.0")]
        [StringHelp("Self item")]
        public ItemRecord Self => this;
    }

    public sealed class ContainerRecord
    {
        [IsMetric("1.0")]
        [StringHelp("Sum of integer list")]
        public int SumInts(List<int> values)
        {
            Assert.IsType<List<int>>(values);
            return values.Sum();
        }

        [IsMetric("1.0")]
        [StringHelp("Sum of double list")]
        public double SumDoubles(List<double> values)
        {
            Assert.IsType<List<double>>(values);
            return values.Sum();
        }

        [IsMetric("1.0")]
        [StringHelp("Collect items")]
        public string JoinNames(List<ItemRecord> items)
        {
            Assert.IsType<List<ItemRecord>>(items);
            return string.Join(",", items.Select(x => x.Name));
        }

        [IsMetric("1.0")]
        [StringHelp("Mixed scalar and aggregate")]
        public string ScaledSummary(string prefix, List<int> scores, double multiplier)
        {
            Assert.IsType<List<int>>(scores);
            double total = scores.Sum() * multiplier;
            return $"{prefix}:{total}";
        }

        [IsMetric("1.0")]
        [StringHelp("Dual aggregates with different types")]
        public string DualAggregates(List<string> names, List<int> scores)
        {
            Assert.IsType<List<string>>(names);
            Assert.IsType<List<int>>(scores);
            return $"{string.Join("+", names)}={scores.Sum()}";
        }
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

    private static MetricCatalogs CreateReflectedCatalogs()
    {
        var catalogs = new MetricCatalogs();
        catalogs.Reflect = TruthInTheFlip_Fluent.DefaultReflect;
        return catalogs;
    }

    // 1. Existing reflected List<double> aggregate metrics still bind and evaluate correctly.
    [Fact]
    public void Reflected_ListDouble_BindsAndEvaluatesCorrectly()
    {
        var catalogs = CreateReflectedCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "SumDoubles#Factor");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        session.Inspect(process, container, new ItemRecord { Factor = 1.5 });
        session.Inspect(process, container, new ItemRecord { Factor = 2.5 });
        session.Inspect(process, container, new ItemRecord { Factor = 3.0 });

        var result = (double)projection.Fields[0].Get(session, container);
        Assert.Equal(7.0, result);
    }

    // 2. Existing manually constructed aggregate descriptors with ReflectedType == null continue to behave as legacy List<double>.
    [Fact]
    public void ManualDescriptor_NullReflectedType_BehavesAsLegacyListDouble()
    {
        var catalogs = new MetricCatalogs();
        catalogs.Catalogs[typeof(ContainerRecord)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["LegacySum"] = new MetricDescriptor(
                    "LegacySum", typeof(double),
                    new List<MetricParameterDescriptor>
                    {
                        new("values", MetricParameterType.Aggregate) // ReflectedType is null
                    },
                    "Legacy sum",
                    (_, _, args) =>
                    {
                        Assert.IsType<List<double>>(args[0]);
                        return ((List<double>)args[0]!).Sum();
                    })
            }
        };

        catalogs.Catalogs[typeof(ItemRecord)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["Factor"] = new MetricDescriptor("Factor", typeof(double), "Factor",
                    (_, o) => ((ItemRecord)o).Factor)
            }
        };

        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "LegacySum#Factor");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        session.Inspect(process, container, new ItemRecord { Factor = 10.0 });
        session.Inspect(process, container, new ItemRecord { Factor = 20.0 });

        var result = (double)projection.Fields[0].Get(session, container);
        Assert.Equal(30.0, result);
    }

    // 3. A reflected List<int> aggregate parameter collects actual int values and receives a real List<int>.
    [Fact]
    public void Reflected_ListInt_CollectsActualIntsAndReceivesRealListInt()
    {
        var catalogs = CreateReflectedCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "SumInts#Score");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        session.Inspect(process, container, new ItemRecord { Score = 10 });
        session.Inspect(process, container, new ItemRecord { Score = 25 });
        session.Inspect(process, container, new ItemRecord { Score = 15 });

        var rawList = session.GetStatValues(container, projection.Fields[0], 0);
        Assert.IsType<List<int>>(rawList);

        var result = (int)projection.Fields[0].Get(session, container);
        Assert.Equal(50, result);
    }

    // 4. A reflected reference-type aggregate such as List<ItemRecord> collects the actual object instances.
    [Fact]
    public void Reflected_ListReferenceType_CollectsActualObjectInstances()
    {
        var catalogs = CreateReflectedCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "JoinNames#Self");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        var item1 = new ItemRecord { Name = "Alpha" };
        var item2 = new ItemRecord { Name = "Beta" };
        var item3 = new ItemRecord { Name = "Gamma" };

        session.Inspect(process, container, item1);
        session.Inspect(process, container, item2);
        session.Inspect(process, container, item3);

        var rawList = session.GetStatValues(container, projection.Fields[0], 0);
        var typedList = Assert.IsType<List<ItemRecord>>(rawList);
        Assert.Equal(3, typedList.Count);
        Assert.Same(item1, typedList[0]);
        Assert.Same(item2, typedList[1]);
        Assert.Same(item3, typedList[2]);

        var result = (string)projection.Fields[0].Get(session, container);
        Assert.Equal("Alpha,Beta,Gamma", result);
    }

    // 5. An aggregate expression producing a value incompatible with List<T> fails clearly.
    [Fact]
    public void IncompatibleType_ThrowsClearInvalidCastException()
    {
        var catalogs = CreateReflectedCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        // SumInts expects List<int>, but Name produces string
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "SumInts#Name");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        var ex = Assert.Throws<InvalidCastException>(() =>
        {
            session.Inspect(process, container, new ItemRecord { Name = "NotAnInt" });
        });

        Assert.Contains("values", ex.Message);
        Assert.Contains("System.Int32", ex.Message);
        Assert.Contains("System.String", ex.Message);
    }

    // 6. Aggregate descriptor with invalid ReflectedType fails defensively
    [Fact]
    public void InvalidReflectedType_ThrowsInvalidOperationException()
    {
        var catalogs = new MetricCatalogs();
        catalogs.Catalogs[typeof(ContainerRecord)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["BadAgg"] = new MetricDescriptor(
                    "BadAgg", typeof(int),
                    new List<MetricParameterDescriptor>
                    {
                        new("bad", MetricParameterType.Aggregate, typeof(int[])) // Array instead of List<T>
                    },
                    "Bad aggregate",
                    (_, _, _) => 0)
            }
        };

        catalogs.Catalogs[typeof(ItemRecord)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["Score"] = new MetricDescriptor("Score", typeof(int), "Score", (_, o) => ((ItemRecord)o).Score)
            }
        };

        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "BadAgg#Score");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            session.Inspect(process, container, new ItemRecord { Score = 10 });
        });

        Assert.Contains("unsupported ReflectedType", ex.Message);
    }

    // 7. A metric containing both scalar and aggregate parameters evaluates correctly.
    [Fact]
    public void MixedScalarAndAggregateParameters_EvaluatesCorrectly()
    {
        var catalogs = CreateReflectedCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "ScaledSummary#Result,Score,2.5");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        session.Inspect(process, container, new ItemRecord { Score = 4 });
        session.Inspect(process, container, new ItemRecord { Score = 6 });

        var result = (string)projection.Fields[0].Get(session, container);
        Assert.Equal("Result:25", result);
    }

    // 8. Two aggregate parameters of different element types ensure state is isolated by parameter index.
    [Fact]
    public void DualAggregatesOfDifferentTypes_IsolatedByParameterIndex()
    {
        var catalogs = CreateReflectedCatalogs();
        var process = new MockProcess(typeof(ContainerRecord), typeof(ItemRecord));

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ContainerRecord), typeof(ItemRecord),
            out var projection, out var error, "DualAggregates#Name,Score");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var session = new MetricEvaluationSession(projection!);
        var container = new ContainerRecord();

        session.Inspect(process, container, new ItemRecord { Name = "A", Score = 10 });
        session.Inspect(process, container, new ItemRecord { Name = "B", Score = 20 });
        session.Inspect(process, container, new ItemRecord { Name = "C", Score = 30 });

        var namesList = session.GetStatValues(container, projection.Fields[0], 0);
        var scoresList = session.GetStatValues(container, projection.Fields[0], 1);

        var typedNames = Assert.IsType<List<string>>(namesList);
        var typedScores = Assert.IsType<List<int>>(scoresList);

        Assert.Equal(new[] { "A", "B", "C" }, typedNames);
        Assert.Equal(new[] { 10, 20, 30 }, typedScores);

        var result = (string)projection.Fields[0].Get(session, container);
        Assert.Equal("A+B+C=60", result);
    }
}
