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
        private readonly Dictionary<Type, MetricCatalog> _dynamicCatalogs = new();

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
            if (!_dynamicCatalogs.TryGetValue(type, out var catalog))
            {
                catalog = new MetricCatalog();
                _dynamicCatalogs[type] = catalog;
            }
            catalog.Add(descriptor);
        }

        public override MetricCatalog? GetDynamicMetricCatalog(Type type)
        {
            return _dynamicCatalogs.TryGetValue(type, out var catalog) ? catalog : null;
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

        // 1 explicit dynamic field + 7 expanded fields (6 catalog + 1 dynamic) = 8
        Assert.Equal(8, projection!.Fields.Count);
        Assert.Equal("DynamicField", projection.Fields[0].ToString());
        Assert.Equal("DynamicField", projection.Fields[7].ToString());

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

    // ── 13. Dynamic metric catalog: default process ───────────────────────────

    private sealed class PlainDefaultProcess : FarmProcess
    {
        public override Type StatType => typeof(ComplexLeafContainer);
        public override Type InputType => typeof(object);
        protected override IEnumerable<object> EnumerateItems(FarmContext context) => Array.Empty<object>();
    }

    [Fact]
    public void DefaultProcess_HasNoDynamicMetricCatalog()
    {
        var process = new PlainDefaultProcess();
        var dynamicCatalog = process.GetDynamicMetricCatalog(typeof(ComplexLeafContainer));
        Assert.Null(dynamicCatalog);
    }

    // ── 14. Dynamic-only root #ALL ────────────────────────────────────────────

    [Fact]
    public void DynamicOnlyRoot_All_ExpandsDynamicScalarProperties()
    {
        var catalogs = CreateTestCatalogs(); // UnregisteredType has no catalog
        var process = new DynamicTestProcess(typeof(UnregisteredType));

        process.AddDynamicMetric(typeof(UnregisteredType), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "DynamicNum",
            ValueType = typeof(double),
            Help = "Dynamic scalar metric",
            Getter = (ctx, o) => ((UnregisteredType)o).SomeValue * 2.0
        });

        process.AddDynamicMetric(typeof(UnregisteredType), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "DynamicTag",
            ValueType = typeof(string),
            Help = "Dynamic text metric",
            Getter = (ctx, o) => "tag_" + ((UnregisteredType)o).SomeValue
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(UnregisteredType), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);
        Assert.Equal(2, projection!.Fields.Count);
        Assert.Equal(new[] { "DynamicNum", "DynamicTag" }, projection.Fields.Select(f => f.ToString()));

        var item = new UnregisteredType { SomeValue = 21.0 };
        var session = new MetricEvaluationSession(projection);
        Assert.Equal(42.0, projection.Fields[0].Get(session, item));
        Assert.Equal("tag_21", projection.Fields[1].Get(session, item));
    }

    // ── 15. Catalog and dynamic merge ─────────────────────────────────────────

    [Fact]
    public void CatalogAndDynamicMerge_All_ProducesBoth()
    {
        var catalogs = CreateTestCatalogs();
        var process = new DynamicTestProcess(typeof(ComplexLeafContainer));

        process.AddDynamicMetric(typeof(ComplexLeafContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "ExtraDyn1",
            ValueType = typeof(double),
            Help = "Extra dynamic 1",
            Getter = (ctx, o) => 111.0
        });

        process.AddDynamicMetric(typeof(ComplexLeafContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "ExtraDyn2",
            ValueType = typeof(string),
            Help = "Extra dynamic 2",
            Getter = (ctx, o) => "extra"
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        // 6 catalog properties + 2 dynamic properties = 8
        Assert.Equal(8, projection!.Fields.Count);
        var names = projection.Fields.Select(f => f.ToString()).ToList();
        Assert.Equal(new[]
        {
            "DoubleVal", "IntVal", "StringVal", "BoolVal", "TimeSpanVal", "DateTimeVal",
            "ExtraDyn1", "ExtraDyn2"
        }, names);

        var item = new ComplexLeafContainer();
        var session = new MetricEvaluationSession(projection);
        Assert.Equal(111.0, projection.Fields[6].Get(session, item));
        Assert.Equal("extra", projection.Fields[7].Get(session, item));
    }

    // ── 16. Catalog precedence on duplicate ───────────────────────────────────

    [Fact]
    public void CatalogPrecedenceOnDuplicate_All_CatalogWins()
    {
        var catalogs = CreateTestCatalogs();
        var process = new DynamicTestProcess(typeof(ComplexLeafContainer));

        // Attempt to shadow DoubleVal dynamically
        process.AddDynamicMetric(typeof(ComplexLeafContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "DoubleVal",
            ValueType = typeof(double),
            Help = "Shadow attempt",
            Getter = (ctx, o) => 99999.0
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        // Still exactly 6 fields (no duplicate DoubleVal)
        Assert.Equal(6, projection!.Fields.Count);
        Assert.Single(projection.Fields.Where(f => f.ToString() == "DoubleVal"));

        // Catalog getter evaluates, NOT dynamic getter
        var item = new ComplexLeafContainer { DoubleVal = 12.34 };
        var session = new MetricEvaluationSession(projection);
        Assert.Equal(12.34, projection.Fields[0].Get(session, item));
    }

    // ── 17. Deterministic dynamic ordering ─────────────────���──────────────────

    [Fact]
    public void DeterministicDynamicOrdering_PreservesOrdinalOrder()
    {
        var catalogs = CreateTestCatalogs();
        var process = new DynamicTestProcess(typeof(ComplexLeafContainer));

        process.AddDynamicMetric(typeof(ComplexLeafContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_0",
            ValueType = typeof(int),
            Getter = (ctx, o) => 0
        });

        process.AddDynamicMetric(typeof(ComplexLeafContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_1",
            ValueType = typeof(int),
            Getter = (ctx, o) => 1
        });

        process.AddDynamicMetric(typeof(ComplexLeafContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_2",
            ValueType = typeof(int),
            Getter = (ctx, o) => 2
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        var names = projection!.Fields.Select(f => f.ToString()).ToList();
        int idx0 = names.IndexOf("item_0");
        int idx1 = names.IndexOf("item_1");
        int idx2 = names.IndexOf("item_2");

        Assert.True(idx0 >= 6);
        Assert.Equal(idx0 + 1, idx1);
        Assert.Equal(idx1 + 1, idx2);
    }

    // ── 18. Dynamic method exclusion ──────────────────────────────────────────

    [Fact]
    public void DynamicMethodExclusion_ExcludesDynamicMethodsFromAll()
    {
        var catalogs = CreateTestCatalogs();
        var process = new DynamicTestProcess(typeof(ComplexLeafContainer));

        process.AddDynamicMetric(typeof(ComplexLeafContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Method,
            Name = "dynamicFunc",
            ValueType = typeof(double),
            Help = "Dynamic function",
            Parameters = new List<MetricParameterDescriptor>
            {
                new() { Name = "arg", Type = MetricParameterType.Scalar, ReflectedType = typeof(double) }
            },
            Invoke = (ctx, o, args) => ((ComplexLeafContainer)o).DoubleVal * (double)args[0]!
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);
        Assert.DoesNotContain(projection!.Fields, f => f.ToString().Contains("dynamicFunc"));

        // Exact invocation still works
        bool okExact = MetricBinder.Bind(process, catalogs, typeof(ComplexLeafContainer), null,
            out var projExact, out var errExact, "dynamicFunc#3.0");
        Assert.True(okExact, errExact?.ToString());
    }

    // ── 19. Dynamic intermediate type filtering ───────────────────────────────

    [Fact]
    public void DynamicIntermediateTypeFiltering_ExcludesMetricBearingObjects()
    {
        var catalogs = CreateTestCatalogs();
        var process = new DynamicTestProcess(typeof(UnregisteredType));

        // Add a dynamic property whose ValueType is NestedObject (which has a catalog)
        process.AddDynamicMetric(typeof(UnregisteredType), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_0",
            ValueType = typeof(NestedObject),
            Help = "Child scope item",
            Getter = (ctx, o) => new NestedObject { ScoreA = 50.0 }
        });

        // Add a scalar dynamic property
        process.AddDynamicMetric(typeof(UnregisteredType), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "ScalarProp",
            ValueType = typeof(double),
            Help = "Scalar prop",
            Getter = (ctx, o) => 123.0
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(UnregisteredType), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        // item_0 is intermediate metric-bearing type, so only ScalarProp is in root #ALL
        Assert.Single(projection!.Fields);
        Assert.Equal("ScalarProp", projection.Fields[0].ToString());
    }

    // ── 20. Dynamic-only intermediate type filtering ──────────────────────────

    private sealed class DynamicOnlyScopeType
    {
        public double HiddenVal { get; set; }
    }

    [Fact]
    public void DynamicOnlyIntermediateTypeFiltering_ExcludesDynamicScopeObjects()
    {
        var catalogs = CreateTestCatalogs();
        var process = new DynamicTestProcess(typeof(UnregisteredType));

        // DynamicOnlyScopeType has NO catalog, BUT process exposes dynamic metrics for it
        process.AddDynamicMetric(typeof(DynamicOnlyScopeType), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "DynamicScopeVal",
            ValueType = typeof(double),
            Getter = (ctx, o) => ((DynamicOnlyScopeType)o).HiddenVal
        });

        // Property on UnregisteredType returning DynamicOnlyScopeType
        process.AddDynamicMetric(typeof(UnregisteredType), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "dynamicScopeItem",
            ValueType = typeof(DynamicOnlyScopeType),
            Getter = (ctx, o) => new DynamicOnlyScopeType { HiddenVal = 77.0 }
        });

        // Scalar property on UnregisteredType
        process.AddDynamicMetric(typeof(UnregisteredType), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "RootScalar",
            ValueType = typeof(double),
            Getter = (ctx, o) => 88.0
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(UnregisteredType), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        // dynamicScopeItem is recognized as metric-bearing and excluded; RootScalar is included
        Assert.Single(projection!.Fields);
        Assert.Equal("RootScalar", projection.Fields[0].ToString());
    }

    // ── 21. Nested dynamic path followed by #ALL ──────────────────────────────

    [Fact]
    public void NestedDynamicPath_FollowedByAll_ExpandsChildCatalogLeaves()
    {
        var catalogs = CreateTestCatalogs();
        var process = new DynamicTestProcess(typeof(UnregisteredType));

        // dynamic item_0 returns NestedObject (which has catalog with ScoreA, ScoreB, Description)
        process.AddDynamicMetric(typeof(UnregisteredType), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_0",
            ValueType = typeof(NestedObject),
            Getter = (ctx, o) => new NestedObject
            {
                ScoreA = 3.14,
                ScoreB = 42,
                Description = "from_item_0"
            }
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(UnregisteredType), null,
            out var projection, out var error, "item_0.#ALL");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        // item_0.ScoreA, item_0.ScoreB, item_0.Description
        Assert.Equal(3, projection!.Fields.Count);
        Assert.Equal(new[] { "item_0.ScoreA", "item_0.ScoreB", "item_0.Description" },
            projection.Fields.Select(f => f.ToString()));

        var item = new UnregisteredType();
        var session = new MetricEvaluationSession(projection);
        Assert.Equal(3.14, projection.Fields[0].Get(session, item));
        Assert.Equal(42, projection.Fields[1].Get(session, item));
        Assert.Equal("from_item_0", projection.Fields[2].Get(session, item));
    }

    // ── 22. Dynamic leaf with SourceExpressions ───────────────────────────────

    [Fact]
    public void SourceExpressions_DynamicLeaf_BindsDependenciesNormally()
    {
        var catalogs = CreateTestCatalogs();
        var process = new DynamicTestProcess(typeof(ComplexLeafContainer));

        process.AddDynamicMetric(typeof(ComplexLeafContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "DynWithDep",
            ValueType = typeof(double),
            Help = "Dynamic with dependency",
            SourceExpressions = new List<string> { "DoubleVal" },
            Getter = (ctx, o) =>
            {
                // Access dependency from projection
                return ((ComplexLeafContainer)o).DoubleVal * 3.0;
            }
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(ComplexLeafContainer), null,
            out var projection, out var error, "#ALL");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);

        Assert.True(projection!.ContainsDependency("DoubleVal"));
        var field = projection.Fields.First(f => f.ToString() == "DynWithDep");

        var item = new ComplexLeafContainer { DoubleVal = 10.0 };
        var session = new MetricEvaluationSession(projection);
        Assert.Equal(30.0, field.Get(session, item));
    }
}
