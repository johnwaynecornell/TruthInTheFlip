namespace FluentCommandLine;

using System.Reflection;
using System.Text;

public class FluentMethodRegistry
{

    public string ElementDescription { get; set; } = "";
    public Type RegistryType { get; private set; }

    public FluentMethodRegistry(Type registryType, string elementDescription)
    {
        this.ElementDescription = elementDescription;
        this.RegistryType = registryType;
    }

    // The registry of all available RegistryType creation strategies
    public Dictionary<string, FluentMethod> Strategies { get; set; } = new Dictionary<string, FluentMethod>();

    public class RegistryParseResult
    {
        public FluentMethodRegistry Registry { get; private set; }
        public string MethodName { get; set; } = "";
        public FluentMethod? strategyDef { get; set; }
        public List<object> ArgValues { get; set; } = new();
        public FluentMethod? Method { get; set; }
        public object? Result { get; set; }

        public RegistryParseResult(FluentMethodRegistry registry)
        {
            Registry = registry;
        }
    }

    /// <summary>
    /// Registers a new custom strategy into the CLI parser.
    /// </summary>
    public FluentMethod AddSource(Delegate func, string name, string help, string[] parameterNames,
        string[] defaultValues, string?[] parameterHelp)
    {
        var methodInfo = func.Method;

        FluentMethod Ret = AddSource(methodInfo, name, help, parameterNames, defaultValues, parameterHelp);
        Ret._Method = func;

        return Ret;
    }

    /// <summary>
    /// Registers a new custom strategy into the CLI parser.
    /// </summary>
    public FluentMethod AddSource(MethodInfo func, string name, string help, string[] parameterNames,
        string[] defaultValues, string?[] parameterHelp)
    {
        if (Strategies.ContainsKey(name)) throw new ArgumentException($"Collision on window strategy {name}");

        var methodDef = new FluentMethod
        {
            Name = name,
            MethodInfo = func,
            Help = help,
        };

        var methodInfo = func;
        var reflectionParams = methodInfo.GetParameters();

        if (parameterNames.Length != reflectionParams.Length || defaultValues.Length != reflectionParams.Length)
        {
            throw new ArgumentException(
                $"Parameter count mismatch for strategy {name}. Expected {reflectionParams.Length}.");
        }

        for (int i = 0; i < reflectionParams.Length; i++)
        {
            methodDef.Parameters.Add(new FluentMethod.Parameter
            {
                Name = parameterNames[i],
                Type = reflectionParams[i].ParameterType,
                Default = defaultValues[i],
                Help = parameterHelp[i]
            });
        }

        Strategies[name] = methodDef;
        return methodDef;
    }

    public delegate void SOut(String message = "", bool nl = true);

    public virtual bool TryParse(FluentEnvironment o, IReadOnlyList<string> commandArgs, ref int cursor, ref int status,
        SOut message,
        SOut errorMessage, out RegistryParseResult? result)
    {
        result = null;
        int startCursor = cursor;

        if (cursor >= commandArgs.Count)
        {
            errorMessage(
                $"FluentEnvironment missing {(ElementDescription != "" ? (ElementDescription + " ") : "")}strategy name parameter");
            status = -1;
            return false;
        }

        result = new RegistryParseResult(this);

        result.MethodName = commandArgs[cursor];

        if (result.MethodName == "list")
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(o.ListCapture(() => { return List(o); }));

            message(sb.ToString(), false);
            o.WantExit = true;
            cursor++;
            result = null;
            return true;
        }

