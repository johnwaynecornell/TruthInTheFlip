namespace JWCFarm.Metrics;

public class MetricProjection
{
    public List<MetricPath> Fields { get; } = new();
    
    public Dictionary<(object, MetricPath), List<double>> StatValues { get; } = new();

    public void ProcessPath(FarmProcess process, MetricPath path, object segment, object state)
    {

        var input = process?.InputProcess;
        object o;
        
        int i;
        for (i = 0; i < path.Count; i++)
        {
            var agg = (path[i].InstanceDescriptor.Type == MetricDescriptor.EType.Aggregate);
            
            if (path[i].ArgumentPath != null)
            {
                if (!agg) ProcessPath(process, path[i].ArgumentPath, segment, state);
            }
            
            if (agg) break;
        }

        if (i >= path.Count) return;

        var argPath = path[i].ArgumentPath;
        
        int ii = 0;
        
        if (process?.InputProcess != null)
        {
            // argument belongs to upstream product/process
            o = argPath.Get(process.InputProcess.Projection, state, null, ref ii);
        }
        else
        {
            // argument remains in the current expression context
            o = argPath.Get(this,state, state, ref ii);
        }
        
            
        double value = Convert.ToDouble(o);
        if (!StatValues.ContainsKey((segment, path))) StatValues[(segment, path)] = new();
        StatValues[(segment, path)].Add(value);
        
    }
    
    
    public void Inspect(FarmProcess process, object segment, object state)
    {
        foreach (MetricPath path in Fields)
        {
            ProcessPath(process,  path, segment, state);
        }
    }

}