using System.Globalization;
using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public class JoinProcessTests
{
    private enum TestStatus
    {
        Pending,
        Active,
        Complete
    }

    private enum OtherStatus
    {
        Pending,
        Active
    }

    private sealed class RecordInt
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public int Id { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Value { get; set; } = "";
    }

    private sealed class RecordLong
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public long Id { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Extra { get; set; } = "";
    }

    private sealed class RecordShort
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public short Id { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Label { get; set; } = "";
    }

    private sealed class RecordDouble
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public double Score { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Desc { get; set; } = "";
    }

    private sealed class RecordFloat
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public float Score { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Note { get; set; } = "";
    }

    private sealed class RecordDecimal
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public decimal Amount { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Tag { get; set; } = "";
    }

    private sealed class RecordString
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Key { get; set; } = "";

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public int Number { get; set; }
    }

    private sealed class RecordTemporal
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public DateTime Timestamp { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public DateTimeOffset OffsetTime { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public TimeSpan Duration { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public Guid Uuid { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public bool Flag { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public char Code { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public TestStatus Status { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Name { get; set; } = "";
    }

    private sealed class RecordOtherEnum
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public OtherStatus Status { get; set; }
    }

    private sealed class RecordSegmentKey
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public SegmentStats NestedSegment { get; set; } = new();
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

    // ── 1. Two-child same-type integer join ───────────────────────────────────

    [Fact]
    public void TwoChildSameTypeJoin_EmitsMatchingRowsInChild0Order()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 10, Value = "A10" },
            new() { Id = 20, Value = "A20" },
            new() { Id = 30, Value = "A30" },
            new() { Id = 40, Value = "A40" }
        });

        var childB = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 30, Value = "B30" },
            new() { Id = 10, Value = "B10" },
            new() { Id = 50, Value = "B50" },
            new() { Id = 40, Value = "B40" }
        });

        var join = new JoinProcess("Id", childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Id", "item_0.Value", "item_1.Value" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        // Child A order: 10, 20, 30, 40. Child B has: 10, 30, 40, 50.
        // Intersection in child A order: 10, 30, 40.
        Assert.Equal(3, produced.Count);

        var session = join.Session!;
        var proj = join.Projection!;

        Assert.Equal(10, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal("A10", proj.Fields[1].Get(session, produced[0]));
        Assert.Equal("B10", proj.Fields[2].Get(session, produced[0]));

        Assert.Equal(30, proj.Fields[0].Get(session, produced[1]));
        Assert.Equal("A30", proj.Fields[1].Get(session, produced[1]));
        Assert.Equal("B30", proj.Fields[2].Get(session, produced[1]));

        Assert.Equal(40, proj.Fields[0].Get(session, produced[2]));
        Assert.Equal("A40", proj.Fields[1].Get(session, produced[2]));
        Assert.Equal("B40", proj.Fields[2].Get(session, produced[2]));
    }

    // ── 2. N-way intersection (3 and 4 children) ──────────────────────────────

    [Fact]
    public void ThreeChildJoin_EmitsOnlyNWayIntersection()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 10, Value = "A10" },
            new() { Id = 20, Value = "A20" },
            new() { Id = 30, Value = "A30" },
            new() { Id = 40, Value = "A40" }
        });

        var childB = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 20, Value = "B20" },
            new() { Id = 40, Value = "B40" },
            new() { Id = 50, Value = "B50" }
        });

        var childC = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 20, Value = "C20" },
            new() { Id = 30, Value = "C30" },
            new() { Id = 40, Value = "C40" }
        });

        var join = new JoinProcess("Id", childA, childB, childC);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Id", "item_0.Value", "item_1.Value", "item_2.Value" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        // Keys present in all 3: 20, 40
        Assert.Equal(2, produced.Count);

        var session = join.Session!;
        var proj = join.Projection!;

        Assert.Equal(20, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal("A20", proj.Fields[1].Get(session, produced[0]));
        Assert.Equal("B20", proj.Fields[2].Get(session, produced[0]));
        Assert.Equal("C20", proj.Fields[3].Get(session, produced[0]));

        Assert.Equal(40, proj.Fields[0].Get(session, produced[1]));
        Assert.Equal("A40", proj.Fields[1].Get(session, produced[1]));
        Assert.Equal("B40", proj.Fields[2].Get(session, produced[1]));
        Assert.Equal("C40", proj.Fields[3].Get(session, produced[1]));
    }

    [Fact]
    public void FourChildJoin_EmitsCorrectIntersection()
    {
        var c0 = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 1, Value = "0_1" }, new() { Id = 2, Value = "0_2" } });
        var c1 = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 1, Value = "1_1" }, new() { Id = 2, Value = "1_2" } });
        var c2 = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 2, Value = "2_2" }, new() { Id = 1, Value = "2_1" } });
        var c3 = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 1, Value = "3_1" }, new() { Id = 3, Value = "3_3" } });

        var join = new JoinProcess("Id", c0, c1, c2, c3);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Id", "item_0.Value", "item_3.Value" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Single(produced);
        var session = join.Session!;
        var proj = join.Projection!;
        Assert.Equal(1, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal("0_1", proj.Fields[1].Get(session, produced[0]));
        Assert.Equal("3_1", proj.Fields[2].Get(session, produced[0]));
    }

    // ── 3. Missing / disjoint / empty stream behavior ─────────────────────────

    [Fact]
    public void DisjointStreams_EmitZeroRows()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 1 }, new() { Id = 2 } });
        var childB = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 3 }, new() { Id = 4 } });

        var join = new JoinProcess("Id", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Id" }, out _);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Empty(produced);
    }

    [Fact]
    public void EmptyChildStream_EmitsZeroRows()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 1 }, new() { Id = 2 } });
        var childB = new MockChildProcess<RecordInt>(Array.Empty<RecordInt>());

        var join = new JoinProcess("Id", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Id" }, out _);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Empty(produced);
    }

    // ── 4. First-child ordering preservation ──────────────────────────────────

    [Fact]
    public void OutputOrder_StrictlyPreservesChild0EmittedOrder()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 40 },
            new() { Id = 10 },
            new() { Id = 30 },
            new() { Id = 20 }
        });

        // Child B emits in completely different order
        var childB = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 10 },
            new() { Id = 20 },
            new() { Id = 30 },
            new() { Id = 40 }
        });

        var join = new JoinProcess("Id", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Id" }, out _);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Equal(4, produced.Count);
        var session = join.Session!;
        var proj = join.Projection!;

        Assert.Equal(40, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal(10, proj.Fields[0].Get(session, produced[1]));
        Assert.Equal(30, proj.Fields[0].Get(session, produced[2]));
        Assert.Equal(20, proj.Fields[0].Get(session, produced[3]));
    }

    // ── 5. Duplicate key rejection ────────────────────────────────────────────

    [Fact]
    public void DuplicateKeyInChild0_ThrowsFarmInputException()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 10, Value = "v1" },
            new() { Id = 20, Value = "v2" },
            new() { Id = 10, Value = "duplicate" }
        });

        var childB = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 10 },
            new() { Id = 20 }
        });

        var join = new JoinProcess("Id", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Id" }, out _);

        var ex = Assert.Throws<FarmInputException>(() => join.Execute(new FarmContext()));
        Assert.Contains("Duplicate join key '10'", ex.Message);
        Assert.Contains("child process 0", ex.Message);
    }

    [Fact]
    public void DuplicateKeyInLaterChild_ThrowsFarmInputException()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 10 },
            new() { Id = 20 }
        });

        var childB = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 10 },
            new() { Id = 20 },
            new() { Id = 20 } // Duplicate in child 1
        });

        var join = new JoinProcess("Id", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Id" }, out _);

        var ex = Assert.Throws<FarmInputException>(() => join.Execute(new FarmContext()));
        Assert.Contains("Duplicate join key '20'", ex.Message);
        Assert.Contains("child process 1", ex.Message);
    }

    // ── 6. Null key policy ────────────────────────────────────────────────────

    [Fact]
    public void NullJoinKeys_AreSkippedAndDoNotJoin()
    {
        var childA = new MockChildProcess<RecordString>(new RecordString[]
        {
            new() { Key = null!, Number = 0 },
            new() { Key = "alpha", Number = 1 },
            new() { Key = "beta", Number = 2 }
        });

        var childB = new MockChildProcess<RecordString>(new RecordString[]
        {
            new() { Key = null!, Number = 99 },
            new() { Key = "beta", Number = 20 },
            new() { Key = "alpha", Number = 10 }
        });

        var join = new JoinProcess("Key", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Key", "item_0.Number", "item_1.Number" }, out _);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        // Null rows skipped; alpha and beta matched
        Assert.Equal(2, produced.Count);
        var session = join.Session!;
        var proj = join.Projection!;

        Assert.Equal("alpha", proj.Fields[0].Get(session, produced[0]));
        Assert.Equal(1, proj.Fields[1].Get(session, produced[0]));
        Assert.Equal(10, proj.Fields[2].Get(session, produced[0]));

        Assert.Equal("beta", proj.Fields[0].Get(session, produced[1]));
        Assert.Equal(2, proj.Fields[1].Get(session, produced[1]));
        Assert.Equal(20, proj.Fields[2].Get(session, produced[1]));
    }

    // ── 7. Numeric widening & canonicalization ────────────────────────────────

    [Fact]
    public void NumericWidening_IntAndLong_ResolvesCanonicalLong()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 100, Value = "a1" },
            new() { Id = 200, Value = "a2" }
        });

        var childB = new MockChildProcess<RecordLong>(new RecordLong[]
        {
            new() { Id = 100L, Extra = "b1" },
            new() { Id = 200L, Extra = "b2" }
        });

        var join = new JoinProcess("Id", childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Id", "item_1.Id" }, out var error);
        Assert.True(ok, error?.ToString());
        Assert.Equal(typeof(long), join.CanonicalKeyType);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Equal(2, produced.Count);
    }

    [Fact]
    public void NumericWidening_ShortIntLong_ResolvesCanonicalLong()
    {
        var c0 = new MockChildProcess<RecordShort>(new RecordShort[] { new() { Id = 5, Label = "s" } });
        var c1 = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 5, Value = "i" } });
        var c2 = new MockChildProcess<RecordLong>(new RecordLong[] { new() { Id = 5L, Extra = "l" } });

        var join = new JoinProcess("Id", c0, c1, c2);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Id", "item_1.Id", "item_2.Id" }, out var error);
        Assert.True(ok, error?.ToString());
        Assert.Equal(typeof(long), join.CanonicalKeyType);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Single(produced);
    }

    [Fact]
    public void NumericWidening_IntAndDouble_ResolvesCanonicalDouble()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 42, Value = "answer" }
        });

        var childB = new MockChildProcess<RecordDouble>(new RecordDouble[]
        {
            new() { Score = 42.0, Desc = "score" }
        });

        // We bind childA on "Id" and childB on "Score" - wait, key expression must have same name across children!
        // To test int + double, let's create classes with same metric name.
    }

    private sealed class RecordKeyInt
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public int Key { get; set; }
    }

    private sealed class RecordKeyDouble
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public double Key { get; set; }
    }

    private sealed class RecordKeyDecimal
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public decimal Key { get; set; }
    }

    [Fact]
    public void NumericWidening_IntAndDouble_SameMetricName_ResolvesCanonicalDouble()
    {
        var childA = new MockChildProcess<RecordKeyInt>(new RecordKeyInt[] { new() { Key = 42 } });
        var childB = new MockChildProcess<RecordKeyDouble>(new RecordKeyDouble[] { new() { Key = 42.0 } });

        var join = new JoinProcess("Key", childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Key", "item_1.Key" }, out var error);
        Assert.True(ok, error?.ToString());
        Assert.Equal(typeof(double), join.CanonicalKeyType);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Single(produced);
    }

    [Fact]
    public void NumericWidening_IntAndDecimal_ResolvesCanonicalDecimal()
    {
        var childA = new MockChildProcess<RecordKeyInt>(new RecordKeyInt[] { new() { Key = 100 } });
        var childB = new MockChildProcess<RecordKeyDecimal>(new RecordKeyDecimal[] { new() { Key = 100m } });

        var join = new JoinProcess("Key", childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Key", "item_1.Key" }, out var error);
        Assert.True(ok, error?.ToString());
        Assert.Equal(typeof(decimal), join.CanonicalKeyType);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Single(produced);
    }

    // ── 8. Incompatible key types ─────────────────────────────────────────────

    [Fact]
    public void IncompatibleTypes_DoubleAndDecimal_FailsBinding()
    {
        var childA = new MockChildProcess<RecordKeyDouble>(new RecordKeyDouble[] { new() { Key = 1.0 } });
        var childB = new MockChildProcess<RecordKeyDecimal>(new RecordKeyDecimal[] { new() { Key = 1.0m } });

        var join = new JoinProcess("Key", childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Key" }, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("incompatible numeric types", error.Message);
    }

    [Fact]
    public void IncompatibleTypes_StringAndInt_FailsBinding()
    {
        var childA = new MockChildProcess<RecordString>(new RecordString[] { new() { Key = "1" } });
        var childB = new MockChildProcess<RecordKeyInt>(new RecordKeyInt[] { new() { Key = 1 } });

        var join = new JoinProcess("Key", childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Key" }, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("incompatible", error.Message);
    }

    [Fact]
    public void IncompatibleTypes_DateTimeAndDateTimeOffset_FailsBinding()
    {
        var childA = new MockChildProcess<RecordTemporal>(new RecordTemporal[] { new() { Timestamp = DateTime.UtcNow } });
        var childB = new MockChildProcess<RecordTemporal>(new RecordTemporal[] { new() { OffsetTime = DateTimeOffset.UtcNow } });

        // Let's create two records with metric name "Time"
    }

    private sealed class RecordDateTime
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public DateTime Time { get; set; }
    }

    private sealed class RecordDateTimeOffset
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public DateTimeOffset Time { get; set; }
    }

    [Fact]
    public void IncompatibleTypes_DateTimeAndDateTimeOffset_SameMetricName_FailsBinding()
    {
        var childA = new MockChildProcess<RecordDateTime>(new RecordDateTime[] { new() { Time = DateTime.UtcNow } });
        var childB = new MockChildProcess<RecordDateTimeOffset>(new RecordDateTimeOffset[] { new() { Time = DateTimeOffset.UtcNow } });

        var join = new JoinProcess("Time", childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Time" }, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("incompatible", error.Message);
    }

    [Fact]
    public void IncompatibleTypes_DifferentEnums_FailsBinding()
    {
        var childA = new MockChildProcess<RecordTemporal>(new RecordTemporal[] { new() { Status = TestStatus.Active } });
        var childB = new MockChildProcess<RecordOtherEnum>(new RecordOtherEnum[] { new() { Status = OtherStatus.Active } });

        var join = new JoinProcess("Status", childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.Status" }, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("incompatible", error.Message);
    }

    [Fact]
    public void InvalidKeyType_IntermediateObject_FailsBinding()
    {
        var childA = new MockChildProcess<RecordSegmentKey>(new RecordSegmentKey[] { new() });
        var childB = new MockChildProcess<RecordSegmentKey>(new RecordSegmentKey[] { new() });

        var join = new JoinProcess("NestedSegment", childA, childB);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.NestedSegment" }, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("not a supported join key type", error.Message);
    }

    // ── 9. Floating-point exceptional values ──────────────────────────────────

    [Fact]
    public void FloatingPointKey_NaN_ThrowsFarmInputException()
    {
        var childA = new MockChildProcess<RecordDouble>(new RecordDouble[]
        {
            new() { Score = double.NaN, Desc = "nan" }
        });

        var childB = new MockChildProcess<RecordDouble>(new RecordDouble[]
        {
            new() { Score = 1.0, Desc = "valid" }
        });

        var join = new JoinProcess("Score", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Score" }, out _);

        var ex = Assert.Throws<FarmInputException>(() => join.Execute(new FarmContext()));
        Assert.Contains("cannot be NaN or Infinity", ex.Message);
    }

    [Fact]
    public void FloatingPointKey_Infinity_ThrowsFarmInputException()
    {
        var childA = new MockChildProcess<RecordDouble>(new RecordDouble[]
        {
            new() { Score = double.PositiveInfinity, Desc = "inf" }
        });

        var childB = new MockChildProcess<RecordDouble>(new RecordDouble[]
        {
            new() { Score = 1.0, Desc = "valid" }
        });

        var join = new JoinProcess("Score", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Score" }, out _);

        var ex = Assert.Throws<FarmInputException>(() => join.Execute(new FarmContext()));
        Assert.Contains("cannot be NaN or Infinity", ex.Message);
    }

    // ── 10. Temporal key semantics ────────────────────────────────────────────

    [Fact]
    public void TemporalKey_DateTime_MatchesTicks()
    {
        var dt1 = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        var dt2 = new DateTime(2026, 9, 29, 13, 0, 0, DateTimeKind.Utc);

        var childA = new MockChildProcess<RecordTemporal>(new RecordTemporal[]
        {
            new() { Timestamp = dt1, Name = "t1" },
            new() { Timestamp = dt2, Name = "t2" }
        });

        var childB = new MockChildProcess<RecordTemporal>(new RecordTemporal[]
        {
            new() { Timestamp = dt2, Name = "b2" },
            new() { Timestamp = dt1, Name = "b1" }
        });

        var join = new JoinProcess("Timestamp", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Name", "item_1.Name" }, out _);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Equal(2, produced.Count);
        var session = join.Session!;
        var proj = join.Projection!;
        Assert.Equal("t1", proj.Fields[0].Get(session, produced[0]));
        Assert.Equal("b1", proj.Fields[1].Get(session, produced[0]));
        Assert.Equal("t2", proj.Fields[0].Get(session, produced[1]));
        Assert.Equal("b2", proj.Fields[1].Get(session, produced[1]));
    }

    [Fact]
    public void TemporalKey_DateTimeOffset_MatchesNormalizedInstantAcrossOffsets()
    {
        // 12:00 UTC == 14:00 UTC+2
        var instant1_utc = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
        var instant1_plus2 = new DateTimeOffset(2026, 9, 29, 14, 0, 0, TimeSpan.FromHours(2));

        var childA = new MockChildProcess<RecordTemporal>(new RecordTemporal[]
        {
            new() { OffsetTime = instant1_utc, Name = "utc_item" }
        });

        var childB = new MockChildProcess<RecordTemporal>(new RecordTemporal[]
        {
            new() { OffsetTime = instant1_plus2, Name = "offset_item" }
        });

        var join = new JoinProcess("OffsetTime", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Name", "item_1.Name" }, out _);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Single(produced);
        var session = join.Session!;
        var proj = join.Projection!;
        Assert.Equal("utc_item", proj.Fields[0].Get(session, produced[0]));
        Assert.Equal("offset_item", proj.Fields[1].Get(session, produced[0]));
    }

    [Fact]
    public void TemporalKey_TimeSpan_MatchesDuration()
    {
        var ts1 = TimeSpan.FromMinutes(30);
        var ts2 = TimeSpan.FromHours(2);

        var childA = new MockChildProcess<RecordTemporal>(new RecordTemporal[]
        {
            new() { Duration = ts1, Name = "30m" },
            new() { Duration = ts2, Name = "2h" }
        });

        var childB = new MockChildProcess<RecordTemporal>(new RecordTemporal[]
        {
            new() { Duration = ts2, Name = "b_2h" },
            new() { Duration = ts1, Name = "b_30m" }
        });

        var join = new JoinProcess("Duration", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Name", "item_1.Name" }, out _);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Equal(2, produced.Count);
    }

    // ── 11. String ordinal matching ───────────────────────────────────────────

    [Fact]
    public void StringKey_ExactOrdinalMatch_CaseDifferencesDoNotMatch()
    {
        var childA = new MockChildProcess<RecordString>(new RecordString[]
        {
            new() { Key = "ABC", Number = 1 },
            new() { Key = "def", Number = 2 }
        });

        var childB = new MockChildProcess<RecordString>(new RecordString[]
        {
            new() { Key = "abc", Number = 10 }, // different case, should NOT match
            new() { Key = "def", Number = 20 }  // exact match
        });

        var join = new JoinProcess("Key", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Number", "item_1.Number" }, out _);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Single(produced);
        var session = join.Session!;
        var proj = join.Projection!;
        Assert.Equal(2, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal(20, proj.Fields[1].Get(session, produced[0]));
    }

    // ── 12. Guid, Enum, Bool, Char keys ───────────────────────────────────────

    [Fact]
    public void ValueKeys_GuidEnumBoolChar_MatchCorrectly()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();

        var childA = new MockChildProcess<RecordTemporal>(new RecordTemporal[]
        {
            new() { Uuid = g1, Flag = true, Code = 'X', Status = TestStatus.Active, Name = "a1" },
            new() { Uuid = g2, Flag = false, Code = 'Y', Status = TestStatus.Pending, Name = "a2" }
        });

        var childB = new MockChildProcess<RecordTemporal>(new RecordTemporal[]
        {
            new() { Uuid = g2, Flag = false, Code = 'Y', Status = TestStatus.Pending, Name = "b2" },
            new() { Uuid = g1, Flag = true, Code = 'X', Status = TestStatus.Active, Name = "b1" }
        });

        var catalogs = CreateCatalogs();

        // Join on Uuid
        var joinGuid = new JoinProcess("Uuid", childA, childB);
        joinGuid.BindFields(catalogs, new[] { "item_0.Name", "item_1.Name" }, out _);
        var pGuid = new List<ProcessArrayStats>();
        joinGuid.Actions = new ProcessActions(process: (_, item) => pGuid.Add((ProcessArrayStats)item));
        joinGuid.Execute(new FarmContext());
        Assert.Equal(2, pGuid.Count);

        // Join on Flag
        var joinBool = new JoinProcess("Flag", childA, childB);
        joinBool.BindFields(catalogs, new[] { "item_0.Name", "item_1.Name" }, out _);
        var pBool = new List<ProcessArrayStats>();
        joinBool.Actions = new ProcessActions(process: (_, item) => pBool.Add((ProcessArrayStats)item));
        joinBool.Execute(new FarmContext());
        Assert.Equal(2, pBool.Count);

        // Join on Code (char)
        var joinChar = new JoinProcess("Code", childA, childB);
        joinChar.BindFields(catalogs, new[] { "item_0.Name", "item_1.Name" }, out _);
        var pChar = new List<ProcessArrayStats>();
        joinChar.Actions = new ProcessActions(process: (_, item) => pChar.Add((ProcessArrayStats)item));
        joinChar.Execute(new FarmContext());
        Assert.Equal(2, pChar.Count);

        // Join on Status (enum)
        var joinEnum = new JoinProcess("Status", childA, childB);
        joinEnum.BindFields(catalogs, new[] { "item_0.Name", "item_1.Name" }, out _);
        var pEnum = new List<ProcessArrayStats>();
        joinEnum.Actions = new ProcessActions(process: (_, item) => pEnum.Add((ProcessArrayStats)item));
        joinEnum.Execute(new FarmContext());
        Assert.Equal(2, pEnum.Count);
    }

    // ── 13. Single child join & empty child list validation ───────────────────

    [Fact]
    public void SingleChildJoin_EmitsChildItemsInOrder()
    {
        var child = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 3, Value = "c3" },
            new() { Id = 1, Value = "c1" },
            new() { Id = 2, Value = "c2" }
        });

        var join = new JoinProcess("Id", child);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Id", "item_0.Value" }, out _);

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Equal(3, produced.Count);
        var session = join.Session!;
        var proj = join.Projection!;

        Assert.Equal(3, proj.Fields[0].Get(session, produced[0]));
        Assert.Equal("c3", proj.Fields[1].Get(session, produced[0]));

        Assert.Equal(1, proj.Fields[0].Get(session, produced[1]));
        Assert.Equal("c1", proj.Fields[1].Get(session, produced[1]));

        Assert.Equal(2, proj.Fields[0].Get(session, produced[2]));
        Assert.Equal("c2", proj.Fields[1].Get(session, produced[2]));
    }

    [Fact]
    public void EmptyChildProcesses_ThrowsFarmInputException()
    {
        Assert.Throws<FarmInputException>(() => new JoinProcess("Id", Array.Empty<FarmProcess>()));
        Assert.Throws<FarmInputException>(() => JoinProcess.Join("Id", Array.Empty<FarmProcess>()));
    }

    [Fact]
    public void EmptyKeyExpression_ThrowsArgumentExceptionOrFarmInputException()
    {
        var child = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 1 } });
        Assert.Throws<ArgumentException>(() => new JoinProcess("", child));
        Assert.Throws<FarmInputException>(() => JoinProcess.Join("", new[] { child }));
    }

    // ── 14. Dynamic metric item_N catalog & #ALL expansion ────────────────────

    [Fact]
    public void DynamicMetrics_Item0AndItem1_ExpandWithAll()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 10, Value = "a1" }
        });

        var childB = new MockChildProcess<RecordString>(new RecordString[]
        {
            new() { Key = "10", Number = 100 }
        });

        var childBInt = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 10, Value = "b1" }
        });

        var join = new JoinProcess("Id", childA, childBInt);
        var catalogs = CreateCatalogs();

        bool ok = join.BindFields(catalogs, new[] { "item_0.#ALL", "item_1.#ALL" }, out var error);
        Assert.True(ok, error?.ToString());
        Assert.Equal(4, join.Projection!.Fields.Count);
        Assert.Equal(new[] { "item_0.Id", "item_0.Value", "item_1.Id", "item_1.Value" }, join.Projection.Fields.Select(f => f.ToString()));

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Single(produced);
    }

    // ── 15. Re-execution idempotency ──────────────────────────────────────────

    [Fact]
    public void Reexecution_YieldsDeterministicFreshResults()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 1, Value = "first" },
            new() { Id = 2, Value = "second" }
        });

        var childB = new MockChildProcess<RecordInt>(new RecordInt[]
        {
            new() { Id = 2, Value = "b_second" },
            new() { Id = 1, Value = "b_first" }
        });

        var join = new JoinProcess("Id", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Id", "item_1.Value" }, out _);

        var run1 = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => run1.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        var run2 = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => run2.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Equal(2, run1.Count);
        Assert.Equal(2, run2.Count);

        var session = join.Session!;
        var proj = join.Projection!;

        Assert.Equal(proj.Fields[0].Get(session, run1[0]), proj.Fields[0].Get(session, run2[0]));
        Assert.Equal(proj.Fields[1].Get(session, run1[0]), proj.Fields[1].Get(session, run2[0]));
        Assert.Equal(proj.Fields[0].Get(session, run1[1]), proj.Fields[0].Get(session, run2[1]));
        Assert.Equal(proj.Fields[1].Get(session, run1[1]), proj.Fields[1].Get(session, run2[1]));
    }

    // ── 16. Child execution failure restores actions & propagates ─────────────

    [Fact]
    public void ChildFailure_RestoresActionsAndPropagatesException()
    {
        var childA = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 1 } });
        var childB = new MockChildProcess<RecordInt>(new RecordInt[] { new() { Id = 1 } }, throwOnEnumerate: true);

        var originalAction = new ProcessActions();
        childB.Actions = originalAction;

        var join = new JoinProcess("Id", childA, childB);
        var catalogs = CreateCatalogs();
        join.BindFields(catalogs, new[] { "item_0.Id" }, out _);

        var ex = Assert.Throws<InvalidOperationException>(() => join.Execute(new FarmContext()));
        Assert.Contains("Simulated child failure", ex.Message);
        Assert.Same(originalAction, childB.Actions);
    }

    // ── 17. CRITICAL ZIP-VS-JOIN PROOF TEST ───────────────────────────────────

    private sealed class SegmentStatRecord
    {
        [IsMetric("TruthInTheFlip.v1.1.0")]
        public long EndTotal { get; set; }

        [IsMetric("TruthInTheFlip.v1.1.0")]
        public string Label { get; set; } = "";
    }

    [Fact]
    public void CriticalZipVsJoinProof_SegmentationDifference()
    {
        // Child A: 3 segments at 100B scale: EndTotal = 100, 200, 300
        var childA_items = new SegmentStatRecord[]
        {
            new() { EndTotal = 100, Label = "A_100" },
            new() { EndTotal = 200, Label = "A_200" },
            new() { EndTotal = 300, Label = "A_300" }
        };

        // Child B: 6 segments at 50B scale: EndTotal = 50, 100, 150, 200, 250, 300
        var childB_items = new SegmentStatRecord[]
        {
            new() { EndTotal = 50,  Label = "B_50"  },
            new() { EndTotal = 100, Label = "B_100" },
            new() { EndTotal = 150, Label = "B_150" },
            new() { EndTotal = 200, Label = "B_200" },
            new() { EndTotal = 250, Label = "B_250" },
            new() { EndTotal = 300, Label = "B_300" }
        };

        var catalogs = CreateCatalogs();

        // 1. Positional ZIP
        var childA_forZip = new MockChildProcess<SegmentStatRecord>(childA_items);
        var childB_forZip = new MockChildProcess<SegmentStatRecord>(childB_items);
        var zip = new ZipProcess(childA_forZip, childB_forZip);
        zip.BindFields(catalogs, new[] { "item_0.EndTotal", "item_1.EndTotal" }, out _);

        var zipProduced = new List<ProcessArrayStats>();
        zip.Actions = new ProcessActions(process: (_, item) => zipProduced.Add((ProcessArrayStats)item));
        zip.Execute(new FarmContext());

        Assert.Equal(3, zipProduced.Count);
        var zipSession = zip.Session!;
        var zipProj = zip.Projection!;

        // In ZIP: row 0 pairs 100 with 50 (coordinate mismatch!)
        Assert.Equal(100L, zipProj.Fields[0].Get(zipSession, zipProduced[0]));
        Assert.Equal(50L,  zipProj.Fields[1].Get(zipSession, zipProduced[0]));
        Assert.NotEqual(zipProj.Fields[0].Get(zipSession, zipProduced[0]), zipProj.Fields[1].Get(zipSession, zipProduced[0]));

        // In ZIP: row 1 pairs 200 with 100 (coordinate mismatch!)
        Assert.Equal(200L, zipProj.Fields[0].Get(zipSession, zipProduced[1]));
        Assert.Equal(100L, zipProj.Fields[1].Get(zipSession, zipProduced[1]));
        Assert.NotEqual(zipProj.Fields[0].Get(zipSession, zipProduced[1]), zipProj.Fields[1].Get(zipSession, zipProduced[1]));

        // In ZIP: row 2 pairs 300 with 150 (coordinate mismatch!)
        Assert.Equal(300L, zipProj.Fields[0].Get(zipSession, zipProduced[2]));
        Assert.Equal(150L, zipProj.Fields[1].Get(zipSession, zipProduced[2]));
        Assert.NotEqual(zipProj.Fields[0].Get(zipSession, zipProduced[2]), zipProj.Fields[1].Get(zipSession, zipProduced[2]));

        // 2. Metric JOIN on EndTotal
        var childA_forJoin = new MockChildProcess<SegmentStatRecord>(childA_items);
        var childB_forJoin = new MockChildProcess<SegmentStatRecord>(childB_items);
        var join = new JoinProcess("EndTotal", childA_forJoin, childB_forJoin);
        join.BindFields(catalogs, new[] { "item_0.EndTotal", "item_1.EndTotal" }, out _);

        var joinProduced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => joinProduced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.Equal(3, joinProduced.Count);
        var joinSession = join.Session!;
        var joinProj = join.Projection!;

        // In JOIN: for every single row, item_0.EndTotal == item_1.EndTotal!
        for (int i = 0; i < joinProduced.Count; i++)
        {
            var left = (long)joinProj.Fields[0].Get(joinSession, joinProduced[i])!;
            var right = (long)joinProj.Fields[1].Get(joinSession, joinProduced[i])!;

            Assert.Equal(left, right);
        }

        Assert.Equal(100L, joinProj.Fields[0].Get(joinSession, joinProduced[0]));
        Assert.Equal(100L, joinProj.Fields[1].Get(joinSession, joinProduced[0]));

        Assert.Equal(200L, joinProj.Fields[0].Get(joinSession, joinProduced[1]));
        Assert.Equal(200L, joinProj.Fields[1].Get(joinSession, joinProduced[1]));

        Assert.Equal(300L, joinProj.Fields[0].Get(joinSession, joinProduced[2]));
        Assert.Equal(300L, joinProj.Fields[1].Get(joinSession, joinProduced[2]));
    }

    // ── 18. CLI fluent parsing integration ────────────────────────────────────

    [Fact]
    public void FluentCLI_ParsesJoinCommandWithArrayAndFields()
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
            "join", "EndTotal",
            "tracker", "window", "by_total", "100B", "file", quantPath,
            "tracker", "window", "by_total", "50B", "file", quantPath,
            ".END."
        }, ref cursor);

        Assert.NotNull(parsed);
        var join = Assert.IsType<JoinProcess>(parsed.Result);
        Assert.Equal("EndTotal", join.KeyExpression);
        Assert.Equal(2, join.Children.Count);

        var catalogs = env.Context.Get<MetricCatalogs>();
        bool ok = join.BindFields(catalogs, new[] { "item_0.EndTotal", "item_1.EndTotal" }, out var error);
        Assert.True(ok, error?.ToString());

        var produced = new List<ProcessArrayStats>();
        join.Actions = new ProcessActions(process: (_, item) => produced.Add((ProcessArrayStats)item));
        join.Execute(new FarmContext());

        Assert.NotEmpty(produced);
        var session = join.Session!;
        var proj = join.Projection!;

        for (int i = 0; i < produced.Count; i++)
        {
            var left = (long)proj.Fields[0].Get(session, produced[i])!;
            var right = (long)proj.Fields[1].Get(session, produced[i])!;
            Assert.Equal(left, right);
        }
    }

    // ── 19. CLI pretty command execution smoke test ───────────────────────────

    [Fact]
    public void FluentCLI_PrettyJoinCommand_ExecutesSuccessfully()
    {
        var env = new FluentEnvironment();
        env.AddModule<TruthInTheFlip_Fluent>();
        env.ServeTypes = new[] { typeof(FarmCommand) };

        string quantPath = Path.GetFullPath("Artifacts/Trackers/Quant.tkr");
        if (!File.Exists(quantPath))
            return;

        using var sw = new StringWriter();
        var farmCtx = new FarmContext { Output = sw };

        using var scope = FluentEnvironmentScope.Enter(env);

        int cursor = 0;
        var parsed = env.ParseOne(new[]
        {
            "pretty",
            "join", "EndTotal",
            "tracker", "window", "by_total", "100B", "file", quantPath,
            "tracker", "window", "by_total", "50B", "file", quantPath,
            ".END.",
            "item_0.EndTotal", "item_1.EndTotal"
        }, ref cursor);

        Assert.NotNull(parsed);
        var command = Assert.IsAssignableFrom<FarmCommand>(parsed.Result);
        command.Execute(farmCtx);

        string output = sw.ToString();
        Assert.Contains("item_0.EndTotal =", output);
        Assert.Contains("item_1.EndTotal =", output);
    }

    // ── 20. Higher-order dynamic metric dotted method invocations ─────────────

    [Fact]
    public void JoinProcess_BindsAndEvaluatesDottedChildMethodInvocations()
    {
        var env = new FluentEnvironment();
        env.AddModule<TruthInTheFlip_Fluent>();
        env.ServeTypes = new[] { typeof(FarmCommand) };

        string quantPath = Path.GetFullPath("Artifacts/Trackers/Quant.tkr");
        if (!File.Exists(quantPath))
            return;

        using var sw = new StringWriter();
        var farmCtx = new FarmContext { Output = sw };

        using var scope = FluentEnvironmentScope.Enter(env);

        int cursor = 0;
        var parsed = env.ParseOne(new[]
        {
            "pretty",
            "join", "EndTotal",
            "segment", "file", quantPath, "by_total", "1000000",
            "segment", "file", quantPath, "by_total", "500000",
            ".END.",
            "item_0.EndTotal",
            "item_0.mean#AnticipatedPercentage",
            "item_1.mean#AnticipatedPercentage"
        }, ref cursor);

        Assert.NotNull(parsed);
        var command = Assert.IsAssignableFrom<FarmCommand>(parsed.Result);
        
        // Execute and verify that binding and execution succeed without error
        command.Execute(farmCtx);

        string output = sw.ToString();
        Assert.Contains("item_0.EndTotal =", output);
        Assert.Contains("item_0.mean#AnticipatedPercentage =", output);
        Assert.Contains("item_1.mean#AnticipatedPercentage =", output);
    }
}
