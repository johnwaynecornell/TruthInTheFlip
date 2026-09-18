using System.Globalization;
using System.Text.Json;
using FluentCommandLine;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public sealed class JsonFormattingTests
{
    private sealed class SampleItem
    {
        public double Number { get; init; }
        public double NonFinite { get; init; }
        public double NaN { get; init; }
        public float NegativeInfinity { get; init; }
        public long Count { get; init; }
        public bool Flag { get; init; }
        public string? Text { get; init; }
        public DateTime UtcDate { get; init; }
        public DateTimeOffset OffsetDate { get; init; }
        public TimeSpan Duration { get; init; }
    }

    private sealed class TestProcess(IEnumerable<object> items) : FarmProcess
    {
        public override Type StatType => typeof(SampleItem);
        public override Type? InputType => null;

        protected override IEnumerable<object> EnumerateItems(FarmContext context)
            => items;
    }

    [Fact]
    public void JsonCommand_StreamsTypedNdjsonUsingInvariantFormats()
    {
        var item = new SampleItem
        {
            Number = 1234.5,
            NonFinite = double.PositiveInfinity,
            NaN = double.NaN,
            NegativeInfinity = float.NegativeInfinity,
            Count = 9876543210,
            Flag = true,
            Text = "alpha, \"beta\"\\gamma\nλ",
            UtcDate = new DateTime(2026, 8, 11, 19, 30, 45, 123, DateTimeKind.Utc),
            OffsetDate = new DateTimeOffset(2026, 8, 11, 21, 30, 45, 123, TimeSpan.FromHours(2)),
            Duration = new TimeSpan(1, 2, 3, 4, 5)
        };

        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            string output = RunJson(
                [item],
                "Number", "NonFinite", "NaN", "NegativeInfinity", "Count", "Flag", "Text",
                "UtcDate", "OffsetDate", "Duration");

            string line = Assert.Single(NonEmptyLines(output));
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;

            Assert.Equal(JsonValueKind.Number, root.GetProperty("Number").ValueKind);
            Assert.Equal(1234.5, root.GetProperty("Number").GetDouble());
            Assert.Equal("Infinity", root.GetProperty("NonFinite").GetString());
            Assert.Equal("NaN", root.GetProperty("NaN").GetString());
            Assert.Equal("-Infinity", root.GetProperty("NegativeInfinity").GetString());
            Assert.Equal(9876543210, root.GetProperty("Count").GetInt64());
            Assert.True(root.GetProperty("Flag").GetBoolean());
            Assert.Equal(item.Text, root.GetProperty("Text").GetString());
            Assert.Equal("2026-08-11T19:30:45.123Z", root.GetProperty("UtcDate").GetString());
            Assert.Equal("2026-08-11T19:30:45.123Z", root.GetProperty("OffsetDate").GetString());
            Assert.Equal("1.02:03:04.0050000", root.GetProperty("Duration").GetString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void JsonCommand_WritesNullAndOneCompleteObjectPerItem()
    {
        string output = RunJson(
            [
                new SampleItem { Number = 1.25, Text = null },
                new SampleItem { Number = 2.5, Text = "second" }
            ],
            "Number", "Text");

        string[] lines = NonEmptyLines(output);
        Assert.Equal(2, lines.Length);

        using JsonDocument first = JsonDocument.Parse(lines[0]);
        using JsonDocument second = JsonDocument.Parse(lines[1]);
        Assert.Equal(JsonValueKind.Null, first.RootElement.GetProperty("Text").ValueKind);
        Assert.Equal("second", second.RootElement.GetProperty("Text").GetString());
    }

    [Fact]
    public void JsonCommand_EmptyProcess_ProducesNoOutput()
    {
        Assert.Empty(RunJson([], "Number"));
    }

    [Fact]
    public void WriteJsonRow_EscapesPropertyNamesWithoutRenamingThem()
    {
        var projection = new MetricProjection();
        projection.Fields.Add(Path(
            "quoted\"key\\path\nline",
            typeof(string),
            (_, _) => "value"));

        using var writer = new StringWriter();
        TruthInTheFlip_Fluent.WriteJsonRow(projection, writer, new SampleItem());

        using JsonDocument document = JsonDocument.Parse(writer.ToString());
        Assert.Equal(
            "value",
            document.RootElement.GetProperty("quoted\"key\\path\nline").GetString());
    }

    private static string RunJson(IEnumerable<SampleItem> items, params string[] fields)
    {
        var env = new FluentEnvironment();
        var catalogs = new MetricCatalogs { Reflect = ReflectSample };
        env.Context.Set(catalogs);

        using var scope = FluentEnvironmentScope.Enter(env);
        var process = new TestProcess(items.Cast<object>());
        FarmCommand command = TruthInTheFlip_Fluent.json(process, fields);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        command.Execute(new FarmContext { Output = writer });
        return writer.ToString();
    }

    private static MetricCatalog? ReflectSample(Type type)
    {
        if (type != typeof(SampleItem)) return null;

        return new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["Number"] = Descriptor("Number", typeof(double), (_, o) => ((SampleItem)o).Number),
                ["NonFinite"] = Descriptor("NonFinite", typeof(double), (_, o) => ((SampleItem)o).NonFinite),
                ["NaN"] = Descriptor("NaN", typeof(double), (_, o) => ((SampleItem)o).NaN),
                ["NegativeInfinity"] = Descriptor("NegativeInfinity", typeof(float), (_, o) => ((SampleItem)o).NegativeInfinity),
                ["Count"] = Descriptor("Count", typeof(long), (_, o) => ((SampleItem)o).Count),
                ["Flag"] = Descriptor("Flag", typeof(bool), (_, o) => ((SampleItem)o).Flag),
                ["Text"] = Descriptor("Text", typeof(string), (_, o) => ((SampleItem)o).Text),
                ["UtcDate"] = Descriptor("UtcDate", typeof(DateTime), (_, o) => ((SampleItem)o).UtcDate),
                ["OffsetDate"] = Descriptor("OffsetDate", typeof(DateTimeOffset), (_, o) => ((SampleItem)o).OffsetDate),
                ["Duration"] = Descriptor("Duration", typeof(TimeSpan), (_, o) => ((SampleItem)o).Duration)
            }
        };
    }

    private static MetricDescriptor Descriptor(
        string name,
        Type valueType,
        Func<MetricEvaluationContext, object, object?> getter)
        => new()
        {
            Type = MetricDescriptor.EType.Property,
            Name = name,
            ValueType = valueType,
            Help = name,
            Getter = getter
        };

    private static MetricPath Path(
        string name,
        Type valueType,
        Func<MetricEvaluationContext, object, object?> getter)
        => new() { Descriptor(name, valueType, getter).CreateInstance(null) };

    private static string[] NonEmptyLines(string text)
        => text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
}
