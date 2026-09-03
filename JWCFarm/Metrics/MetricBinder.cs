using System.Globalization;

namespace JWCFarm.Metrics;

public class MetricBinder
{
    // Shared context threaded through ParseExpression for source-expression binding.
    // Null means no source-expression binding is requested (pure parse-only mode).
    private sealed class BindContext
    {
        public MetricProjection Projection { get; }

        // Names of MetricDescriptors currently having their SourceExpressions bound.
        // Used to detect cycles (e.g. Foo → Bar → Foo).
        // The chain is shared across InputProcess descent levels so cycles that
        // span process boundaries are still detected correctly.
        public List<string> BindingChain { get; }

        public BindContext(MetricProjection projection)
        {
            Projection    = projection;
            BindingChain  = new();
        }

        // Create a child context that targets a different projection but shares the
        // same binding chain. Use this when descending into an InputProcess so that
        // SourceExpression dependencies are stored in the projection the Getter will
        // actually receive at evaluation time.
        public BindContext WithProjection(MetricProjection projection)
            => new(projection, BindingChain);

        private BindContext(MetricProjection projection, List<string> sharedChain)
        {
            Projection   = projection;
            BindingChain = sharedChain;
        }
    }

    // ── recursive parser ──────────────────────────────────────────────────────
    //
    // On failure:
    //   - sets `error` to a structured description
    //   - sets `offset` to the character position where the error was detected
    //   - returns false without printing anything
    // Recursive call failures propagate as-is (error already set).
    //
    // `bindCtx` is threaded through so that SourceExpressions are bound inline
    // for every descriptor encountered, at the correct type context.

    private static bool ParseExpression(
        FarmProcess? process,
        MetricCatalogs catalogs,
        MetricPath path,
        Type currentType,
        Type inputType,
        string field,
        ref int offset,
        out MetricBindError? error,
        BindContext? bindCtx = null)
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

            // Bind any SourceExpressions declared by this function descriptor.
            if (!BindSourceExpressionsForDescriptor(
                    process, catalogs, _currentType, inputType,
                    func, bindCtx, field, i, out error))
            {
                offset = i;
                return false;
            }

            List<MetricPath> arguments = new();

