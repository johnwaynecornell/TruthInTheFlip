namespace JWCFarm.Metrics;

public class MetricBinder
{
    protected static bool ParseExpression(FarmProcess?  process, MetricCatalogs catalogs, MetricPath path, Type currentType, Type inputType, string field)
    {
        bool rc = true;
        
        Type _currentType = currentType;
        
        MetricCatalog catalog;
        
        int i = field.IndexOf('#');
                
        if (i != -1)
        {
            if (!catalogs.TryGet(_currentType, out catalog))
            {
                Console.Error.WriteLine($"Metric catalog not found for type {_currentType}");
                return false;
            }

            string funcName = field.Substring(0, i);
            string _field = field.Substring(i + 1);
                    
            if (!catalog.Metrics.TryGetValue(funcName, out var func))
            {
                Console.Error.WriteLine($"Metric function not found for type {_currentType} and field {field}");
                return false;
            }

            List<MetricPath> arguments = new List<MetricPath>();

            for (int pi = 0; pi < func.Parameters.Count; pi++)
            {
                var p = func.Parameters[pi];
                
                if (p.Type == MetricParameterType.Aggregate) _currentType = inputType;
                else _currentType = currentType;

                MetricPath argumentPath = new();

                if (p.Type == MetricParameterType.Aggregate)
                {
                    var inputProcess = process?.InputProcess;

                    if (inputProcess != null)
                    {
                        rc = ParseExpression(
                            inputProcess,
                            catalogs,
                            argumentPath,
                            inputProcess.StatType,
                            inputProcess.InputType,
                            _field);

                        inputProcess.Projection.Fields.Add(argumentPath);
                    }
                    else
                    {
                        rc = ParseExpression(
                            null,
                            catalogs,
                            argumentPath,
                            _currentType,
                            inputType,
                            _field);
                    }
                }
                else
                {
                    rc = ParseExpression(
                        process,
                        catalogs,
                        argumentPath,
                        currentType,
                        inputType,
                        _field);
                }

                if (rc == false)
                {
                    Console.Error.WriteLine(
                        $"Metric expression outer failed for type {_currentType} and field {_field}");
                    return false;
                }
                
                arguments.Add(argumentPath);
            }

            path.Add(func.CreateInstance(arguments));
              
            return true;
        }
        
        foreach (string _part in field.Split('.'))
        {
            string part = _part;
    
            if (!catalogs.TryGet(_currentType, out catalog))
            {
                Console.Error.WriteLine($"Metric catalog not found for type {_currentType}");
                return false;
            }

            if (!catalog.Metrics.TryGetValue(part, out var metric))
            {
                Console.Error.WriteLine($"Metric not found for type {_currentType} and field {field}");
                rc = false;
                break;
            }
                
            path.Add(metric.CreateInstance(null));
            _currentType = metric.ValueType;
        }

        return rc;

    }
    
    public static bool Bind(FarmProcess process, MetricCatalogs catalogs, Type type, Type inputType, out MetricProjection target, params string[] fields)
    {
        MetricProjection projection = new MetricProjection();
        
        bool rc = true;
        
        List<MetricPath> childBind = new List<MetricPath>();
        
        foreach (string field in fields)
        {
            MetricPath path = new MetricPath();
            Type currentType = type;
            rc = ParseExpression(process, catalogs, path, currentType, inputType, field);
            if (rc) projection.Fields.Add(path);
        }
        
        
        if (rc)
            target = projection;
        else
            target = null;
        
        return rc;
    }
}