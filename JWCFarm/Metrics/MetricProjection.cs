namespace JWCFarm.Metrics;

public class MetricProjection
{
    public List<MetricPath> Fields { get; } = new();
    
    public Dictionary<(object, MetricPath, int), List<double>> StatValues { get; } = new();

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
                        if (!StatValues.ContainsKey((segment, path, arg_i))) StatValues[(segment, path, arg_i)] = new();
                        StatValues[(segment, path, arg_i)].Add(value);
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