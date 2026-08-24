namespace JWCFarm.Metrics;

public class MetricBinder
{
    // Recursive parser. On failure:
    //   - sets `error` to a structured description
    //   - sets `offset` to the character position where the error was detected
    //   - returns false without printing anything
    // Recursive call failures are propagated as-is (error already set).
    protected static bool ParseExpression(
        FarmProcess? process,
        MetricCatalogs catalogs,
        MetricPath path,
        Type currentType,
        Type inputType,
        string field,
        ref int offset,
        out MetricBindError? error)
    {
        error = null;
        Type _currentType = currentType;
        MetricCatalog? catalog;
        int this_offset = offset;

        // Scan forward for '#', stopping at ',' (which belongs to the caller's param loop).
        int i = this_offset;
        while (i < field.Length && field[i] != '#')
        {
            if (field[i] == ',') i = field.Length; else i++;
        }

        if (i < field.Length) // found '#' → function-call branch
        {
            if (!catalogs.TryGet(_currentType, out catalog))
            {
                offset = this_offset;
                error = new MetricBindError(field, this_offset,
                    $"No metric catalog for type '{_currentType?.Name ?? "null"}'.");
                return false;
            }

            string funcName = field[this_offset..i];

            if (!catalog!.Metrics.TryGetValue(funcName, out var func))
            {
                offset = this_offset;
                error = new MetricBindError(field, this_offset, funcName.Length,
                    $"Unknown metric function '{funcName}' on {_currentType!.Name}.");
                return false;
            }

            this_offset = i + 1; // skip past '#'

            List<MetricPath> arguments = new();

            for (int pi = 0; pi < func.Parameters.Count; pi++)
            {
                var p = func.Parameters[pi];

                if (pi != 0)
                {
                    if (this_offset >= field.Length || field[this_offset] != ',')
                    {
                        offset = this_offset;
                        error = new MetricBindError(field, this_offset,
                            $"Expected ',' before parameter '{p.Parameter.Name}' " +
                            $"(parameter {pi + 1} of '{funcName}').");
                        return false;
                    }
                    this_offset++;
                }

                if (p.Type == MetricParameterType.Aggregate) _currentType = inputType;
                else _currentType = currentType;

                MetricPath argumentPath = new();
                bool paramOk;

                if (p.Type == MetricParameterType.Aggregate)
                {
                    var inputProcess = process?.InputProcess;

                    if (inputProcess != null)
                    {
                        paramOk = ParseExpression(
                            inputProcess, catalogs, argumentPath,
                            inputProcess.StatType, inputProcess.InputType,
                            field, ref this_offset, out error);

                        if (paramOk) inputProcess.Projection.Fields.Add(argumentPath);
                    }
                    else if (_currentType == null)
                    {
                        // inputType is null — no child process type to sample from.
                        offset = this_offset;
                        error = new MetricBindError(field, this_offset,
                            $"Aggregate parameter '{p.Parameter.Name}' of '{funcName}' " +
                            $"requires a child process, but the current process has none.");
                        return false;
                    }
                    else
                    {
                        paramOk = ParseExpression(
                            null, catalogs, argumentPath,
                            _currentType, inputType,
                            field, ref this_offset, out error);
                    }
                }
                else
                {
                    paramOk = ParseExpression(
                        process, catalogs, argumentPath,
                        currentType, inputType,
                        field, ref this_offset, out error);
                }

                if (!paramOk)
                {
                    offset = this_offset;
                    return false; // error already set by recursive call
                }

                arguments.Add(argumentPath);
            }

            path.Add(func.CreateInstance(arguments));
            offset = this_offset;
            return true;
        }

        // Property path / numeric literal branch
        int end = field.IndexOf(',', this_offset);
        if (end < 0) end = field.Length;

        string expression = field[this_offset..end];

        if (double.TryParse(expression, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double value))
        {
            path.Add(MetricDescriptor.CreateInstance(value));
            offset = end;
            return true;
        }

        // Dot-separated property path
        string[] parts = expression.Split('.');
        int segStart = this_offset;

        foreach (string part in parts)
        {
            if (!catalogs.TryGet(_currentType, out catalog))
            {
                offset = segStart;
                error = new MetricBindError(field, segStart, part.Length,
                    $"No metric catalog for type '{_currentType?.Name ?? "null"}'.");
                return false;
            }

            if (!catalog!.Metrics.TryGetValue(part, out var metric))
            {
                offset = segStart;
                error = new MetricBindError(field, segStart, part.Length,
                    $"Unknown metric '{part}' on {_currentType!.Name}.");
                return false;
            }

            path.Add(metric.CreateInstance(null));
            _currentType = metric.ValueType;
            segStart += part.Length + 1; // +1 for the '.'
        }

        offset = end;
        return true;
    }

    // Primary overload: returns structured error information.
    public static bool Bind(
        FarmProcess? process,
        MetricCatalogs catalogs,
        Type type,
        Type? inputType,
        out MetricProjection? target,
        out MetricBindError? bindError,
        params string[] fields)
    {
        MetricProjection projection = new();
        bindError = null;

        foreach (string field in fields)
        {
            MetricPath path = new();
            int this_offset = 0;

            bool ok = ParseExpression(
                process, catalogs, path,
                type, inputType!,
                field, ref this_offset, out MetricBindError? fieldError);

            if (ok && this_offset != field.Length)
            {
                // Expression parsed successfully but left unconsumed text.
                ok = false;
                fieldError = new MetricBindError(field, this_offset,
                    field.Length - this_offset,
                    $"Unexpected text after expression (starting at position {this_offset}).");
            }

            if (!ok)
            {
                bindError = fieldError;
                target = null;
                return false;
            }

            projection.Fields.Add(path);
        }

        target = projection;
        return true;
    }

    // Compatibility overload for callers that do not need the structured error.
    public static bool Bind(
        FarmProcess? process,
        MetricCatalogs catalogs,
        Type type,
        Type? inputType,
        out MetricProjection? target,
        params string[] fields)
    {
        return Bind(process, catalogs, type, inputType, out target, out _, fields);
    }
}
