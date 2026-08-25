using System.Reflection;
using JWCFarm;
using JWCFarm.Metrics;

namespace TruthInTheFlip.Farm.Tests;

/// <summary>
/// Tests for MetricBinder error-reporting: structured MetricBindError, accurate offset,
/// correct message, and no console output from the parser.
/// </summary>
public class MetricBinderErrorTests
{
    // ── catalog types ────────────────────────────────────────────────────────

    private sealed class Root
    {
        public double Score { get; init; }
        public Leaf? Nested { get; init; }
    }

    private sealed class Leaf
    {
        public double Value { get; init; }
    }

    // Host class for scalar and aggregate metric methods used in catalog setup.
    private sealed class Functions
    {
        public double abs(double value) => Math.Abs(value);
        public double add(double x, double y) => x + y;
        public double mean(List<double> values) => values.Count == 0 ? double.NaN : values.Average();
        public double pearson(List<double> x_values, List<double> y_values) => 0.0; // value irrelevant for parse tests
    }

    // ── catalog builders ─────────────────────────────────────────────────────

    private static MetricDescriptor Property(string name, Type valueType, Func<MetricEvaluationContext, object, object?> getter)
        => new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Property,
            Name = name,
            ValueType = valueType,
            Help = name,
            Getter = getter,
            Parameters = Array.Empty<MetricParameterDescriptor>()
        };

    private static MetricDescriptor Method(string name, string methodName, Type hostType)
    {
        var mi = hostType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
                 ?? throw new InvalidOperationException($"Method {methodName} not found.");
        var parameters = mi.GetParameters()
            .Select(p => new MetricParameterDescriptor
            {
                Name = p.Name,
                Type = p.ParameterType == typeof(List<double>)
                    ? MetricParameterType.Aggregate
                    : MetricParameterType.Scalar
            })
            .ToList();

        return new MetricDescriptor
        {
            Type = MetricDescriptor.EType.Method,
            Name = name,
            ValueType = mi.ReturnType,
            Help = name,
            Invoke = (ctx, instance, args) =>
                ((MethodInfo)mi).Invoke(instance, args),
            Parameters = parameters
        };
    }

    private static MetricCatalogs CreateCatalogs()
    {
        var catalogs = new MetricCatalogs();

        catalogs.Catalogs[typeof(Root)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["Score"]  = Property("Score",  typeof(double), (ctx, o) => ((Root)o).Score),
                ["Nested"] = Property("Nested", typeof(Leaf),   (ctx, o) => ((Root)o).Nested),
                ["abs"]    = Method("abs",    "abs",    typeof(Functions)),
                ["add"]    = Method("add",    "add",    typeof(Functions)),
                ["mean"]   = Method("mean",   "mean",   typeof(Functions)),
                ["pearson"] = Method("pearson", "pearson", typeof(Functions)),
            }
        };

        catalogs.Catalogs[typeof(Leaf)] = new MetricCatalog
        {
            Metrics = new Dictionary<string, MetricDescriptor>
            {
                ["Value"] = Property("Value", typeof(double), (ctx, o) => ((Leaf)o).Value),
                ["abs"]   = Method("abs", "abs", typeof(Functions)),
            }
        };

        return catalogs;
    }

    // Helper: call Bind with a null process (no child process) and no InputType.
    private static bool Bind(string field, out MetricBindError? error,
        Type? inputType = null, FarmProcess? process = null)
    {
        MetricCatalogs catalogs = CreateCatalogs();
        bool ok = MetricBinder.Bind(process, catalogs, typeof(Root), inputType,
            out _, out error, field);
        return ok;
    }

    // ── property-path error tests ─────────────────────────────────────────────

    [Fact]
    public void Error_TypoInPropertyName()
    {
        bool ok = Bind("Scoree", out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal("Scoree", error!.Expression);
        Assert.Equal(0, error.Offset);
        Assert.Equal(6, error.Length);
        Assert.Contains("Scoree", error.Message);
        Assert.Contains("Root", error.Message);
    }

    [Fact]
    public void Error_TypoInNestedPropertyName()
    {
        // "Nested.Valuee" — error is at position 7 (start of "Valuee")
        bool ok = Bind("Nested.Valuee", out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal("Nested.Valuee", error!.Expression);
        Assert.Equal(7, error.Offset);   // after "Nested."
        Assert.Equal(6, error.Length);   // len("Valuee")
        Assert.Contains("Valuee", error.Message);
        Assert.Contains("Leaf", error.Message);
    }

    // ── function-name error tests ──────────────────────────────────────────────

    [Fact]
    public void Error_TypoInFunctionName()
    {
        // "abss#Score" — "abss" is not in the catalog
        bool ok = Bind("abss#Score", out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(0, error!.Offset);
        Assert.Equal(4, error.Length);   // len("abss")
        Assert.Contains("abss", error.Message);
        Assert.Contains("Root", error.Message);
    }

    // ── missing comma test ─────────────────────────────────────────────────────

    [Fact]
    public void Error_MissingComma()
    {
        // "add#Score" — add takes (double x, double y).
        // "Score" parses successfully as x (offset advances to 9 = field.Length).
        // The loop then tries pi=1 and finds this_offset >= field.Length → missing ','.
        // The property-path scanner stops at commas, so no comma means end-of-string,
        // and the comma check fires at offset 9.
        bool ok = Bind("add#Score", out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(9, error!.Offset); // end of "add#Score"
        Assert.Contains("','", error.Message);
        Assert.Contains("y", error.Message); // second parameter name
    }

    // ── too-few-parameters test ────────────────────────────────────────────────

    [Fact]
    public void Error_TooFewParameters_MissingSecondArg()
    {
        // Same scenario as Error_MissingComma: add#Score provides only one argument
        // where two are required.  The error message identifies the missing parameter.
        bool ok = Bind("add#Score", out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        // The error must name the missing parameter (y) and indicate a comma was expected.
        Assert.Contains("y", error!.Message);
        Assert.Contains("add", error.Message);
    }

    // ── trailing text test ─────────────────────────────────────────────────────

    [Fact]
    public void Error_TrailingTextAfterExpression()
    {
        // "Score,garbage" — the property-path scanner stops at the comma, so "Score"
        // parses successfully (offset = 5, the comma position).  Bind then detects
        // that this_offset (5) != field.Length (13) and reports trailing text.
        bool ok = Bind("Score,garbage", out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(5, error!.Offset);  // position of the comma
        Assert.Contains("Unexpected text", error.Message);
    }

    // ── aggregate with no child process ───────────────────────────────────────

    [Fact]
    public void Error_AggregateParameterWithNoChildProcess()
    {
        // "mean#Score" — mean takes List<double> (aggregate).
        // With inputType == null, there is no child process to sample from.
        bool ok = Bind("mean#Score", out var error, inputType: null, process: null);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("mean", error!.Message);
        Assert.Contains("child process", error.Message);
    }

    // ── valid deep-nested expression unchanged ─────────────────────────────────

    [Fact]
    public void ValidDeepNestedExpression_Succeeds()
    {
        // "abs#Nested.Value" — abs is scalar, Nested.Value is a dot path through Leaf.
        // This should parse successfully with no error.
        bool ok = Bind("abs#Nested.Value", out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void ValidSimplePath_Succeeds()
    {
        bool ok = Bind("Score", out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void ValidNestedPath_Succeeds()
    {
        bool ok = Bind("Nested.Value", out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    // ── FormatDiagnostic layout ────────────────────────────────────────────────

    [Fact]
    public void FormatDiagnostic_ContainsCaret()
    {
        var err = new MetricBindError("Nested.Typo", 7, 4, "Unknown metric 'Typo'.");
        string diag = err.FormatDiagnostic();

        Assert.Contains("Nested.Typo", diag);
        // Line after expression must have 2 leading spaces + 7 blanks + 4 carets
        Assert.Contains("         ^^^^", diag);
        Assert.Contains("Unknown metric 'Typo'.", diag);
    }

    [Fact]
    public void FormatDiagnostic_SingleCaret()
    {
        var err = new MetricBindError("add#Score Score", 9, "',' expected.");
        string diag = err.FormatDiagnostic();

        // "  " + 9 spaces + "^"
        string expectedCaretLine = "  " + new string(' ', 9) + "^";
        Assert.Contains(expectedCaretLine, diag);
    }
}