            for (int pi = 0; pi < func.Parameters!.Count; pi++)
            {
                var p = func.Parameters[pi];

                if (pi != 0)
                {
                    if (this_offset >= field.Length || field[this_offset] != ',')
                    {
                        offset = this_offset;
                        error = new MetricBindError(field, this_offset,
                            $"Expected ',' before parameter '{p.Name}' " +
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
                        // Descend into the inner process.  Any SourceExpression
                        // dependencies discovered here must land in
                        // inputProcess.Projection — that is the projection the
                        // Getter's context will carry at evaluation time.
                        var innerCtx = bindCtx?.WithProjection(inputProcess.Projection);

                        paramOk = ParseExpression(
                            inputProcess, catalogs, argumentPath,
                            inputProcess.StatType, inputProcess.InputType,
                            field, ref this_offset, out error, innerCtx);

                        if (paramOk) inputProcess.Projection.Fields.Add(argumentPath);
                    }
                    else if (_currentType == null)
                    {
                        offset = this_offset;
                        error = new MetricBindError(field, this_offset,
                            $"Aggregate parameter '{p.Name}' of '{funcName}' " +
                            $"requires a child process, but the current process has none.");
                        return false;
                    }
                    else
                    {
                        paramOk = ParseExpression(
                            null, catalogs, argumentPath,
                            _currentType, inputType,
                            field, ref this_offset, out error, bindCtx);
                    }
                }
                else
                {
                    int tokenEnd = field.IndexOf(',', this_offset);
                    if (tokenEnd < 0) tokenEnd = field.Length;
                    string token = field[this_offset..tokenEnd];

                    if (p.ReflectedType != null && TryParseReflectedValue(p.ReflectedType, token, out object? parsedValue))
                    {
                        argumentPath.Add(MetricDescriptor.CreateInstance(parsedValue));
                        this_offset = tokenEnd;
                        paramOk = true;
                    }
                    else
                    {
                        paramOk = ParseExpression(
                            process, catalogs, argumentPath,
                            currentType, inputType,
                            field, ref this_offset, out error, bindCtx);
                    }
                }

                if (!paramOk)
                {
                    offset = this_offset;
                    return false;
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

            // Bind any SourceExpressions declared by this property descriptor.
            // Use _currentType (before advancing to ValueType) as the receiver context.
            if (!BindSourceExpressionsForDescriptor(
                    process, catalogs, _currentType, inputType,
                    metric, bindCtx, field, segStart, out error))
            {
                offset = segStart;
                return false;
            }

            path.Add(metric.CreateInstance(null));
            _currentType = metric.ValueType;
            segStart += part.Length + 1; // +1 for the '.'
        }

        offset = end;
        return true;
    }

    // ── SourceExpressions binding ─────────────────────────────────────────────

    // Binds all SourceExpressions declared by `descriptor` into bindCtx.Projection.Dependencies.
    // - currentType : the receiver type where the descriptor was found
    // - inputType   : the child/input type for aggregate descent at that level
    // Skips expressions already present in Dependencies (deduplication).
    // Returns false (with error set) on cycle or parse failure.
    private static bool BindSourceExpressionsForDescriptor(
        FarmProcess? process,
        MetricCatalogs catalogs,
        Type currentType,
        Type? inputType,
        MetricDescriptor descriptor,
        BindContext? bindCtx,
        string outerField,
        int errorOffset,
        out MetricBindError? error)
    {
        error = null;

        if (bindCtx == null ||
            descriptor.SourceExpressions == null ||
            descriptor.SourceExpressions.Count == 0)
            return true;

        // Cycle check: if this descriptor's name is already in the binding chain,
        // we would recurse into it again — that is a cycle.
        if (bindCtx.BindingChain.Contains(descriptor.Name))
        {
            int cycleStart = bindCtx.BindingChain.IndexOf(descriptor.Name);
            string chain = string.Join(" → ",
                bindCtx.BindingChain.Skip(cycleStart).Append(descriptor.Name));
            error = new MetricBindError(outerField, errorOffset,
                $"Metric dependency cycle: {chain}.");
            return false;
        }

        bindCtx.BindingChain.Add(descriptor.Name);

        try
        {
            foreach (string srcExpr in descriptor.SourceExpressions)
            {
                // Deduplication: skip if already bound under this canonical key.
                if (bindCtx.Projection.ContainsDependency(srcExpr)) continue;

                MetricPath srcPath = new();
                int srcOffset = 0;

                bool ok = ParseExpression(
                    process, catalogs, srcPath,
                    currentType, inputType!,
                    srcExpr, ref srcOffset, out error, bindCtx);

                if (!ok) return false;

                if (srcOffset != srcExpr.Length)
                {
                    error = new MetricBindError(srcExpr, srcOffset,
                        srcExpr.Length - srcOffset,
                        $"Unexpected text in SourceExpression '{srcExpr}'.");
                    return false;
                }

                bindCtx.Projection.AddDependency(srcExpr, srcPath);
            }
        }
        finally
        {
            bindCtx.BindingChain.RemoveAt(bindCtx.BindingChain.Count - 1);
        }

        return true;
    }

    // ── public Bind overloads ─────────────────────────────────────────────────

    // Primary overload: returns structured error information.
    // Binds SourceExpressions for all descriptors encountered.
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
        var bindCtx = new BindContext(projection);

        foreach (string field in fields)
        {
            MetricPath path = new();
            int this_offset = 0;

            bool ok = ParseExpression(
                process, catalogs, path,
                type, inputType!,
                field, ref this_offset, out MetricBindError? fieldError, bindCtx);

            if (ok && this_offset != field.Length)
            {
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

    // ── reflected literal parser ──────────────────────────────────────────────
    private static bool TryParseReflectedValue(Type targetType, string token, out object? result)
    {
        result = null;

        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
        {
            if (string.Equals(token, "null", StringComparison.OrdinalIgnoreCase))
            {
                result = null;
                return true;
            }
            targetType = underlying;
        }

        if (targetType == typeof(string))
        {
            result = token;
            return true;
        }

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(token, out var b))
            {
                result = b;
                return true;
            }
            return false;
        }

        if (targetType == typeof(int))
        {
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(double))
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(float))
        {
            if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(long))
        {
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(decimal))
        {
            if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(TimeSpan))
        {
            if (TimeSpan.TryParse(token, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(DateTime))
        {
            if (DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(short))
        {
            if (short.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(byte))
        {
            if (byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(sbyte))
        {
            if (sbyte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(uint))
        {
            if (uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(ulong))
        {
            if (ulong.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(ushort))
        {
            if (ushort.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(char))
        {
            if (char.TryParse(token, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType == typeof(Guid))
        {
            if (Guid.TryParse(token, out var v))
            {
                result = v;
                return true;
            }
            return false;
        }

        if (targetType.IsEnum)
        {
            if (Enum.TryParse(targetType, token, ignoreCase: true, out var v) &&
                (Enum.IsDefined(targetType, v) || targetType.IsDefined(typeof(FlagsAttribute), false)))
            {
                result = v;
                return true;
            }
            return false;
        }

        return false;
    }
}
