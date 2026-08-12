using JWCFarm;

namespace TruthInTheFlip.Farm.Tests;

public class FarmProcessTests
{
    private sealed class TestProcess : FarmProcess
    {
        private readonly IEnumerable<object> items;

        public TestProcess(IEnumerable<object> items)
        {
            this.items = items;
        }

        public override Type StatType => typeof(int);

        protected override IEnumerable<object> EnumerateItems(FarmContext context)
            => items;
    }

    private static IEnumerable<object> ThrowAfterOne()
    {
        yield return 1;
        throw new InvalidOperationException("boom");
    }

    [Fact]
    public void Execute_RunsLifecycleInOrder()
    {
        var events = new List<string>();
        var process = new TestProcess(new object[] { 1, 2, 3 });
        process.Actions = new ProcessActions(
            begin: _ => events.Add("begin"),
            process: (_, item) => events.Add($"item:{item}"),
            end: _ => events.Add("end"),
            abort: (_, _) => events.Add("abort"));

        process.Execute(new FarmContext());

        Assert.Equal(
            new[] { "begin", "item:1", "item:2", "item:3", "end" },
            events);
    }

    [Fact]
    public void Execute_AbortsAndRethrowsWhenEnumerationFails()
    {
        var events = new List<string>();
        var process = new TestProcess(ThrowAfterOne());
        process.Actions = new ProcessActions(
            begin: _ => events.Add("begin"),
            process: (_, item) => events.Add($"item:{item}"),
            end: _ => events.Add("end"),
            abort: (_, ex) => events.Add($"abort:{ex.Message}"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => process.Execute(new FarmContext()));

        Assert.Equal("boom", exception.Message);
        Assert.Equal(
            new[] { "begin", "item:1", "abort:boom" },
            events);
    }

    [Fact]
    public void Execute_AllowsProcessActionToBeAbsent()
    {
        var events = new List<string>();
        var process = new TestProcess(new object[] { 1, 2, 3 });
        process.Actions = new ProcessActions(
            begin: _ => events.Add("begin"),
            end: _ => events.Add("end"));

        process.Execute(new FarmContext());

        Assert.Equal(new[] { "begin", "end" }, events);
    }
}
