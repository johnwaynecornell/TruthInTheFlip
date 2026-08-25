namespace JWCFarm.Metrics;

public sealed class MetricEvaluationContext
{
    public required MetricProjection Projection { get; init; }

    public required object Stats { get; init; }

    public required object Root { get; init; }

    public FarmProcess? Process { get; init; }

    public MetricPath? Path { get; init; }

    // Returns a copy of this context with Stats replaced by newStats.
    // Used inside MetricPath.Get to give each Getter/Invoke the correct current object.
    public MetricEvaluationContext WithStats(object newStats) => new()
    {
        Projection = Projection,
        Stats      = newStats,
        Root       = Root,
        Process    = Process,
        Path       = Path,
    };

    // Retrieves the value of a pre-bound SourceExpression dependency.
    // Throws KeyNotFoundException if the expression was not declared in SourceExpressions.
    public object? Get(string expression)
    {
        if (!Projection.TryGetDependency(expression, out MetricPath? depPath) || depPath == null)
            throw new KeyNotFoundException(
                $"Metric source expression '{expression}' was not declared as a SourceExpression " +
                "for this descriptor. Add it to SourceExpressions and rebind.");

        return depPath.Get(Projection, Stats);
    }

    public T Get<T>(string expression)
    {
        object? result = Get(expression);
        if (result is T t) return t;
        if (result is null) return default!;
        return (T)Convert.ChangeType(result, typeof(T),
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
