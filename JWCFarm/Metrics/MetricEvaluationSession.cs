using System.Runtime.CompilerServices;

namespace JWCFarm.Metrics;

/// <summary>
/// Represents one execution lifetime of a <see cref="MetricProjection"/>.
/// Owns transient evaluation-lifetime state (such as aggregate metric samples and custom session state)
/// while preserving <see cref="MetricProjection"/> as a reusable bound query plan.
/// </summary>
public sealed class MetricEvaluationSession
{
    public MetricProjection Projection { get; }

    // ── session state store (for serial/stateful custom metrics) ──────────────

    private readonly Dictionary<object, object> _states = new();

    public MetricEvaluationSession(MetricProjection projection)
    {
        Projection = projection ?? throw new ArgumentNullException(nameof(projection));
    }

    /// <summary>
    /// Gets or creates a strongly-typed state object associated with the specified key for the duration of this session.
    /// </summary>
    public T GetState<T>(object key, Func<T> factory) where T : class
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        if (!_states.TryGetValue(key, out var existing))
        {
            T created = factory();
            _states[key] = created;
            return created;
        }

        if (existing is T typed)
            return typed;

        throw new InvalidCastException(
            $"Session state for key '{key}' is of type '{existing.GetType().FullName}', not '{typeof(T).FullName}'.");
    }

    // ── aggregate state (ConditionalWeakTable keyed by product/segment object) ─

    private readonly ConditionalWeakTable<object, ProductMetricState> _statValues = new();

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

    public void ProcessPath(FarmProcess? process, MetricPath path, object segment, object state)
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
                        var inputSession = process.InputProcess.Session
                                           ?? (process.InputProcess.Projection != null
                                               ? new MetricEvaluationSession(process.InputProcess.Projection)
                                               : null);

                        o = inputSession != null
                            ? arg.Get(inputSession, state, state, ref ii)
                            : arg.Get(this, state, state, ref ii);
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

    /// <summary>
    /// Accumulates aggregate metric state for both user-requested fields and hidden dependencies.
    /// </summary>
    public void Inspect(FarmProcess? process, object segment, object state)
    {
        foreach (MetricPath path in Projection.Fields)
            ProcessPath(process, path, segment, state);

        foreach (MetricPath path in Projection.Dependencies.Values)
            ProcessPath(process, path, segment, state);
    }
}
