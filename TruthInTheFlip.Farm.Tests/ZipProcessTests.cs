using System.Globalization;
using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public class ZipProcessTests
{
    private sealed class RecordA
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public double ZScore { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public int Id { get; set; }
    }

    private sealed class RecordB
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public double EndTrueZ { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Label { get; set; } = "";
    }

    private sealed class RecordC
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public double AvgMeanA { get; set; }
    }

    private sealed class MockChildProcess<T> : FarmProcess
    {
        private readonly IEnumerable<T> _items;
        private readonly bool _throwOnEnumerate;

        public MockChildProcess(IEnumerable<T> items, bool throwOnEnumerate = false)
        {
            _items = items;
            _throwOnEnumerate = throwOnEnumerate;
        }

        public override Type StatType => typeof(T);
        public override Type InputType => typeof(T);

        protected override IEnumerable<object> EnumerateItems(FarmContext context)
        {
            if (_throwOnEnumerate)
                throw new InvalidOperationException("Simulated child failure in EnumerateItems");

            foreach (var item in _items)
            {
                yield return item!;
            }
        }
    }

    private static MetricCatalogs CreateCatalogs()
    {
        var catalogs = new MetricCatalogs();
        catalogs.Reflect = TruthInTheFlip_Fluent.DefaultReflect;
        return catalogs;
    }

    // ── 1. Two-child positional zip ───────────────────────────────────────────

    [Fact]
    public void TwoChildPositionalZip_EmitsAlignedRows()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 1.0, Id = 101 },
            new() { ZScore = 2.0, Id = 102 },
            new() { ZScore = 3.0, Id = 103 }
        });

        var childB = new MockChildProcess<RecordB>(new RecordB[]
        {
            new() { EndTrueZ = 10.0, Label = "b1" },
            new() { EndTrueZ = 20.0, Label = "b2" },
            new() { EndTrueZ = 30.0, Label = "b3" }
        });

        var zip = new ZipProcess(childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = zip.BindFields(catalogs, new[] { "item_0.ZScore", "item_1.EndTrueZ", "item_1.Label" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        Assert.Equal(3, produced.Count);

        var session = zip.Session!;
        var proj = zip.Projection!;

        Assert.Equal(1.0, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal(10.0, proj.Fields[1].Get(session, produced[0]));
        Assert.Equal("b1", proj.Fields[2].Get(session, produced[0]));

        Assert.Equal(2.0, proj.Fields[0].Get(session, produced[1]));
        Assert.Equal(20.0, proj.Fields[1].Get(session, produced[1]));
        Assert.Equal("b2", proj.Fields[2].Get(session, produced[1]));

        Assert.Equal(3.0, proj.Fields[0].Get(session, produced[2]));
        Assert.Equal(30.0, proj.Fields[1].Get(session, produced[2]));
        Assert.Equal("b3", proj.Fields[2].Get(session, produced[2]));
    }

    // ── 2. N-way positional zip (3 children) ──────────────────────────────────

    [Fact]
    public void NWayPositionalZip_ThreeChildren_EmitsAlignedRows()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 1.5, Id = 1 },
            new() { ZScore = 2.5, Id = 2 }
        });

        var childB = new MockChildProcess<RecordB>(new RecordB[]
        {
            new() { EndTrueZ = 10.5, Label = "first" },
            new() { EndTrueZ = 20.5, Label = "second" }
        });

        var childC = new MockChildProcess<RecordC>(new RecordC[]
        {
            new() { AvgMeanA = 51.0 },
            new() { AvgMeanA = 52.0 }
        });

        var zip = new ZipProcess(childA, childB, childC);
        var catalogs = CreateCatalogs();

        bool ok = zip.BindFields(catalogs, new[] { "item_0.ZScore", "item_1.Label", "item_2.AvgMeanA" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        Assert.Equal(2, produced.Count);

        var session = zip.Session!;
        var proj = zip.Projection!;

        Assert.Equal(1.5, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal("first", proj.Fields[1].Get(session, produced[0]));
        Assert.Equal(51.0, proj.Fields[2].Get(session, produced[0]));

        Assert.Equal(2.5, proj.Fields[0].Get(session, produced[1]));
        Assert.Equal("second", proj.Fields[1].Get(session, produced[1]));
        Assert.Equal(52.0, proj.Fields[2].Get(session, produced[1]));
    }

    // ── 3. Shortest wins ──────────────────────────────────────────────────────

    [Fact]
    public void ShortestWins_EmitsMinimumCommonCount()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 1.0 },
            new() { ZScore = 2.0 },
            new() { ZScore = 3.0 }
        }); // length 3

        var childB = new MockChildProcess<RecordB>(new RecordB[]
        {
            new() { EndTrueZ = 10.0 },
            new() { EndTrueZ = 20.0 }
        }); // length 2

        var childC = new MockChildProcess<RecordC>(new RecordC[]
        {
            new() { AvgMeanA = 50.0 },
            new() { AvgMeanA = 51.0 },
            new() { AvgMeanA = 52.0 },
            new() { AvgMeanA = 53.0 }
        }); // length 4

        var zip = new ZipProcess(childA, childB, childC);
        var catalogs = CreateCatalogs();

        bool ok = zip.BindFields(catalogs, new[] { "item_0.ZScore", "item_1.EndTrueZ", "item_2.AvgMeanA" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        // Min length is 2
        Assert.Equal(2, produced.Count);

        var session = zip.Session!;
        var proj = zip.Projection!;

        Assert.Equal(1.0, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal(10.0, proj.Fields[1].Get(session, produced[0]));
        Assert.Equal(50.0, proj.Fields[2].Get(session, produced[0]));

        Assert.Equal(2.0, proj.Fields[0].Get(session, produced[1]));
        Assert.Equal(20.0, proj.Fields[1].Get(session, produced[1]));
        Assert.Equal(51.0, proj.Fields[2].Get(session, produced[1]));
    }

    // ── 4. Empty child population ───────────────────────────────────────��─────

    [Fact]
    public void EmptyChildPopulation_EmitsZeroRowsWithoutError()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 1.0 },
            new() { ZScore = 2.0 }
        });

        var childB = new MockChildProcess<RecordB>(Array.Empty<RecordB>()); // empty population

        var zip = new ZipProcess(childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = zip.BindFields(catalogs, new[] { "item_0.ZScore", "item_1.EndTrueZ" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        Assert.Empty(produced);
    }

    // ── 5. Empty child collection / null validation ───────────────────────────

    [Fact]
    public void EmptyChildCollection_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ZipProcess(Array.Empty<FarmProcess>()));
        Assert.Throws<ArgumentNullException>(() => new ZipProcess((FarmProcess[])null!));
        Assert.Throws<ArgumentException>(() => ZipProcess.Zip(Array.Empty<FarmProcess>()));
    }

    [Fact]
    public void NullChildInArray_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ZipProcess(new FarmProcess[] { null! }));
    }

    // ── 6. Dynamic item metrics exposed only for real child positions ─────────

    [Fact]
    public void DynamicItemMetrics_ExposedForValidChildIndicesOnly()
    {
        var childA = new MockChildProcess<RecordA>(Array.Empty<RecordA>());
        var childB = new MockChildProcess<RecordB>(Array.Empty<RecordB>());
        var zip = new ZipProcess(childA, childB);

        var catalog = zip.GetDynamicMetricCatalog(typeof(ProcessArrayStats));
        Assert.NotNull(catalog);

        Assert.True(catalog!.Metrics.TryGetValue("item_0", out var metric0));
        Assert.NotNull(metric0);
        Assert.Equal(typeof(RecordA), metric0!.ValueType);
        Assert.Equal(MetricDescriptor.EType.Property, metric0.Type);

        Assert.True(catalog.Metrics.TryGetValue("item_1", out var metric1));
        Assert.NotNull(metric1);
        Assert.Equal(typeof(RecordB), metric1!.ValueType);
        Assert.Equal(MetricDescriptor.EType.Property, metric1.Type);

        // Out of bounds
        Assert.False(catalog.Metrics.ContainsKey("item_2"));
        Assert.False(catalog.Metrics.ContainsKey("item_-1"));
        Assert.False(catalog.Metrics.ContainsKey("item_abc"));
        Assert.False(catalog.Metrics.ContainsKey("other"));

        // Wrong type
        Assert.Null(zip.GetDynamicMetricCatalog(typeof(RecordA)));
    }

    // ── 6b. Zip dynamic catalog stability ─────────────────────────────────────

    [Fact]
    public void ZipProcess_DynamicCatalog_IsStableAndDeterministic()
    {
        var childA = new MockChildProcess<RecordA>(Array.Empty<RecordA>());
        var childB = new MockChildProcess<RecordB>(Array.Empty<RecordB>());
        var zip = new ZipProcess(childA, childB);

        var catalog1 = zip.GetDynamicMetricCatalog(typeof(ProcessArrayStats));
        var catalog2 = zip.GetDynamicMetricCatalog(typeof(ProcessArrayStats));

        Assert.NotNull(catalog1);
        Assert.Same(catalog1, catalog2);

        // Keys preserved in child ordinal order
        Assert.Equal(new[] { "item_0", "item_1" }, catalog1!.Metrics.Keys);
    }

    // ── 7. Nested metric continuation ─────────────────────────────────────────

    [Fact]
    public void NestedMetricContinuation_BindsAndEvaluatesHeterogeneousPaths()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[] { new() { ZScore = 4.25, Id = 77 } });
        var childB = new MockChildProcess<RecordB>(new RecordB[] { new() { EndTrueZ = 9.75, Label = "test" } });

        var zip = new ZipProcess(childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = zip.BindFields(catalogs, new[] { "item_0.ZScore", "item_0.Id", "item_1.EndTrueZ", "item_1.Label" }, out var error);
        Assert.True(ok, error?.ToString());
        Assert.NotNull(zip.Projection);
        Assert.Equal(4, zip.Projection!.Fields.Count);

        var row = new ProcessArrayStats(new RecordA { ZScore = 4.25, Id = 77 }, new RecordB { EndTrueZ = 9.75, Label = "test" });
        var session = new MetricEvaluationSession(zip.Projection);

        Assert.Equal(4.25, zip.Projection.Fields[0].Get(session, row));
        Assert.Equal(77, zip.Projection.Fields[1].Get(session, row));
        Assert.Equal(9.75, zip.Projection.Fields[2].Get(session, row));
        Assert.Equal("test", zip.Projection.Fields[3].Get(session, row));
    }

    // ── 8. Invalid item identity ──────────────────────────────────────────────

    [Fact]
    public void InvalidItemIdentity_FailsBindingWithDiagnosticError()
    {
        var childA = new MockChildProcess<RecordA>(Array.Empty<RecordA>());
        var childB = new MockChildProcess<RecordB>(Array.Empty<RecordB>());

        var zip = new ZipProcess(childA, childB);
        var catalogs = CreateCatalogs();

        // 2 children -> item_0 and item_1 exist, item_2 does not
        bool ok = zip.BindFields(catalogs, new[] { "item_2.ZScore" }, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("item_2", error!.Message);
    }

    // ── 9. Existing child Actions are preserved ───────────────────────────────

    [Fact]
    public void ExistingChildActions_ArePreservedAndRestored()
    {
        var events = new List<string>();

        var childA = new MockChildProcess<RecordA>(new RecordA[] { new() { ZScore = 1.0 }, new() { ZScore = 2.0 } });
        var originalActionsA = new ProcessActions(
            begin: _ => events.Add("A:Begin"),
            process: (_, item) => events.Add($"A:Process:{(item as RecordA)?.ZScore}"),
            end: _ => events.Add("A:End"),
            abort: (_, ex) => events.Add($"A:Abort:{ex.Message}")
        );
        childA.Actions = originalActionsA;

        var childB = new MockChildProcess<RecordB>(new RecordB[] { new() { EndTrueZ = 10.0 }, new() { EndTrueZ = 20.0 } });
        var originalActionsB = new ProcessActions(
            begin: _ => events.Add("B:Begin"),
            process: (_, item) => events.Add($"B:Process:{(item as RecordB)?.EndTrueZ}"),
            end: _ => events.Add("B:End"),
            abort: (_, ex) => events.Add($"B:Abort:{ex.Message}")
        );
        childB.Actions = originalActionsB;

        var zip = new ZipProcess(childA, childB);
        var zipEvents = new List<string>();
        zip.Actions = new ProcessActions(
            begin: _ => zipEvents.Add("Zip:Begin"),
            process: (_, item) => zipEvents.Add($"Zip:Process:{((ProcessArrayStats)item).Items.Count}"),
            end: _ => zipEvents.Add("Zip:End")
        );

        zip.Execute(new FarmContext());

        // Child A executed sequentially to completion, then Child B executed sequentially to completion
        Assert.Equal(new[]
        {
            "A:Begin", "A:Process:1", "A:Process:2", "A:End",
            "B:Begin", "B:Process:10", "B:Process:20", "B:End"
        }, events);

        Assert.Equal(new[] { "Zip:Begin", "Zip:Process:2", "Zip:Process:2", "Zip:End" }, zipEvents);

        // Actions references are exactly restored
        Assert.Same(originalActionsA, childA.Actions);
        Assert.Same(originalActionsB, childB.Actions);
    }

    // ── 10. Child failure ─────────────────────────────────────────────────────

    [Fact]
    public void ChildFailure_PropagatesExceptionAndRestoresActions()
    {
        var events = new List<string>();

        var childA = new MockChildProcess<RecordA>(new RecordA[] { new() { ZScore = 1.0 } });
        var originalActionsA = new ProcessActions(
            begin: _ => events.Add("A:Begin"),
            process: (_, _) => events.Add("A:Process"),
            end: _ => events.Add("A:End"),
            abort: (_, ex) => events.Add($"A:Abort:{ex.Message}")
        );
        childA.Actions = originalActionsA;

        var childB = new MockChildProcess<RecordB>(Array.Empty<RecordB>(), throwOnEnumerate: true);
        var originalActionsB = new ProcessActions(
            begin: _ => events.Add("B:Begin"),
            process: (_, _) => events.Add("B:Process"),
            end: _ => events.Add("B:End"),
            abort: (_, ex) => events.Add($"B:Abort:{ex.Message}")
        );
        childB.Actions = originalActionsB;

        var zip = new ZipProcess(childA, childB);
        var zipEvents = new List<string>();
        zip.Actions = new ProcessActions(
            begin: _ => zipEvents.Add("Zip:Begin"),
            process: (_, _) => zipEvents.Add("Zip:Process"),
            end: _ => zipEvents.Add("Zip:End"),
            abort: (_, ex) => zipEvents.Add($"Zip:Abort:{ex.Message}")
        );

        var ex = Assert.Throws<InvalidOperationException>(() => zip.Execute(new FarmContext()));
        Assert.Contains("Simulated child failure in EnumerateItems", ex.Message);

        // A executed fine, B aborted
        Assert.Contains("A:Begin", events);
        Assert.Contains("A:End", events);
        Assert.Contains("B:Begin", events);
        Assert.Contains("B:Abort:Simulated child failure in EnumerateItems", events);

        // Outer zip also received Abort
        Assert.Contains("Zip:Begin", zipEvents);
        Assert.Contains("Zip:Abort:Simulated child failure in EnumerateItems", zipEvents);
        Assert.DoesNotContain("Zip:Process", zipEvents);
        Assert.DoesNotContain("Zip:End", zipEvents);

        // Child actions references guaranteed restored
        Assert.Same(originalActionsA, childA.Actions);
        Assert.Same(originalActionsB, childB.Actions);
    }

    // ── 11. ChildProcessObserver failure ──────────────────────────────────────

    [Fact]
    public void ChildProcessObserver_WhenObserverThrows_RestoresActionsAndPropagates()
    {
        var child = new MockChildProcess<RecordA>(new RecordA[] { new() { ZScore = 1.0 } });
        var originalActions = new ProcessActions(
            begin: _ => { },
            abort: (_, _) => { }
        );
        child.Actions = originalActions;

        Assert.Throws<InvalidOperationException>(() =>
        {
            ChildProcessObserver.Execute(child, new FarmContext(), (_, _) =>
            {
                throw new InvalidOperationException("Observer blew up");
            });
        });

        Assert.Same(originalActions, child.Actions);
    }

    // ── 12. Repeated execution ────────────────────────────────────────────────

    [Fact]
    public void RepeatedExecution_ProducesFreshSessionsAndDeterministicOutput()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[] { new() { ZScore = 10.0 } });
        var childB = new MockChildProcess<RecordB>(new RecordB[] { new() { EndTrueZ = 20.0 } });
        var zip = new ZipProcess(childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = zip.BindFields(catalogs, new[] { "item_0.ZScore", "item_1.EndTrueZ" }, out var error);
        Assert.True(ok, error?.ToString());

        var run1Items = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => run1Items.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());
        var session1 = zip.Session;

        var run2Items = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => run2Items.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());
        var session2 = zip.Session;

        Assert.NotSame(session1, session2);
        Assert.Single(run1Items);
        Assert.Single(run2Items);
        Assert.Equal(10.0, zip.Projection!.Fields[0].Get(session2!, run2Items[0]));
        Assert.Equal(20.0, zip.Projection!.Fields[1].Get(session2!, run2Items[0]));
    }

    // ── 13. Nested higher-order processes ─────────────────────────────────────

    [Fact]
    public void NestedHigherOrder_WrapAroundZip_EvaluatesCorrectly()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 2.0 },
            new() { ZScore = 4.0 },
            new() { ZScore = 6.0 }
        });

        var childB = new MockChildProcess<RecordB>(new RecordB[]
        {
            new() { EndTrueZ = 10.0 },
            new() { EndTrueZ = 20.0 },
            new() { EndTrueZ = 30.0 }
        });

        var zip = new ZipProcess(childA, childB);
        var wrap = new WrapProcess(zip);

        var catalogs = CreateCatalogs();
        bool ok = wrap.BindFields(catalogs, new[] { "mean#item_0.ZScore", "max#item_1.EndTrueZ" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<object>();
        wrap.Actions = new ProcessActions(process: (_, item) => produced.Add(item));
        wrap.Execute(new FarmContext());

        Assert.Single(produced);
        var session = wrap.Session!;
        var proj = wrap.Projection!;

        Assert.Equal(4.0, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal(30.0, proj.Fields[1].Get(session, produced[0]));
    }

    [Fact]
    public void NestedHigherOrder_ZipAroundWrap_EvaluatesCorrectly()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 2.0 },
            new() { ZScore = 4.0 }
        });
        var wrapA = new WrapProcess(childA);

        var childB = new MockChildProcess<RecordB>(new RecordB[]
        {
            new() { EndTrueZ = 100.0 }
        });

        var zip = new ZipProcess(wrapA, childB);

        var catalogs = CreateCatalogs();
        // wrapA emits 1 WrapStats item; childB emits 1 RecordB item
        bool zipOk = zip.BindFields(catalogs, new[] { "item_1.EndTrueZ", "item_1.Label" }, out var zipError);
        Assert.True(zipOk, zipError?.ToString());

        var produced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        Assert.Single(produced);
        var session = zip.Session!;
        var proj = zip.Projection!;

        Assert.IsType<WrapStats>(produced[0].Items[0]);
        Assert.IsType<RecordB>(produced[0].Items[1]);
        Assert.Equal(100.0, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal("", proj.Fields[1].Get(session, produced[0]));
    }

    // ── 14. Metric functions across items ─────────────────────────────────────

    [Fact]
    public void MetricFunctionsAcrossItems_EvaluatesCorrectly()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 15.0 },
            new() { ZScore = 50.0 }
        });

        var childB = new MockChildProcess<RecordB>(new RecordB[]
        {
            new() { EndTrueZ = 5.0 },
            new() { EndTrueZ = 20.0 }
        });

        var zip = new ZipProcess(childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = zip.BindFields(catalogs, new[] { "sub#item_0.ZScore,item_1.EndTrueZ", "add#item_0.ZScore,item_1.EndTrueZ" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        Assert.Equal(2, produced.Count);

        var session = zip.Session!;
        var proj = zip.Projection!;

        // Row 0: sub(15, 5) = 10, add(15, 5) = 20
        Assert.Equal(10.0, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal(20.0, proj.Fields[1].Get(session, produced[0]));

        // Row 1: sub(50, 20) = 30, add(50, 20) = 70
        Assert.Equal(30.0, proj.Fields[0].Get(session, produced[1]));
        Assert.Equal(70.0, proj.Fields[1].Get(session, produced[1]));
    }

    // ── 15. FluentCommandLine CLI integration ─────────────────────────────────

    [Fact]
    public void FluentCommandLine_ParsesAndExecutesZipCommand()
    {
        var env = new FluentEnvironment();
        env.AddModule<TruthInTheFlip_Fluent>();
        env.ServeTypes = new[] { typeof(FarmProcess), typeof(FarmCommand) };

        string quantPath = Path.GetFullPath("Artifacts/Trackers/Quant.tkr");
        if (!File.Exists(quantPath))
            return;

        int cursor = 0;
        var parsed = env.ParseOne(new[]
        {
            "zip",
            "tracker", "window", "by_total", "10B", "file", quantPath,
            "tracker", "window", "by_total", "100B", "file", quantPath,
            ".END."
        }, ref cursor);

        Assert.NotNull(parsed);
        var zip = Assert.IsType<ZipProcess>(parsed.Result);
        Assert.Equal(2, zip.Children.Count);

        var catalogs = env.Context.Get<MetricCatalogs>();
        bool ok = zip.BindFields(catalogs, new[] { "item_0.ZScore", "item_1.ZScore" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        Assert.NotEmpty(produced);
        var session = zip.Session!;
        var proj = zip.Projection!;

        var z0 = (double)proj.Fields[0].Get(session, produced[0])!;
        var z1 = (double)proj.Fields[1].Get(session, produced[0])!;

        Assert.False(double.IsNaN(z0));
        Assert.False(double.IsNaN(z1));
    }

    // ── 16. Zip item_0.#ALL ───────────────────────────────────────────────────

    [Fact]
    public void ZipProcess_Item0All_ExpandsChild0CatalogLeaves()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 1.25, Id = 10 },
            new() { ZScore = 2.50, Id = 20 }
        });

        var childB = new MockChildProcess<RecordB>(new RecordB[]
        {
            new() { EndTrueZ = 100.0, Label = "row1" },
            new() { EndTrueZ = 200.0, Label = "row2" }
        });

        var zip = new ZipProcess(childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = zip.BindFields(catalogs, new[] { "item_0.#ALL" }, out var error);
        Assert.True(ok, error?.ToString());
        Assert.NotNull(zip.Projection);

        Assert.Equal(2, zip.Projection!.Fields.Count);
        Assert.Equal(new[] { "item_0.ZScore", "item_0.Id" }, zip.Projection.Fields.Select(f => f.ToString()));

        var produced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        Assert.Equal(2, produced.Count);
        var session = zip.Session!;
        var proj = zip.Projection!;

        Assert.Equal(1.25, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal(10, proj.Fields[1].Get(session, produced[0]));

        Assert.Equal(2.50, proj.Fields[0].Get(session, produced[1]));
        Assert.Equal(20, proj.Fields[1].Get(session, produced[1]));
    }

    // ── 17. Zip item_0.#ALL and item_1.#ALL coexist ───────────────────────────

    [Fact]
    public void ZipProcess_Item0All_And_Item1All_CoexistInRequestedOrder()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 3.5, Id = 99 }
        });

        var childB = new MockChildProcess<RecordB>(new RecordB[]
        {
            new() { EndTrueZ = 45.6, Label = "tagB" }
        });

        var zip = new ZipProcess(childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = zip.BindFields(catalogs, new[] { "item_0.#ALL", "item_1.#ALL" }, out var error);
        Assert.True(ok, error?.ToString());
        Assert.NotNull(zip.Projection);

        Assert.Equal(4, zip.Projection!.Fields.Count);
        Assert.Equal(new[]
        {
            "item_0.ZScore", "item_0.Id",
            "item_1.EndTrueZ", "item_1.Label"
        }, zip.Projection.Fields.Select(f => f.ToString()));

        var produced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        Assert.Single(produced);
        var session = zip.Session!;
        var proj = zip.Projection!;

        Assert.Equal(3.5, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal(99, proj.Fields[1].Get(session, produced[0]));
        Assert.Equal(45.6, proj.Fields[2].Get(session, produced[0]));
        Assert.Equal("tagB", proj.Fields[3].Get(session, produced[0]));
    }

    // ── 18. Root #ALL on zip filters intermediate child scopes ────────────────

    [Fact]
    public void ZipProcess_RootAll_FiltersIntermediateChildScopes()
    {
        var childA = new MockChildProcess<RecordA>(new RecordA[]
        {
            new() { ZScore = 1.0, Id = 1 }
        });

        var childB = new MockChildProcess<RecordB>(new RecordB[]
        {
            new() { EndTrueZ = 2.0, Label = "b" }
        });

        var zip = new ZipProcess(childA, childB);
        var catalogs = CreateCatalogs();

        // Root "#ALL" evaluates ProcessArrayStats dynamic schema: item_0 (RecordA) and item_1 (RecordB).
        // Both are metric-bearing types (intermediate scopes), so they are excluded from leaf projection.
        bool ok = zip.BindFields(catalogs, new[] { "#ALL" }, out var error);
        Assert.True(ok, error?.ToString());
        Assert.NotNull(zip.Projection);

        // 0 leaf fields remain
        Assert.Empty(zip.Projection!.Fields);
    }
}
