using JWCFarm.Metrics;

namespace TruthInTheFlip.Farm.Tests;

public class MetricBinderTests
{
    private sealed class Parent
    {
        public int Value { get; init; }
        public Child Nested { get; init; } = new();
    }

    private sealed class Child
    {
        public string Name { get; init; } = "";
    }

    private static MetricCatalogs CreateCatalogs()
    {
        var catalogs = new MetricCatalogs();

        catalogs.Catalogs[typeof(Parent)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["Value"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "Value",
                    ValueType = typeof(int),
                    Help = "Parent value",
                    Getter = (ctx, value) => ((Parent)value).Value
                },
                ["Nested"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "Nested",
                    ValueType = typeof(Child),
                    Help = "Nested child",
                    Getter = (ctx, value) => ((Parent)value).Nested
                }
            }
        };

        catalogs.Catalogs[typeof(Child)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["Name"] = new MetricDescriptor
                {
                    Type = MetricDescriptor.EType.Property,
                    Name = "Name",
                    ValueType = typeof(string),
                    Help = "Child name",
                    Getter = (ctx, value) => ((Child)value).Name
                }
            }
        };

        return catalogs;
    }

    [Fact]
    public void Bind_PreservesRequestedFieldOrder()
    {
        var catalogs = CreateCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(Parent), null,
            out var projection,
            "Nested.Name",
            "Value");

        Assert.True(ok);
        Assert.Equal(2, projection.Fields.Count);
        Assert.Equal(new[] { "Nested", "Name" }, projection.Fields[0].Select(x => x.InstanceDescriptor.Name));
        Assert.Equal(new[] { "Value" }, projection.Fields[1].Select(x => x.InstanceDescriptor.Name));
    }

    [Fact]
    public void Bind_NestedPathCanBeEvaluated()
    {
        var catalogs = CreateCatalogs();
        MetricBinder.Bind(null, catalogs, typeof(Parent), null, out var projection, "Nested.Name");
        var source = new Parent { Nested = new Child { Name = "hello" } };

        object? value = source;
        foreach (var descriptor in projection.Fields.Single())
            value = descriptor.InstanceDescriptor.Getter(null, value!);

        Assert.Equal("hello", value);
    }

    [Fact]
    public void Bind_ReturnsFalseForUnknownNestedMetric()
    {
        var catalogs = CreateCatalogs();

        bool ok = MetricBinder.Bind(null, catalogs, typeof(Parent), null,
            out var projection,
            "Nested.Missing");

        Assert.False(ok);
        Assert.Null(projection);
    }

    [Fact]
    public void Catalogs_CacheReflectedCatalogByType()
    {
        var catalogs = new MetricCatalogs();
        int calls = 0;
        var expected = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>()
        };

        catalogs.Reflect = type =>
        {
            calls++;
            return type == typeof(Parent) ? expected : null;
        };

        Assert.True(catalogs.TryGet(typeof(Parent), out var first));
        Assert.True(catalogs.TryGet(typeof(Parent), out var second));

        Assert.Same(expected, first);
        Assert.Same(first, second);
        Assert.Equal(1, calls);
    }
}
