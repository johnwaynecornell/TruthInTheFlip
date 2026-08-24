using System.Globalization;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;

namespace TruthInTheFlip.Farm.Tests;

public class CsvFormattingTests
{
    private sealed class Row
    {
        public double Number { get; init; }
        public string Text { get; init; } = "";
        public NestedRow Nested { get; init; } = new();
    }

    private sealed class NestedRow
    {
        public string Value { get; init; } = "";
    }

    private static MetricProjection CreateProjection()
    {
        var projection = new MetricProjection();
        projection.Fields.Add(new MetricPath
        {
            new MetricDescriptor
            {
                Type = MetricDescriptor.EType.Property,
                Name = "Number",
                ValueType = typeof(double),
                Help = "Number",
                Getter = value => ((Row)value).Number
            }.CreateInstance(null)
        });
        projection.Fields.Add(new MetricPath
        {
            new MetricDescriptor
            {
                Type = MetricDescriptor.EType.Property,
                Name = "Nested",
                ValueType = typeof(NestedRow),
                Help = "Nested",
                Getter = value => ((Row)value).Nested
            }.CreateInstance(null),
            new MetricDescriptor
            {
                Type = MetricDescriptor.EType.Property,
                Name = "Value",
                ValueType = typeof(string),
                Help = "Value",
                Getter = value => ((NestedRow)value).Value
            }.CreateInstance(null)
        });
        return projection;
    }

    [Fact]
    public void WriteHeader_WritesDottedMetricPathsInOrder()
    {
        using var writer = new StringWriter();

        TruthInTheFlip_Fluent.WriteHeader(CreateProjection(), writer);

        Assert.Equal($"Number,Nested.Value{Environment.NewLine}", writer.ToString());
    }

    [Fact]
    public void WriteRow_WritesProjectionAndEscapesCsvText()
    {
        using var writer = new StringWriter();
        var row = new Row
        {
            Number = 1.25,
            Nested = new NestedRow { Value = "alpha,\"beta\"" }
        };

        TruthInTheFlip_Fluent.WriteRow(CreateProjection(), writer, row);

        Assert.Equal(
            $"1.25,\"alpha,\"\"beta\"\"\"{Environment.NewLine}",
            writer.ToString());
    }

    [Fact]
    public void CsvOut_UsesInvariantCultureForFloatingPointValues()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            using var writer = new StringWriter();

            TruthInTheFlip_Fluent.CSVOut(writer, 1234.5);

            Assert.Equal("1234.5", writer.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void CsvOut_UsesStableDateTimeAndTimeSpanFormats()
    {
        using var dateWriter = new StringWriter();
        using var spanWriter = new StringWriter();

        TruthInTheFlip_Fluent.CSVOut(
            dateWriter,
            new DateTime(2026, 8, 11, 19, 30, 45, 123, DateTimeKind.Utc));
        TruthInTheFlip_Fluent.CSVOut(
            spanWriter,
            new TimeSpan(1, 2, 3, 4, 5));

        Assert.Equal("2026-08-11T19:30:45.123Z", dateWriter.ToString());
        Assert.Equal("1.02:03:04.0050000", spanWriter.ToString());
    }
}
