using FluentCommandLine;

namespace TruthInTheFlip.Farm.Tests;

public class FluentContextDataTests
{
    private sealed record Service(string Name);

    [Fact]
    public void SetAndGet_ReturnRegisteredValue()
    {
        var context = new FluentContextData();
        var expected = new Service("metrics");

        context.Set(expected);

        Assert.Same(expected, context.Get<Service>());
    }

    [Fact]
    public void Set_ReplacesValueForSameType()
    {
        var context = new FluentContextData();
        context.Set(new Service("first"));
        var expected = new Service("second");

        context.Set(expected);

        Assert.Same(expected, context.Get<Service>());
    }

    [Fact]
    public void TryGet_ReturnsFalseForMissingType()
    {
        var context = new FluentContextData();

        bool found = context.TryGet<Service>(out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Get_ThrowsForMissingType()
    {
        var context = new FluentContextData();

        Assert.Throws<KeyNotFoundException>(() => context.Get<Service>());
    }
}
