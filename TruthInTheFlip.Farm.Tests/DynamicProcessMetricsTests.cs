using JWCFarm;
using JWCFarm.Metrics;

namespace TruthInTheFlip.Farm.Tests;

public class DynamicProcessMetricsTests
{
    // ── fake domain types ──────────────────────────────────────────────────────

    private sealed class FakeTracker
    {
        public double ZScore { get; init; }
        public double ZScoreHeads { get; init; }
    }

    private sealed class FakeSegment
    {
        public double EndTrueZ { get; init; }
    }

    private sealed class RegisteredStat
    {
        public double Value { get; init; }
        public string Title { get; init; } = "";
    }

    private sealed class UnregisteredStat
    {
        public double HiddenNumber { get; init; }
    }

    private sealed class JoinContainer
    {
        public FakeTracker TrackerItem { get; init; } = new();
        public FakeSegment SegmentItem { get; init; } = new();
    }

    // ── mock FarmProcess with dynamic metrics ───────────────────────────────────

    private sealed class DynamicMockProcess : FarmProcess
    {
        private readonly Type _statType;
        private readonly Type _inputType;
        private readonly FarmProcess? _inputProcess;
        private readonly Dictionary<Type, MetricCatalog> _dynamicCatalogs = new();
        private readonly IEnumerable<object>? _items;

        public DynamicMockProcess(
            Type statType,
            Type? inputType = null,
            FarmProcess? inputProcess = null,
            IEnumerable<object>? items = null)
        {
            _statType = statType;
            _inputType = inputType ?? typeof(object);
            _inputProcess = inputProcess;
            _items = items;
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
            => _items ?? throw new NotSupportedException("DynamicMockProcess is for binding/projection tests only.");
    }

    // ── catalog helpers ────────────────────────────────────────────────────────

    private static MetricCatalogs CreateBaseCatalogs()
    {
        var catalogs = new MetricCatalogs();

        catalogs.Catalogs[typeof(FakeTracker)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["ZScore"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "ZScore",
                    ValueType = typeof(double),
                    Help = "ZScore",
                    Getter = (ctx, o) => ((FakeTracker)o).ZScore
                },
                ["ZScoreHeads"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "ZScoreHeads",
                    ValueType = typeof(double),
                    Help = "ZScoreHeads",
                    Getter = (ctx, o) => ((FakeTracker)o).ZScoreHeads
                }
            }
        };

        catalogs.Catalogs[typeof(FakeSegment)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["EndTrueZ"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "EndTrueZ",
                    ValueType = typeof(double),
                    Help = "EndTrueZ",
                    Getter = (ctx, o) => ((FakeSegment)o).EndTrueZ
                }
            }
        };

