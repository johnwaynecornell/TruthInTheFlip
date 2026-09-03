using System.Collections;

namespace JWCFarm.Metrics;

/// <summary>
/// Reusable bound query plan resulting from binding requested metric expressions and their hidden dependencies.
/// Contains no execution-lifetime state. Execution-lifetime state belongs to <see cref="MetricEvaluationSession"/>.
/// </summary>
public class MetricProjection
{
    // User-requested output fields. These appear as CSV columns.
    public List<MetricPath> Fields { get; } = new();

    // Hidden dependencies bound from MetricDescriptor.SourceExpressions.
    // Keyed by canonical expression string. These participate in aggregate-state
    // preparation but do NOT appear as output columns.
    private readonly Dictionary<string, MetricPath> _dependencies = new();

    public IReadOnlyDictionary<string, MetricPath> Dependencies => _dependencies;

    public void AddDependency(string canonicalExpression, MetricPath path)
    {
        _dependencies.TryAdd(canonicalExpression, path);
    }

    public bool TryGetDependency(string canonicalExpression, out MetricPath? path)
        => _dependencies.TryGetValue(canonicalExpression, out path);

    public bool ContainsDependency(string canonicalExpression)
        => _dependencies.ContainsKey(canonicalExpression);

    /// <summary>
    /// Creates a new execution session for evaluating this projection.
    /// </summary>
    public MetricEvaluationSession CreateSession() => new(this);

    // ── compatibility delegates ───────────────────────────────────────────────

    public void Inspect(FarmProcess? process, object segment, object state)
    {
        var session = process?.Session ?? CreateSession();
        session.Inspect(process, segment, state);
    }

    public IList GetStatValues(
        object stats,
        MetricPath path,
        int parameterIndex)
    {
        throw new NotSupportedException(
            "Aggregate metric state belongs to MetricEvaluationSession. " +
            "Access GetStatValues via MetricEvaluationSession or MetricEvaluationContext.Session.");
    }
}
