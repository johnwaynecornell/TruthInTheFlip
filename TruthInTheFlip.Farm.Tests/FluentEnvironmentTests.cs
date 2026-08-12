using FluentCommandLine;

namespace TruthInTheFlip.Farm.Tests;

public class FluentEnvironmentTests
{
    private sealed record Marker(string Value);
    private sealed record ParseResult(string Value);

    private enum TestMode
    {
        Alpha,
        Beta
    }

    private class TestModule
    {
        public static void FluentModuleInitialize(FluentEnvironment env)
        {
            env.Context.Set(new Marker("initialized"));
        }

        [FluentMethod("current")]
        public static ParseResult Current()
        {
            string value = FluentEnvironment.Current.Context.Get<Marker>().Value;
            return new ParseResult(value);
        }

        [FluentMethod("enum")]
        public static ParseResult Enum(TestMode mode)
            => new ParseResult(mode.ToString());

        [FluentMethod("join")]
        public static ParseResult Join(params string[] values)
            => new ParseResult(string.Join("|", values));
    }

    private static FluentEnvironment CreateEnvironment()
    {
        var env = new FluentEnvironment();
        env.AddModule<TestModule>();
        env.ServeTypes = new[] { typeof(ParseResult) };
        return env;
    }

    [Fact]
    public void AddModule_RunsModuleInitializer()
    {
        var env = CreateEnvironment();

        Assert.Equal("initialized", env.Context.Get<Marker>().Value);
    }

    [Fact]
    public void ParseOne_ExposesCurrentEnvironmentDuringFluentMethodInvocation()
    {
        var env = CreateEnvironment();
        int cursor = 0;

        var parsed = env.ParseOne(new[] { "current" }, ref cursor);

        var result = Assert.IsType<ParseResult>(parsed.Result);
        Assert.Equal("initialized", result.Value);
        Assert.Equal(1, cursor);
    }

    [Fact]
    public void ParseOne_ParsesEnumArguments()
    {
        var env = CreateEnvironment();
        int cursor = 0;

        var parsed = env.ParseOne(new[] { "enum", "Beta" }, ref cursor);

        var result = Assert.IsType<ParseResult>(parsed.Result);
        Assert.Equal("Beta", result.Value);
        Assert.Equal(2, cursor);
    }

    [Fact]
    public void ParseOne_ParsesParamsArrayUntilEndMarker()
    {
        var env = CreateEnvironment();
        int cursor = 0;

        var parsed = env.ParseOne(
            new[] { "join", "one", "two", "three", ".END." },
            ref cursor);

        var result = Assert.IsType<ParseResult>(parsed.Result);
        Assert.Equal("one|two|three", result.Value);
        Assert.Equal(5, cursor);
    }
}
