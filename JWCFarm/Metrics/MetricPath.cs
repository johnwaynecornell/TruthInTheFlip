using System.Text;

namespace JWCFarm.Metrics;

public class MetricPath : List<MetricDescriptor.Instance>
{
    public object Get(MetricProjection projection, object stats, object root, ref int index)
    {
        bool hasO = false;
        object o = null;

        while (index < Count)
        {
            var p = this[index++];

            if (!hasO)
            {
                //o = p.Type == MetricDescriptor.EType.Aggregate ? root : stats;
                o = stats;
                hasO = true;
            }

            if (o == null)
                throw new NullReferenceException(
                    $"Field {string.Join(".", from p2 in this select p2.InstanceDescriptor.Name)} contains null");

			if (p.IsValue)
			    o = p.Value; 
            else if (p.InstanceDescriptor.Type == MetricDescriptor.EType.Property)
                o = p.InstanceDescriptor.Getter(o);
            else if (p.InstanceDescriptor.Type == MetricDescriptor.EType.Method)
            {
                object[] parameters = new object[p.ArgumentPaths.Count];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var arg = p.ArgumentPaths[i];
                    var desc = p.InstanceDescriptor.Parameters[i];

                    if (desc.Type == MetricParameterType.Scalar)
                    {
                        //var o2 = Get(projection, root, root, ref index);
                        int ii = 0;
                        parameters[i] = arg.Get(projection, root, root, ref ii);

                    }
                    else if (desc.Type == MetricParameterType.Aggregate)
                    {
                        parameters[i] =
                            projection.GetStatValues(
                                stats,
                                this,
                                i);
                        
                        index = Count;
                    }
                }

                o = p.InstanceDescriptor.Method.Invoke(o, parameters );
            }
        }
        
        if (o == null)
        {
            
        }

        return o;
    }
    
    public object Get(MetricProjection projection, object stats)
    {
        object? o = stats;

        int i = 0;
        while (i<Count)
        {
            o = Get(projection, o, stats, ref i);
        }
        
        return o;
    }

    public override string ToString()
    {
        StringBuilder writer = new StringBuilder();
        
        string mark = "";
        foreach (var p in this)
        {
            writer.Append(mark);
            if (p.IsValue) writer.Append(p.Value);
            else
            {
                writer.Append(p.InstanceDescriptor.Name);
                
                if (p.ArgumentPaths != null) writer.Append($"#{string.Join("," , p.ArgumentPaths)}");
            }
            mark = ".";
        }
        return writer.ToString();
    }
}