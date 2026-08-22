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
            if (p.InstanceDescriptor.Type == MetricDescriptor.EType.Property) 
                o = p.InstanceDescriptor.Getter(o);
            else if (p.InstanceDescriptor.Type == MetricDescriptor.EType.Scalar)
            {
                //var o2 = Get(projection, root, root, ref index);
                int ii = 0;
                var o2 = p.ArgumentPath.Get(projection, root, root, ref ii);
                o = p.InstanceDescriptor.Method.Invoke(o, new object?[] { o2 });
            } else if (p.InstanceDescriptor.Type == MetricDescriptor.EType.Aggregate)
            {
                var o2 = projection.StatValues[(stats, this)];
                o = p.InstanceDescriptor.Method.Invoke(o, new object?[] { o2 });
                index = Count;
            }
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
            writer.Append(p.InstanceDescriptor.Name);
            if (p.ArgumentPath != null) writer.Append($"#{p.ArgumentPath}");
            
            mark = ".";
        }
        return writer.ToString();
    }
}