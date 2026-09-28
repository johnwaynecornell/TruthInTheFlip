using System.Globalization;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;

namespace TruthInTheFlip.Farm.Tests;

public class ProjectionExpansionAllTests
{
    // ── fake domain types ──────────────────────────────────────────────────────

    private sealed class ComplexLeafContainer
    {
        public double DoubleVal { get; init; }
        public int IntVal { get; init; }
        public string StringVal { get; init; } = "";
        public bool BoolVal { get; init; }
        public TimeSpan TimeSpanVal { get; init; }
        public DateTime DateTimeVal { get; init; }
        public NestedObject ChildScope { get; init; } = new();
    }

    private sealed class NestedObject
    {
        public double ScoreA { get; init; }
        public int ScoreB { get; init; }
        public string Description { get; init; } = "";
        public DeepNestedObject DeepChild { get; init; } = new();
    }

    private sealed class DeepNestedObject
    {
        public double DeepValue { get; init; }
    }

    private sealed class UnregisteredType
    {
        public double SomeValue { get; init; }
    }

    private sealed class DerivedMetricHolder
    {
        public double RawBase { get; init; }
        public double ComputedDouble { get; init; }
    }

    private sealed class DynamicTestProcess : FarmProcess
    {
        private readonly Type _statType;
        private readonly Type _inputType;
        private readonly FarmProcess? _inputProcess;
        private readonly Dictionary<(Type, string), MetricDescriptor> _dynamicMetrics = new();

        public DynamicTestProcess(
            Type statType,
            Type? inputType = null,
            FarmProcess? inputProcess = null)
        {
            _statType = statType;
            _inputType = inputType ?? typeof(object);
            _inputProcess = inputProcess;
        }

        public void AddDynamicMetric(Type type, MetricDescriptor descriptor)
        {
            _dynamicMetrics[(type, descriptor.Name)] = descriptor;
        }

        public override bool TryGetDynamicMetric(Type type, string name, out MetricDescriptor? metric)
        {
            return _dynamicMetrics.TryGetValue((type, name), out metric);
        }

        public override Type StatType => _statType;
        public override Type InputType => _inputType;
        public override FarmProcess? InputProcess => _inputProcess;

        protected override IEnumerable<object> EnumerateItems(FarmContext context)
            => throw new NotSupportedException();
    }

    // ── catalog setup ──────────────────────────────────────────────────────────

    private static MetricCatalogs CreateTestCatalogs()
    {
        var catalogs = new MetricCatalogs();

        catalogs.Catalogs[typeof(ComplexLeafContainer)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["DoubleVal"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "DoubleVal",
                    ValueType = typeof(double),
                    Help = "DoubleVal help",
                    Getter = (ctx, o) => ((ComplexLeafContainer)o).DoubleVal
                },
                ["IntVal"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "IntVal",
                    ValueType = typeof(int),
                    Help = "IntVal help",
                    Getter = (ctx, o) => ((ComplexLeafContainer)o).IntVal
                },
                ["StringVal"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "StringVal",
                    ValueType = typeof(string),
                    Help = "StringVal help",
                    Getter = (ctx, o) => ((ComplexLeafContainer)o).StringVal
                },
                ["BoolVal"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "BoolVal",
                    ValueType = typeof(bool),
                    Help = "BoolVal help",
                    Getter = (ctx, o) => ((ComplexLeafContainer)o).BoolVal
                },
                ["TimeSpanVal"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "TimeSpanVal",
                    ValueType = typeof(TimeSpan),
                    Help = "TimeSpanVal help",
                    Getter = (ctx, o) => ((ComplexLeafContainer)o).TimeSpanVal
                },
                ["DateTimeVal"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "DateTimeVal",
                    ValueType = typeof(DateTime),
                    Help = "DateTimeVal help",
                    Getter = (ctx, o) => ((ComplexLeafContainer)o).DateTimeVal
                },
                ["ChildScope"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "ChildScope",
                    ValueType = typeof(NestedObject),
                    Help = "ChildScope help",
                    Getter = (ctx, o) => ((ComplexLeafContainer)o).ChildScope
                },
                ["calculate"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Method,
                    Name = "calculate",
                    ValueType = typeof(double),
                    Help = "calculate method",
                    Parameters = new List<MetricParameterDescriptor>
                    {
                        new() { Name = "factor", Type = MetricParameterType.Scalar, ReflectedType = typeof(double) }
                    },
                    Invoke = (ctx, instance, args) => ((ComplexLeafContainer)instance).DoubleVal * (double)args[0]!
                }
            }
        };

