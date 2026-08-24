using System.Runtime.CompilerServices;

namespace JWCFarm.Metrics;

public class MetricProjection
{
    public List<MetricPath> Fields { get; } = new();
    
    private readonly ConditionalWeakTable<object, ProductMetricState>
        _statValues = new();

    private sealed class ProductMetricState
    {
        public Dictionary<(MetricPath Path, int ParameterIndex), List<double>>
            Values { get; } = new();
    }

    public void AddStatValue(
        object stats,
        MetricPath path,
        int parameterIndex,
        double value)
    {
        ProductMetricState state =
            _statValues.GetOrCreateValue(stats);

        var key = (path, parameterIndex);

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
                (path, parameterIndex),
                out var values))
        {
            throw new KeyNotFoundException(
                $"Aggregate metric state was not found for parameter {parameterIndex}.");
        }

        return values;
    }

    public void ProcessPath(FarmProcess process, MetricPath path, object segment, object state)
    {
        var input = process?.InputProcess;
        object o;
        
        int i;
        for (i = 0; i < path.Count; i++)
        {
            if (path[i].ArgumentPaths != null)
            {
                for (int arg_i =0; arg_i < path[i].ArgumentPaths.Count; arg_i++)
                {
                    var arg = path[i].ArgumentPaths[arg_i];
                    var desc = path[i].InstanceDescriptor.Parameters[arg_i];
                    
                    int ii = 0;

                    
                    if (desc.Type == MetricParameterType.Aggregate)
                    {
                        if (process?.InputProcess != null)
                        {
                            //ProcessPath(process.InputProcess, arg, state, state);
                            
                            // argument belongs to upstream product/process
                            o = arg.Get(process.InputProcess.Projection, state, state, ref ii);
                        }
                        else
                        {
                            ProcessPath(process, arg, state, state);

                            // argument remains in the current expression context
                            o = arg.Get(this, state, state, ref ii);
                        }


                        double value = Convert.ToDouble(o);
                        
                        AddStatValue(
                            segment,
                            path,
                            arg_i,
                            value);
                        
                    } else ProcessPath(process, arg, segment, state);
                }
            }
        }
    }
    
    
    public void Inspect(FarmProcess process, object segment, object state)
    {
        foreach (MetricPath path in Fields)
        {
            ProcessPath(process,  path, segment, state);
        }
    }

}