        catalogs.Catalogs[typeof(RegisteredStat)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["Value"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "Value",
                    ValueType = typeof(double),
                    Help = "Catalog Value",
                    Getter = (ctx, o) => ((RegisteredStat)o).Value
                },
                ["Title"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "Title",
                    ValueType = typeof(string),
                    Help = "Catalog Title",
                    Getter = (ctx, o) => ((RegisteredStat)o).Title
                }
            }
        };

        return catalogs;
    }

    // ── 1. Dynamic root property ───────────────────────────────────────────────

    [Fact]
    public void DynamicRootProperty_BindsAndEvaluatesSuccessfully()
    {
        var catalogs = CreateBaseCatalogs();
        var process = new DynamicMockProcess(typeof(RegisteredStat));

        process.AddDynamicMetric(typeof(RegisteredStat), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "DynamicScore",
            ValueType = typeof(double),
            Help = "Dynamic root property",
            Getter = (ctx, o) => ((RegisteredStat)o).Value * 2.0
        });

        // Verify DynamicScore is not present in the global catalog
        Assert.False(catalogs.Catalogs[typeof(RegisteredStat)]!.Metrics.ContainsKey("DynamicScore"));

        bool ok = MetricBinder.Bind(process, catalogs, typeof(RegisteredStat), null,
            out var projection, out var error, "DynamicScore");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);
        Assert.Single(projection!.Fields);

        var stat = new RegisteredStat { Value = 42.0 };
        var session = new MetricEvaluationSession(projection);
        var result = projection.Fields[0].Get(session, stat);

        Assert.Equal(84.0, result);
    }

    // ── 2. Dynamic-only stat type (no registered global catalog) ───────────────

    [Fact]
    public void DynamicOnlyStatType_BindsWithoutRegisteredCatalog()
    {
        var catalogs = CreateBaseCatalogs();

        // Confirm UnregisteredStat has no global catalog
        Assert.False(catalogs.TryGet(typeof(UnregisteredStat), out _));

        var process = new DynamicMockProcess(typeof(UnregisteredStat));
        process.AddDynamicMetric(typeof(UnregisteredStat), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "CustomMetric",
            ValueType = typeof(double),
            Help = "Dynamic property on uncataloged type",
            Getter = (ctx, o) => ((UnregisteredStat)o).HiddenNumber + 10.0
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(UnregisteredStat), null,
            out var projection, out var error, "CustomMetric");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);

        var stat = new UnregisteredStat { HiddenNumber = 5.0 };
        var session = new MetricEvaluationSession(projection);
        var result = projection.Fields[0].Get(session, stat);

        Assert.Equal(15.0, result);
    }

    // ── 3. Dynamic property followed by ordinary nested property ───────────────

    [Fact]
    public void DynamicProperty_FollowedByOrdinaryNestedProperty_BindsAndEvaluates()
    {
        var catalogs = CreateBaseCatalogs();

        // JoinContainer has no catalog
        Assert.False(catalogs.TryGet(typeof(JoinContainer), out _));

        var process = new DynamicMockProcess(typeof(JoinContainer));
        process.AddDynamicMetric(typeof(JoinContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_0",
            ValueType = typeof(FakeTracker),
            Help = "Process-local child 0",
            Getter = (ctx, o) => ((JoinContainer)o).TrackerItem
        });
        process.AddDynamicMetric(typeof(JoinContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_1",
            ValueType = typeof(FakeSegment),
            Help = "Process-local child 1",
            Getter = (ctx, o) => ((JoinContainer)o).SegmentItem
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(JoinContainer), null,
            out var projection, out var error, "item_0.ZScore", "item_1.EndTrueZ");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);
        Assert.Equal(2, projection!.Fields.Count);

        var item0Field = projection.Fields[0];
        Assert.Equal(2, item0Field.Count);
        Assert.Equal("item_0", item0Field[0].InstanceDescriptor.Name);
        Assert.Equal("ZScore", item0Field[1].InstanceDescriptor.Name);

        var item1Field = projection.Fields[1];
        Assert.Equal(2, item1Field.Count);
        Assert.Equal("item_1", item1Field[0].InstanceDescriptor.Name);
        Assert.Equal("EndTrueZ", item1Field[1].InstanceDescriptor.Name);

        var container = new JoinContainer
        {
            TrackerItem = new FakeTracker { ZScore = 3.14 },
            SegmentItem = new FakeSegment { EndTrueZ = -2.71 }
        };

        var session = new MetricEvaluationSession(projection);
        var val0 = item0Field.Get(session, container);
        var val1 = item1Field.Get(session, container);

        Assert.Equal(3.14, val0);
        Assert.Equal(-2.71, val1);
    }

    // ── 4. Ordinary catalog precedence ─────────────────────────────────────────

    [Fact]
    public void OrdinaryCatalogMetric_TakesPrecedenceOverDynamicMetric()
    {
        var catalogs = CreateBaseCatalogs();
        var process = new DynamicMockProcess(typeof(RegisteredStat));

        // Attempt to shadow ordinary "Value" (which returns RegisteredStat.Value)
        process.AddDynamicMetric(typeof(RegisteredStat), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "Value",
            ValueType = typeof(double),
            Help = "Shadow attempt",
            Getter = (ctx, o) => 999999.0
        });

        bool ok = MetricBinder.Bind(process, catalogs, typeof(RegisteredStat), null,
            out var projection, out var error, "Value");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);

        var stat = new RegisteredStat { Value = 123.0 };
        var session = new MetricEvaluationSession(projection);
        var result = projection.Fields[0].Get(session, stat);

        // Ordinary catalog must win
        Assert.Equal(123.0, result);
    }

    // ── 5. Different process instances can expose different metrics ────────────

    [Fact]
    public void DifferentProcessInstances_CanExposeDifferentDynamicMetrics()
    {
        var catalogs = CreateBaseCatalogs();

        var processA = new DynamicMockProcess(typeof(JoinContainer));
        processA.AddDynamicMetric(typeof(JoinContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_0",
            ValueType = typeof(FakeTracker),
            Help = "Process A item_0 is FakeTracker",
            Getter = (ctx, o) => ((JoinContainer)o).TrackerItem
        });

        var processB = new DynamicMockProcess(typeof(JoinContainer));
        processB.AddDynamicMetric(typeof(JoinContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_0",
            ValueType = typeof(FakeSegment),
            Help = "Process B item_0 is FakeSegment",
            Getter = (ctx, o) => ((JoinContainer)o).SegmentItem
        });
        processB.AddDynamicMetric(typeof(JoinContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_1",
            ValueType = typeof(FakeTracker),
            Help = "Process B item_1 is FakeTracker",
            Getter = (ctx, o) => ((JoinContainer)o).TrackerItem
        });

        // Process A binding
        bool okA = MetricBinder.Bind(processA, catalogs, typeof(JoinContainer), null,
            out var projA, out var errA, "item_0.ZScore");
        Assert.True(okA);
        Assert.Null(errA);

        // Process A does NOT have item_1
        bool okAItem1 = MetricBinder.Bind(processA, catalogs, typeof(JoinContainer), null,
            out _, out var errA1, "item_1.ZScore");
        Assert.False(okAItem1);
        Assert.NotNull(errA1);
        Assert.Contains("Unknown metric 'item_1'", errA1!.Message);

        // Process B binding: item_0 is FakeSegment (has EndTrueZ, not ZScore)
        bool okB0 = MetricBinder.Bind(processB, catalogs, typeof(JoinContainer), null,
            out var projB, out var errB, "item_0.EndTrueZ", "item_1.ZScore");
        Assert.True(okB0);
        Assert.Null(errB);

        bool okBWrong = MetricBinder.Bind(processB, catalogs, typeof(JoinContainer), null,
            out _, out var errBWrong, "item_0.ZScore");
        Assert.False(okBWrong);
        Assert.NotNull(errBWrong);
        Assert.Contains("Unknown metric 'ZScore' on FakeSegment", errBWrong!.Message);
    }

    // ── 6. Unknown metrics still fail normally ──────────────────────────────────

    [Fact]
    public void UnknownMetric_FailsNormallyWithAccurateDiagnostics()
    {
        var catalogs = CreateBaseCatalogs();
        var process = new DynamicMockProcess(typeof(JoinContainer));
        process.AddDynamicMetric(typeof(JoinContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_0",
            ValueType = typeof(FakeTracker),
            Help = "item_0",
            Getter = (ctx, o) => ((JoinContainer)o).TrackerItem
        });

        // 1. Unknown root metric on uncataloged type
        bool ok1 = MetricBinder.Bind(process, catalogs, typeof(JoinContainer), null,
            out var proj1, out var err1, "nonexistent");

        Assert.False(ok1);
        Assert.Null(proj1);
        Assert.NotNull(err1);
        Assert.Equal(0, err1!.Offset);
        Assert.Equal(11, err1.Length);
        Assert.Contains("Unknown metric 'nonexistent'", err1.Message);

        // 2. Unknown nested metric
        bool ok2 = MetricBinder.Bind(process, catalogs, typeof(JoinContainer), null,
            out var proj2, out var err2, "item_0.NonExistentField");

        Assert.False(ok2);
        Assert.Null(proj2);
        Assert.NotNull(err2);
        Assert.Equal(7, err2!.Offset); // after "item_0."
        Assert.Equal(16, err2.Length);
        Assert.Contains("Unknown metric 'NonExistentField' on FakeTracker", err2.Message);
    }

    // ── 7. Dynamic function descriptor ─────────────────────────────────────────

    [Fact]
    public void DynamicFunctionDescriptor_BindsAndInvokes()
    {
        var catalogs = CreateBaseCatalogs();
        var process = new DynamicMockProcess(typeof(RegisteredStat));

        process.AddDynamicMetric(typeof(RegisteredStat), new MetricDescriptor(
            "scaleBy",
            typeof(double),
            new List<MetricParameterDescriptor>
            {
                new("val", MetricParameterType.Scalar) { ReflectedType = typeof(double) },
                new("factor", MetricParameterType.Scalar) { ReflectedType = typeof(double) }
            },
            "Scale value by factor",
            (ctx, inst, args) => ((double)args[0]) * ((double)args[1])
        ));

        bool ok = MetricBinder.Bind(process, catalogs, typeof(RegisteredStat), null,
            out var projection, out var error, "scaleBy#Value,2.5");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);

        var stat = new RegisteredStat { Value = 4.0 };
        var session = new MetricEvaluationSession(projection!);
        var result = projection!.Fields[0].Get(session, stat);

        Assert.Equal(10.0, result);
    }

    // ── 8. SourceExpressions through dynamic metrics ───────────────────────────

    [Fact]
    public void DynamicMetric_WithSourceExpressions_BindsDependenciesAndEvaluates()
    {
        var catalogs = CreateBaseCatalogs();
        var process = new DynamicMockProcess(typeof(JoinContainer));

        process.AddDynamicMetric(typeof(JoinContainer), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "item_0",
            ValueType = typeof(FakeTracker),
            Help = "item_0 tracker",
            Getter = (ctx, o) => ((JoinContainer)o).TrackerItem
        });

        // Dynamic metric declaring a dependency on "item_0.ZScore"
        process.AddDynamicMetric(typeof(JoinContainer), new MetricDescriptor(
            "TenTimesZ",
            typeof(double),
            "10 * item_0.ZScore",
            (ctx, o) => ctx.Get<double>("item_0.ZScore") * 10.0,
            sourceExpressions: new[] { "item_0.ZScore" }
        ));

        bool ok = MetricBinder.Bind(process, catalogs, typeof(JoinContainer), null,
            out var projection, out var error, "TenTimesZ");

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(projection);

        // Verify dependency was stored
        Assert.True(projection!.ContainsDependency("item_0.ZScore"));

        var container = new JoinContainer
        {
            TrackerItem = new FakeTracker { ZScore = 1.25 }
        };

        var session = new MetricEvaluationSession(projection);
        var result = projection.Fields[0].Get(session, container);

        Assert.Equal(12.5, result);
    }

    // ── 9. Aggregate/input-process scope ────────────────────────────────────────

    [Fact]
    public void AggregateBinding_ResolvesDynamicMetricsInInputProcessScope()
    {
        var catalogs = CreateBaseCatalogs();

        // Register aggregate function 'mean' on FakeSegment
        catalogs.Catalogs[typeof(FakeSegment)]!.Metrics["mean"] = new MetricDescriptor(
            "mean",
            typeof(double),
            new List<MetricParameterDescriptor>
            {
                new("values", MetricParameterType.Aggregate)
            },
            "mean",
            (ctx, inst, args) =>
            {
                var list = (List<double>)args[0];
                return list.Count == 0 ? 0.0 : list.Average();
            }
        );

        // Child process producing FakeTracker items with an inner dynamic metric
        var childProcess = new DynamicMockProcess(
            typeof(FakeTracker),
            items: new[]
            {
                new FakeTracker { ZScore = 2.0 },
                new FakeTracker { ZScore = 4.0 }
            });
        childProcess.Projection = new MetricProjection();

        childProcess.AddDynamicMetric(typeof(FakeTracker), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "InnerDynamicScore",
            ValueType = typeof(double),
            Help = "Inner dynamic score",
            Getter = (ctx, o) => ((FakeTracker)o).ZScore * 3.0
        });

        // Outer process with StatType = FakeSegment, InputType = FakeTracker, InputProcess = childProcess
        var outerProcess = new DynamicMockProcess(
            typeof(FakeSegment),
            inputType: typeof(FakeTracker),
            inputProcess: childProcess);

        outerProcess.AddDynamicMetric(typeof(FakeSegment), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "OuterOnlyProp",
            ValueType = typeof(double),
            Help = "Outer only",
            Getter = (ctx, o) => 100.0
        });

        // 1. Binding mean#InnerDynamicScore on outerProcess succeeds
        bool okInner = MetricBinder.Bind(outerProcess, catalogs, typeof(FakeSegment), typeof(FakeTracker),
            out var projInner, out var errInner, "mean#InnerDynamicScore");

        Assert.True(okInner);
        Assert.Null(errInner);
        Assert.NotNull(projInner);

        // 2. Binding mean#OuterOnlyProp fails because OuterOnlyProp is on outerProcess, not childProcess
        bool okOuterInChild = MetricBinder.Bind(outerProcess, catalogs, typeof(FakeSegment), typeof(FakeTracker),
            out _, out var errOuterInChild, "mean#OuterOnlyProp");

        Assert.False(okOuterInChild);
        Assert.NotNull(errOuterInChild);
        Assert.Contains("Unknown metric 'OuterOnlyProp' on FakeTracker", errOuterInChild!.Message);
    }

    // ── 10. Default FarmProcess returns null catalog ───────────────────────────

    private sealed class PlainProcess : FarmProcess
    {
        public override Type StatType => typeof(RegisteredStat);
        public override Type InputType => typeof(object);
        protected override IEnumerable<object> EnumerateItems(FarmContext context) => Array.Empty<object>();
    }

    [Fact]
    public void DefaultFarmProcess_GetDynamicMetricCatalog_ReturnsNull()
    {
        var process = new PlainProcess();
        Assert.Null(process.GetDynamicMetricCatalog(typeof(RegisteredStat)));
        Assert.Null(process.GetDynamicMetricCatalog(typeof(UnregisteredStat)));
    }

    // ── 11. Process-local catalog stability ───────────────────────────────────

    [Fact]
    public void ProcessLocalCatalog_ReturnsSameCatalogInstance()
    {
        var process = new DynamicMockProcess(typeof(UnregisteredStat));
        process.AddDynamicMetric(typeof(UnregisteredStat), new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = "StableProp",
            ValueType = typeof(double),
            Help = "Stable",
            Getter = (ctx, o) => 1.0
        });

        var catalog1 = process.GetDynamicMetricCatalog(typeof(UnregisteredStat));
        var catalog2 = process.GetDynamicMetricCatalog(typeof(UnregisteredStat));

        Assert.NotNull(catalog1);
        Assert.Same(catalog1, catalog2);
    }

    // ── 12. Dotted method binding on dynamic child process properties ──────────

    private sealed class MockCombinatorProcess : FarmProcess
    {
        private readonly IReadOnlyList<FarmProcess> _children;
        private readonly MetricCatalog _catalog;

        public override IReadOnlyList<FarmProcess> Children => _children;
        public override Type StatType => typeof(JoinContainer);
        public override Type InputType => typeof(object);

        public MockCombinatorProcess(params FarmProcess[] children)
        {
            _children = children;
            _catalog = new MetricCatalog();
            for (int i = 0; i < children.Length; i++)
            {
                int captured = i;
                _catalog.Add(new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = $"item_{captured}",
                    ValueType = children[captured].StatType,
                    Help = $"Item {captured}",
                    Getter = (ctx, row) => ((JoinContainer)row).TrackerItem
                });
            }
        }

        public override MetricCatalog? GetDynamicMetricCatalog(Type type)
            => type == typeof(JoinContainer) ? _catalog : null;

        protected override IEnumerable<object> EnumerateItems(FarmContext context) => Array.Empty<object>();
    }

    [Fact]
    public void DottedMethodBinding_OnChildProperty_BindsAndResolvesCorrectly()
    {
        var catalogs = CreateBaseCatalogs();

        catalogs.Catalogs[typeof(FakeTracker)]!.Metrics["scaleByTen"] = new MetricDescriptor(
            "scaleByTen",
            typeof(double),
            new List<MetricParameterDescriptor>
            {
                new("value", MetricParameterType.Scalar)
            },
            "scaleByTen",
            (ctx, inst, args) => (double)args[0] * 10.0
        );

        var child = new DynamicMockProcess(typeof(FakeTracker));
        var combinator = new MockCombinatorProcess(child);

        bool ok = MetricBinder.Bind(combinator, catalogs, typeof(JoinContainer), null,
            out var projection, out var error, "item_0.scaleByTen#ZScore");

        Assert.True(ok, error?.ToString());
        Assert.NotNull(projection);
        Assert.Single(projection.Fields);

        var container = new JoinContainer
        {
            TrackerItem = new FakeTracker { ZScore = 3.5 }
        };

        var session = new MetricEvaluationSession(projection);
        var result = projection.Fields[0].Get(session, container);

        Assert.Equal(35.0, result);
    }
}
