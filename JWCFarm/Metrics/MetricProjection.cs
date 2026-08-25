using System.Runtime.CompilerServices;

namespace JWCFarm.Metrics;

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

    // ── aggregate state (ConditionalWeakTable keyed by product/segment object) ─

    private readonly ConditionalWeakTable<object, ProductMetricState>
        _statValues = new();

    private sealed class ProductMetricState
    {
        public Dictionary<(string canonicalPath, int ParameterIndex), List<double>>
            Values { get; } = new();
    }

    public void AddStatValue(
        object stats,
        MetricPath path,
        int parameterIndex,
        double value)
    {
        ProductMetricState state = _statValues.GetOrCreateValue(stats);
        var key = (path.ToString(), parameterIndex);

        if (!state.Values.TryGetValue(key, out var values))
        {
            values = new List<double>();
            state.Values[key] = values;
        }

        values.Add(value);
    }

    public List<double> GetStatValues(
        object stats,
        MetricPath path,
        int parameterIndex)
    {
        if (!_statValues.TryGetValue(stats, out var state) ||
            !state.Values.TryGetValue(
                (path.ToString(), parameterIndex),
                out var values))
        {
            throw new KeyNotFoundException(
                $"Aggregate metric state was not found for parameter {parameterIndex} " +
                $"of path '{path}'.");
        }

        return values;
    }

    // ── per-item inspection (aggregate-state accumulation) ─────────────────────

    public void ProcessPath(FarmProcess process, MetricPath path, object segment, object state)
    {
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i].ArgumentPaths == null) continue;

            for (int arg_i = 0; arg_i < path[i].ArgumentPaths.Count; arg_i++)
            {
                var arg  = path[i].ArgumentPaths[arg_i];
                var desc = path[i].InstanceDescriptor.Parameters![arg_i];

                int ii = 0;

                if (desc.Type == MetricParameterType.Aggregate)
                {
                    object o;
                    if (process?.InputProcess != null)
                    {
                        o = arg.Get(process.InputProcess.Projection, state, state, ref ii);
                    }
                    else
                    {
                        ProcessPath(process, arg, state, state);
                        o = arg.Get(this, state, state, ref ii);
                    }

                    double value = Convert.ToDouble(o);
                    AddStatValue(segment, path, arg_i, value);
                }
                else
                {
                    ProcessPath(process, arg, segment, state);
                }
            }
        }
    }

    // Called once per child item (e.g. each Tracker record within a segment).
    // Processes both user-requested Fields and hidden Dependencies so that
    // aggregate state is accumulated for all paths that need it.
    public void Inspect(FarmProcess process, object segment, object state)
    {
        foreach (MetricPath path in Fields)
            ProcessPath(process, path, segment, state);

        foreach (MetricPath path in _dependencies.Values)
            ProcessPath(process, path, segment, state);
    }
}
