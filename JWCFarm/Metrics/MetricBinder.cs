namespace JWCFarm.Metrics;

public class MetricBinder
{
    protected static bool ParseExpression(FarmProcess?  process, MetricCatalogs catalogs, MetricPath path, Type currentType, Type inputType, string field, ref int offset)
    {
        bool rc = true;
        
        Type _currentType = currentType;
        
        MetricCatalog catalog;
        
        int this_offset = offset;

        int i = this_offset;
        while (i<field.Length && field[i] != '#') 
        {
            if (field[i] == ',') i = field.Length; else i++;
        }
                
        if (i < field.Length)
        {
            if (!catalogs.TryGet(_currentType, out catalog))
            {
                Console.Error.WriteLine($"Metric catalog not found for type {_currentType}");
                return false;
            }

            string funcName = field[this_offset..i];
            this_offset = i + 1;
            //string _field = field.Substring(i + 1);
                    
            if (!catalog.Metrics.TryGetValue(funcName, out var func))
            {
                Console.Error.WriteLine($"Metric function {funcName} not found for type {_currentType} and field {field}");
                return false;
            }

            List<MetricPath> arguments = new List<MetricPath>();

            for (int pi = 0; pi < func.Parameters.Count; pi++)
            {
                var p = func.Parameters[pi];

                if (pi != 0)
                {
                    if (field[this_offset] != ',')
                    {
                        Console.Error.WriteLine($"{func.ToString()} parameter {pi} {p.Parameter.Name} missing preceeding comma for type {_currentType} and field {field} ({field.Substring(this_offset)})");
                        return false;
                    }
                    this_offset++;
                }
                
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
                            field, ref this_offset);

                        if (rc) inputProcess.Projection.Fields.Add(argumentPath);
                    }
                    else
                    {
                        rc = ParseExpression(
                            null,
                            catalogs,
                            argumentPath,
                            _currentType,
                            inputType,
                            field, ref this_offset);
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
                        field, ref this_offset);
                }

                if (rc == false)
                {
                    Console.Error.WriteLine(
                        $"Metric expression outer failed for type {_currentType} and field {field} ({field.Substring(this_offset)})");
                    return false;
                }
                
                arguments.Add(argumentPath);
            }

            path.Add(func.CreateInstance(arguments));
            offset = this_offset;
            return true;
        }
        
        int end = field.IndexOf(',', this_offset);

        if (end < 0)
            end = field.Length;

        string expression = field[this_offset..end];
        if (double.TryParse(expression, out double value))
        {
            path.Add(MetricDescriptor.CreateInstance(value));
            
            offset = end;
            return true;
        }
        
        foreach (string _part in expression.Split('.'))
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

        if (rc) offset = end;
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
            int this_offset = 0;
            rc = ParseExpression(process, catalogs, path, currentType, inputType, field, ref this_offset);
            if (this_offset != field.Length) 
            {
                Console.Error.WriteLine($"Invalid field expression: {field} text remains after offset {this_offset}, {field.Substring(this_offset)}");
                rc = false;
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