        catalogs.Catalogs[typeof(NestedObject)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["ScoreA"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "ScoreA",
                    ValueType = typeof(double),
                    Help = "ScoreA help",
                    Getter = (ctx, o) => ((NestedObject)o).ScoreA
                },
                ["ScoreB"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "ScoreB",
                    ValueType = typeof(int),
                    Help = "ScoreB help",
                    Getter = (ctx, o) => ((NestedObject)o).ScoreB
                },
                ["Description"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "Description",
                    ValueType = typeof(string),
                    Help = "Description help",
                    Getter = (ctx, o) => ((NestedObject)o).Description
                },
                ["DeepChild"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "DeepChild",
                    ValueType = typeof(DeepNestedObject),
                    Help = "DeepChild help",
                    Getter = (ctx, o) => ((NestedObject)o).DeepChild
                },
                ["subMethod"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Method,
                    Name = "subMethod",
                    ValueType = typeof(double),
                    Help = "subMethod help",
                    Parameters = new List<MetricParameterDescriptor>
                    {
                        new() { Name = "x", Type = MetricParameterType.Scalar, ReflectedType = typeof(double) }
                    },
                    Invoke = (ctx, instance, args) => ((NestedObject)instance).ScoreA + (double)args[0]!
                }
            }
        };

        catalogs.Catalogs[typeof(DeepNestedObject)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["DeepValue"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "DeepValue",
                    ValueType = typeof(double),
                    Help = "DeepValue help",
                    Getter = (ctx, o) => ((DeepNestedObject)o).DeepValue
                }
            }
        };

        catalogs.Catalogs[typeof(DerivedMetricHolder)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["RawBase"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "RawBase",
                    ValueType = typeof(double),
                    Help = "RawBase help",
                    Getter = (ctx, o) => ((DerivedMetricHolder)o).RawBase
                },
                ["ComputedDouble"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "ComputedDouble",
                    ValueType = typeof(double),
                    Help = "ComputedDouble help",
                    SourceExpressions = new[] { "RawBase" },
                    Getter = (ctx, o) => ctx.Get<double>("RawBase") * 2.0
                }
            }
        };

        return catalogs;
    }

    // ── 1. Root expansion (#ALL) ──────────────────────────────────────────────

    [Fact]
    public void RootExpansion_ExpandsAllEligibleRootLeafProperties()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);

        // 6 eligible leaf properties: DoubleVal, IntVal, StringVal, BoolVal, TimeSpanVal, DateTimeVal
        // Excluded: ChildScope (intermediate metric-bearing object), calculate (method)
        Assert.Equal(6, projection!.Fields.Count);

        var fieldNames = projection.Fields.Select(f => f.ToString()).ToList();
        Assert.Equal(new[] { "DoubleVal", "IntVal", "StringVal", "BoolVal", "TimeSpanVal", "DateTimeVal" }, fieldNames);

        var item = new ComplexLeafContainer
        {
            DoubleVal = 3.14,
            IntVal = 42,
            StringVal = "test",
            BoolVal = true,
            TimeSpanVal = TimeSpan.FromSeconds(5),
            DateTimeVal = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc)
        };

        var session = new MetricEvaluationSession(projection);
        Assert.Equal(3.14, projection.Fields[0].Get(session, item));
        Assert.Equal(42, projection.Fields[1].Get(session, item));
        Assert.Equal("test", projection.Fields[2].Get(session, item));
        Assert.Equal(true, projection.Fields[3].Get(session, item));
        Assert.Equal(TimeSpan.FromSeconds(5), projection.Fields[4].Get(session, item));
        Assert.Equal(new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc), projection.Fields[5].Get(session, item));
    }

    // ── 2. Nested expansion (SomePath.#ALL) ────────────────────────────────────

    [Fact]
    public void NestedExpansion_ExpandsNestedScopeLeafProperties()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "ChildScope.#ALL");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);

        // NestedObject has ScoreA, ScoreB, Description as leaves.
        // DeepChild (object scope) and subMethod (method) excluded.
        Assert.Equal(3, projection!.Fields.Count);

        var fieldNames = projection.Fields.Select(f => f.ToString()).ToList();
        Assert.Equal(new[] { "ChildScope.ScoreA", "ChildScope.ScoreB", "ChildScope.Description" }, fieldNames);

        var item = new ComplexLeafContainer
        {
            ChildScope = new NestedObject
            {
                ScoreA = 99.5,
                ScoreB = 7,
                Description = "NestedDesc"
            }
        };

        var session = new MetricEvaluationSession(projection);
        Assert.Equal(99.5, projection.Fields[0].Get(session, item));
        Assert.Equal(7, projection.Fields[1].Get(session, item));
        Assert.Equal("NestedDesc", projection.Fields[2].Get(session, item));
    }

    [Fact]
    public void DeeplyNestedExpansion_ExpandsDeeperScopeLeafProperties()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "ChildScope.DeepChild.#ALL");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);

        Assert.Single(projection!.Fields);
        Assert.Equal("ChildScope.DeepChild.DeepValue", projection.Fields[0].ToString());

        var item = new ComplexLeafContainer
        {
            ChildScope = new NestedObject
            {
                DeepChild = new DeepNestedObject { DeepValue = 123.456 }
            }
        };

        var session = new MetricEvaluationSession(projection);
        Assert.Equal(123.456, projection.Fields[0].Get(session, item));
    }

    // ── 3. Intermediate object filtering ──────────────────────────────────────

    [Fact]
    public void IntermediateObjectFiltering_ExcludesMetricBearingObjectsAndDoesNotRecursivelyDescend()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok);
        Assert.NotNull(projection);

        var names = projection!.Fields.Select(f => f.ToString()).ToList();
        // ChildScope is a metric-bearing object, so it must not be in fields
        Assert.DoesNotContain("ChildScope", names);
        // And it must not recursively emit nested children
        Assert.DoesNotContain("ChildScope.ScoreA", names);
        Assert.DoesNotContain("ChildScope.DeepChild.DeepValue", names);
    }

    // ── 4. Method exclusion ───────────────────────────────────────────────────

    [Fact]
    public void MethodExclusion_ExcludesMethodsAndFunctions()
    {
        var catalogs = CreateTestCatalogs();

        bool okRoot = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projRoot, out _, "#ALL");
        Assert.True(okRoot);
        Assert.DoesNotContain(projRoot!.Fields, f => f.ToString().Contains("calculate"));

        bool okNested = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projNested, out _, "ChildScope.#ALL");
        Assert.True(okNested);
        Assert.DoesNotContain(projNested!.Fields, f => f.ToString().Contains("subMethod"));
    }

    // ── 5. Supported scalar types ─────────────────────────────────────────────

    [Fact]
    public void SupportedScalarTypes_IncludesVariousScalarAndDateTimeTypes()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out _, "#ALL");

        Assert.True(ok);
        var returnTypes = projection!.Fields
            .Select(f => MetricBinder.GetPathReturnType(f))
            .ToList();

        Assert.Contains(typeof(double), returnTypes);
        Assert.Contains(typeof(int), returnTypes);
        Assert.Contains(typeof(string), returnTypes);
        Assert.Contains(typeof(bool), returnTypes);
        Assert.Contains(typeof(TimeSpan), returnTypes);
        Assert.Contains(typeof(DateTime), returnTypes);
    }

    // ── 6. Unknown / no-catalog nested scope ──────────────────────────────────

    [Fact]
    public void RootExpansion_OnUncatalogedType_ReturnsClearBindError()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(UnregisteredType), null,
            out var projection, out var error, "#ALL");

        Assert.False(ok);
        Assert.Null(projection);
        Assert.NotNull(error);
        Assert.Equal(0, error!.Offset);
        Assert.Equal(4, error.Length);
        Assert.Contains("No metric catalog for type 'UnregisteredType' to expand '#ALL'", error.Message);
    }

    [Fact]
    public void NestedExpansion_OnUnknownPrefix_ReturnsClearBindError()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "NonExistent.#ALL");

        Assert.False(ok);
        Assert.Null(projection);
        Assert.NotNull(error);
        Assert.Equal(0, error!.Offset);
        Assert.Contains("Unknown metric 'NonExistent'", error.Message);
    }

    [Fact]
    public void NestedExpansion_OnScalarPrefix_ReturnsClearBindError()
    {
        var catalogs = CreateTestCatalogs();

        // DoubleVal returns double, which has no metric catalog
        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "DoubleVal.#ALL");

        Assert.False(ok);
        Assert.Null(projection);
        Assert.NotNull(error);
        Assert.Equal(10, error!.Offset); // "DoubleVal.".Length = 10
        Assert.Equal(4, error.Length);
        Assert.Contains("No metric catalog for type 'Double' to expand '#ALL'", error.Message);
    }

    [Fact]
    public void NestedExpansion_EmptyPrefix_ReturnsClearBindError()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, ".#ALL");

        Assert.False(ok);
        Assert.Null(projection);
        Assert.NotNull(error);
        Assert.Contains("Invalid scope path before '.#ALL'", error!.Message);
    }

    // ── 7. Ordinary explicit fields remain unchanged ──────────────────────────

    [Fact]
    public void OrdinaryExplicitFields_RemainUnchanged()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error,
            "DoubleVal",
            "ChildScope.ScoreA",
            "calculate#2.5");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);
        Assert.Equal(3, projection!.Fields.Count);

        var item = new ComplexLeafContainer
        {
            DoubleVal = 10.0,
            ChildScope = new NestedObject { ScoreA = 20.0 }
        };

        var session = new MetricEvaluationSession(projection);
        Assert.Equal(10.0, projection.Fields[0].Get(session, item));
        Assert.Equal(20.0, projection.Fields[1].Get(session, item));
        Assert.Equal(25.0, projection.Fields[2].Get(session, item));
    }

    // ── 8. SourceExpressions on expanded fields ───────────────────────────────

    [Fact]
    public void SourceExpressions_BoundAutomaticallyOnExpandedFields()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(DerivedMetricHolder), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);

        Assert.Equal(2, projection!.Fields.Count);
        Assert.Equal("RawBase", projection.Fields[0].ToString());
        Assert.Equal("ComputedDouble", projection.Fields[1].ToString());

        // Verify SourceExpressions dependency was tracked in projection
        Assert.True(projection.ContainsDependency("RawBase"));

        var item = new DerivedMetricHolder
        {
            RawBase = 21.0,
            ComputedDouble = 0.0 // Value is computed via Getter with SourceExpression
        };

        var session = new MetricEvaluationSession(projection);
        Assert.Equal(21.0, projection.Fields[0].Get(session, item));
        Assert.Equal(42.0, projection.Fields[1].Get(session, item));
    }

    // ── 9. Dynamic metric regression ──────────────────────────────────────────

    [Fact]
    public void DynamicMetrics_ExplicitlyNamedDynamicMetricStillBinds()
    {
        var catalogs = CreateTestCatalogs();
        var process = new DynamicTestProcess(typeof(ComplexLeafContainer));

        process.AddDynamicMetric(typeof(ComplexLeafContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "DynamicField",
            ValueType = typeof(double),
            Help = "Dynamic metric",
            Getter = (ctx, o) => ((ComplexLeafContainer)o).DoubleVal + 100.0
        });

        // Explicit dynamic field still binds alongside #ALL
        bool ok = MetricBinder.Bind(process, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "DynamicField", "#ALL");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);

        // 1 dynamic field + 6 expanded fields = 7
        Assert.Equal(7, projection!.Fields.Count);
        Assert.Equal("DynamicField", projection.Fields[0].ToString());

        var item = new ComplexLeafContainer { DoubleVal = 5.0 };
        var session = new MetricEvaluationSession(projection);
        Assert.Equal(105.0, projection.Fields[0].Get(session, item));
    }

    // ── 10. Output adapter smoke (Pretty and CSV) ─────────────────────────────

    [Fact]
    public void OutputAdapterSmoke_PrettyAndCsvRenderExpandedFields()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok);
        Assert.NotNull(projection);

        var item = new ComplexLeafContainer
        {
            DoubleVal = 1.23,
            IntVal = 10,
            StringVal = "abc",
            BoolVal = false,
            TimeSpanVal = TimeSpan.FromHours(1),
            DateTimeVal = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)
        };

        var session = new MetricEvaluationSession(projection!);

        // Pretty output smoke test
        var prettyFields = projection!.Fields
            .Select(f => (f.ToString(), TruthInTheFlip_Fluent.PrettyOut(f.Get(session, item))))
            .ToList();

        Assert.Equal(6, prettyFields.Count);
        Assert.Equal(("DoubleVal", "1.23"), prettyFields[0]);
        Assert.Equal(("IntVal", "10"), prettyFields[1]);
        Assert.Equal(("StringVal", "abc"), prettyFields[2]);
        Assert.Equal(("BoolVal", "false"), prettyFields[3]);
        Assert.Equal(("TimeSpanVal", "01:00:00"), prettyFields[4]);
        Assert.Equal(("DateTimeVal", "2026-09-28T00:00:00.000Z"), prettyFields[5]);

        // CSV output smoke test
        using var stringWriter = new StringWriter();
        TruthInTheFlip_Fluent.WriteHeader(projection, stringWriter);
        TruthInTheFlip_Fluent.WriteRow(session, stringWriter, item);

        string csvOutput = stringWriter.ToString();
        var lines = csvOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Equal("DoubleVal,IntVal,StringVal,BoolVal,TimeSpanVal,DateTimeVal", lines[0]);
        Assert.Equal("1.23,10,abc,False,01:00:00,2026-09-28T00:00:00.000Z", lines[1]);
    }

    // ── 11. Duplicate handling & ordering ─────────────────────────────────────

    [Fact]
    public void DuplicateHandling_PreservesRequestedOrder()
    {
        var catalogs = CreateTestCatalogs();

        // Explicit "DoubleVal" followed by "#ALL" (which also contains "DoubleVal")
        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "DoubleVal", "#ALL");

        Assert.True(ok);
        Assert.NotNull(projection);

        // 1 explicit + 6 expanded = 7 fields
        Assert.Equal(7, projection!.Fields.Count);
        Assert.Equal("DoubleVal", projection.Fields[0].ToString());
        Assert.Equal("DoubleVal", projection.Fields[1].ToString());
        Assert.Equal("IntVal", projection.Fields[2].ToString());
    }

    // ── 12. Multiple expansions in same projection ────────────────────────────

    [Fact]
    public void MultipleExpansions_InSameProjection_ExpandsEachInOrder()
    {
        var catalogs = CreateTestCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "ChildScope.#ALL", "ChildScope.DeepChild.#ALL");

        Assert.True(ok);
        Assert.NotNull(projection);

        // 3 from ChildScope + 1 from DeepChild = 4 fields
        Assert.Equal(4, projection!.Fields.Count);
        Assert.Equal(new[]
        {
            "ChildScope.ScoreA",
            "ChildScope.ScoreB",
            "ChildScope.Description",
            "ChildScope.DeepChild.DeepValue"
        }, projection.Fields.Select(f => f.ToString()));
    }
}