        if (result.MethodName.ToLower() == "def")
        {
            result.MethodName =
                ThrowIfNull((from v in Strategies.Values where v.IsDefault select v.Name).FirstOrDefault(),
                    "Default strategy not found");

            if (Strategies.TryGetValue(result.MethodName, out var actualMethodDef))
            {
                result.Method = actualMethodDef;
            }

            if (result.Method.Parameters.Count > 0)
            {
                result.ArgValues.Add("def");
                cursor++;
            }
        }
        else
        {
            if (!Strategies.TryGetValue(result.MethodName, out var methodDef))
            {
                // errorMessage(
                //     $"Error: Unknown {(ElementDescription != "" ? (ElementDescription + " ") : "")}strategy '{result.MethodName}'.");
                // status = -1;
                result = null;
                return false;
            }

            result.Method = methodDef;

            int expectedParams = result.Method.Parameters.Count;

            cursor++;
            result.ArgValues.Clear();

            // Slurp up the required number of parameters
            int i;

            for (i = 0; i < expectedParams; i++)
            {
                if (cursor < commandArgs.Count && commandArgs[cursor] == "def")
                {
                    cursor++;

                    for (int i2 = i; i2 < expectedParams; i2++)
                    {
                        var pDef = result.Method.Parameters[i2];
                        int dummyCursor = 0;
                        int dummyStatus = 0;
                        if (o.TryParseParameter(pDef.Type, new[] { pDef.Default }, ref dummyCursor, ref dummyStatus,
                                message, errorMessage, out var res))
                        {
                            result.ArgValues.Add(res);
                        }
                        else
                        {
                            result.ArgValues.Add("def");
                        }
                    }

                    break;
                }

                if (cursor >= commandArgs.Count)
                {
                    errorMessage(
                        $"FluentEnvironment '{result.MethodName}' is missing parameter {i + 1} of {expectedParams}.");
                    status = -1;
                    cursor = startCursor;
                    return false;
                }

                {
                    if (o.TryParseParameter(result.Method.Parameters[i].Type, commandArgs, ref cursor,
                            ref status, message, errorMessage, out var result2))
                    {
                        if ((result2 == null) && o.WantExit)
                        {
                            result = null;
                            return true;
                        }

                        result.ArgValues.Add(result2);
                        continue;
                    }

                    if (status != 0)
                    {
                        cursor = startCursor;
                        return false;
                    }
                }

                result.ArgValues.Add(commandArgs[cursor]);
                cursor++;

                if (result.ArgValues.Last() as string == "def")
                {
                    for (int i2 = i + 1; i2 < expectedParams; i2++)
                    {
                        result.ArgValues.Add("def");
                    }

                    break;
                }
            }
        }

        CompileStrategy(o, result, errorMessage, ref status);
        if (status != 0)
        {
            cursor = startCursor;
            return false;
        }

