namespace JWCFarm.Metrics;

public class MetricBinder
{
    public static bool Bind<T>(MetricCatalogs catalogs, out MetricProjection target, params string[] fields)
    {
        return Bind(catalogs, typeof(T), out target, fields);
    }
    
    public static bool Bind(MetricCatalogs catalogs, Type type, out MetricProjection target, params string[] fields)
    {
        MetricProjection projection = new MetricProjection();
        
        bool rc = true;
        
        foreach (string field in fields)
        {
            MetricPath path = new MetricPath();
            
            Type currentType = type;
            
            foreach (string part in field.Split('.'))
            {
                if (!catalogs.TryGet(currentType, out var catalog))
                {
                    Console.Error.WriteLine($"Metric catalog not found for type {currentType}");
                    rc = false;
                    break;
                }

                if (!catalog.Metrics.TryGetValue(part, out var metric))
                {
                    Console.Error.WriteLine($"Metric not found for type {currentType} and field {field}");
                    rc = false;
                    break;
                }
                
                path.Add(metric);
                currentType = metric.ValueType;
            }
            
            if (rc) projection.Fields.Add(path);
        }
        
        if (rc)
            target = projection;
        else
            target = null;
        
        return rc;
    }
}