        return true;
    }


    protected virtual void CompileStrategy(FluentEnvironment o, RegistryParseResult result, SOut errorMessage, ref int exitStatus)
    {
        if (exitStatus != 0) return;

        try
        {
            result.strategyDef = null;
            if (!Strategies.TryGetValue(result.MethodName, out var _strategyDef))
            {
                errorMessage(
                    $"Error: {(ElementDescription != "" ? (ElementDescription + " ") : "")}Strategy '{result.MethodName}' not found in registry.");
                exitStatus = -1;
                return;
            }

            result.strategyDef = _strategyDef;

            object[] parsedArgs = new object[result.strategyDef.Parameters.Count];

            int defI = 0;
            for (int i = 0; i < result.strategyDef.Parameters.Count; i++)
            {
                var paramDef = result.strategyDef.Parameters[i];
                string? rawVal;
                if (result.ArgValues.Count > 0 && defI < result.ArgValues.Count &&
                    result.ArgValues[defI] as string == "def")
                {
                    rawVal = paramDef.Default;
                    defI++;
                }
                else
                {
                    rawVal = result.ArgValues[i] as string;
                    defI++;
                }

                // Handle string type directly
                if (paramDef.Type == typeof(string))
                {
                    parsedArgs[i] = ThrowIfNull(rawVal, "rawVal");
                }
                else if (rawVal == null)
                {
                    if (result == null || result.ArgValues == null)
                        throw new ArgumentNullException(nameof(result), "Result or ArgValues cannot be null");
                    if (result.ArgValues[i] is RegistryParseResult rpr && rpr != null)
                        parsedArgs[i] = ThrowIfNull(rpr.Result, "Strategy");
                    else if (paramDef.Type.IsArray)
                    {
                        object[] p = (object[])result.ArgValues[i];
                        Array array = Array.CreateInstance(paramDef.Type.GetElementType(), p.Length);
                        for (int j = 0; j < p.Length; j++)
                        {
                            object v = ThrowIfNull(p[j], "p[j]");
                            if (v is RegistryParseResult r) v = ThrowIfNull(r.Result, "Strategy");
                            array.SetValue(v, j);
                        }

                        parsedArgs[i] = array;
                    }
                    else parsedArgs[i] = result.ArgValues[i];
                }
                else
                {
                    if (o.Registries.TryGetValue(paramDef.Type, out var r))
                    {
                        errorMessage($"{rawVal} is not a valid <{r.RegistryType.Name}> {r.ElementDescription}");
                        exitStatus = -1;
                        return;
                    }

                    if (paramDef.Type.IsEnum)
                    {
                        parsedArgs[i] = Enum.Parse(paramDef.Type, rawVal);
                        continue;
                    }
                    
                    // Use reflection to find and invoke the Parse method
                    var parseMethod = paramDef.Type.GetMethod("Parse",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string) },
                        null);

                    if (parseMethod == null)
                    {
                        errorMessage($"Error: Type '{paramDef.Type.Name}' does not have a Parse(string) method.");
                        exitStatus = -1;
                        return;
                    }

                    try
                    {
                        parsedArgs[i] = parseMethod.Invoke(null, new object[] { rawVal })!;
                    }
                    catch (Exception ex)
                    {
                        errorMessage(
                            $"Error parsing '{rawVal}' as {paramDef.Type.Name}: {ex.InnerException?.Message ?? ex.Message}");
                        exitStatus = -1;
                        return;
                    }
                }
            }

            result.Result = result.strategyDef.Method.DynamicInvoke(parsedArgs);
        }
        catch (Exception ex)
        {
            errorMessage(
                $"Error compiling {(ElementDescription != "" ? (ElementDescription + " ") : "")}strategy: {ex}");
            exitStatus = -1;
        }
    }

    public static T ThrowIfNull<T>(T? value, string message)
    {
        if (value == null) throw new ArgumentNullException(message);
        return value;
    }

    
    public static string ValString(
        FluentEnvironment environment,
        object? value,
        Type declaredType)
    {
        if (value is RegistryParseResult nestedResult)
        {
            if (!environment.Registries.TryGetValue(
                    declaredType,
                    out var handler))
            {
                throw new InvalidOperationException(
                    $"No handler found for type {declaredType}.");
            }

            string nestedInfo = handler
                .Info(environment, nestedResult)
                .Trim();

            return $"<{nestedResult.Registry.RegistryType.Name}> {{\n{Indent(nestedInfo, 4)}\n}}";
        }

        if (declaredType.IsArray)
        {
            if (value is not Array array)
            {
                throw new InvalidOperationException(
                    $"Expected an array for type {declaredType}.");
            }

            Type elementType = declaredType.GetElementType()
                               ?? throw new InvalidOperationException(
                                   $"Array type '{declaredType}' has no element type.");

            var values = array
                .Cast<object?>()
                .Select(item => ValString(
                    environment,
                    item,
                    elementType))
                .ToList();

            string compact = $"[{string.Join(", ", values)}]";

            bool multiLine =
                values.Any(item => item.Contains('\n')) ||
                compact.Length > 60;

            if (!multiLine)
                return compact;

            return "[\n" +
                   string.Join(
                       ",\n",
                       values.Select(item => Indent(item, 4))) +
                   "\n]";
        }

        return value?.ToString() ?? "null";
    }

    private static string Indent(string text, int spaces)
    {
        string prefix = new(' ', spaces);

        return prefix +
               text.Replace("\n", "\n" + prefix);
    }

    public virtual string Info(FluentEnvironment o, RegistryParseResult result)
    {
        if (!Strategies.TryGetValue(result.MethodName, out var def)) return "Error";

        string isDefault = (result.strategyDef?.IsDefault == true) ? "(default)" : "";

        List<string> formattedArgs = new List<string>();
        int argIndex = 0;

        StringBuilder commandText = new StringBuilder();
        commandText.Append(result.MethodName);
        
        
        for (int i = 0; i < def.Parameters.Count; i++)
        {
            var param = def.Parameters[i];
            string valStr;
            
            commandText.Append(" ");
            commandText.Append(param.Name);
            
            if (argIndex < result.ArgValues.Count)
            {
                var arg = result.ArgValues[argIndex];
                if (arg is string s && s.ToLower() == "def")
                {
                    valStr = $"def=\"{param.Default}\"";
                    // If the user entered "def" and it's the last argument, it usually implies defaults for all remaining parameters
                    if (argIndex < result.ArgValues.Count - 1)
                    {
                        argIndex++;
                    }
                }
                else
                { 
                    valStr = ValString(o, arg, param.Type);
                    argIndex++;
                }
            }
            else
            {
                // Unspecified arguments likely defaulted
                valStr = $"implicit_def=\"{param.Default}\"";
            }

            formattedArgs.Add($"{param.Name}={valStr}");
        }
        
        bool multiLine = formattedArgs.Any(x => x.Contains('\n'));

        string joinedArgs = multiLine
            ? "Values:\n" + string.Join(
                "\n",
                formattedArgs.Select(x => Indent(x, 4)))
            : "Values:         " + string.Join(", ", formattedArgs);

        //string joinedArgs = formattedArgs.Count > 0 ? string.Join(", ", formattedArgs) : "None";

        
        
        
        return $@"
{ElementDescription}:  {commandText.ToString()}{isDefault}          //{def.Help}
{joinedArgs}
";
    }

    public virtual string List(FluentEnvironment o)
    {
        return ListNew(o);
    }
    
    public virtual string ListNew(FluentEnvironment o)
    {
        StringBuilder stringBuilder = new StringBuilder();

        //stringBuilder.AppendLine(
        //    $"Available {(ElementDescription != "" ? (ElementDescription + " ") : "")}Strategies: ");

        bool first = true;

        foreach (var kvp in Strategies)
        {
            if (!first) stringBuilder.AppendLine(); else first = false;
            
            //StringBuilder command = new StringBuilder();
            stringBuilder.Append("    " + kvp.Key);
            StringBuilder pool = new StringBuilder();
            pool.AppendLine();
            
            if (kvp.Value.Parameters.Count > 0)
            {
                foreach (var param in kvp.Value.Parameters)
                {

                    string name;
                    name = param.Type.Name;

                    stringBuilder.Append(" " + param.Name);

                    pool.Append("    " + "    " + FluentEnvironment.PadRight(param.Name) + FluentEnvironment.PadRight($"<{name}>"));
                    bool both = false;
                    if (param.Help != null)
                    {
                        both = true;
                        pool.Append(param.Help);
                    }
                    
                    if (param.Default != "")
                    {
                        if (both) pool.Append("    ");
                        pool.Append($"default: \"{param.Default}\"");
                    }

                    pool.AppendLine();

                    o.ListReference(param.Type);
                }
            }

            
            if (kvp.Value.Help != null) stringBuilder.AppendLine("\n        " + kvp.Value.Help);
            
            stringBuilder.Append(pool);

            
            
        }


        return stringBuilder.ToString();
    }

    public virtual string ListLegacy(FluentEnvironment o)
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.AppendLine(
            $"Available {(ElementDescription != "" ? (ElementDescription + " ") : "")}Strategies: ");

        foreach (var kvp in Strategies)
        {
            string methodTypeStr = kvp.Key;
            string defStr = "def=";

            if (kvp.Value.Parameters.Count > 0)
            {
                foreach (var param in kvp.Value.Parameters)
                {
                    string name;
                    if (o.Registries.TryGetValue(param.Type, out var handler)) name = handler.ElementDescription;
                    else name = param.Type.Name;

                    methodTypeStr += $" <{name}>";
                    defStr += $" \"{param.Default}\"";
                }
            }

            stringBuilder.AppendLine(FluentEnvironment.PadRight("") +
                                     FluentEnvironment.PadRight(methodTypeStr + (kvp.Value.IsDefault ? " (default)" : ""), 40) +
                                     FluentEnvironment.PadRight(defStr));
            stringBuilder.AppendLine(FluentEnvironment.PadRight("") + $"  {kvp.Value.Help}");
        }

        return stringBuilder.ToString();
    }
}