using System.Reflection;
using System.Text;

namespace TruthInTheFlip.Format.Options;

public class DelegateMethodRegistry
{
    public class RegistryMethod
    {
        public Delegate Method { get; set; } = default!;
        public string Help { get; set; } = "";
        public VersioningAttribute? RequiredVersion { get; set; }

        public string Name { get; set; } = "";
        public bool IsDefault { get; set; } = false;

        public class Parameter
        {
            public string Name { get; set; } = "";
            public Type Type { get; set; } = default!;
            public string Default { get; set; } = "";
        }

        public List<Parameter> Parameters { get; set; } = new List<Parameter>();

        public RegistryMethod Versioning(string requiredVersion, string? versionCapExclusive = null,
            bool obsolete = false)
        {
            RequiredVersion = new VersioningAttribute(requiredVersion, versionCapExclusive, obsolete);
            return this;
        }
    }

    public string ElementDescription { get; set; } = "";
    public Type RegistryType { get; private set; }

    public DelegateMethodRegistry(Type registryType, string elementDescription)
    {
        this.ElementDescription = elementDescription;
        this.RegistryType = registryType;
    }

    // The registry of all available windowing strategies
    public Dictionary<string, RegistryMethod> Strategies { get; set; } = new Dictionary<string, RegistryMethod>();

    public Dictionary<Type, DelegateMethodRegistry> TypeHandlers { get; set; } =
        new Dictionary<Type, DelegateMethodRegistry>();

    public DelegateMethodRegistry AddTypeHandler(DelegateMethodRegistry handler)
    {
        TypeHandlers[handler.RegistryType] = handler;
        return this;

    }

    public class RegistryParseResult
    {
        public DelegateMethodRegistry Registry { get; private set; }
        public string MethodName { get; set; } = "";
        public RegistryMethod? strategyDef { get; set; }
        public List<object> ArgValues { get; set; } = new();
        public DelegateMethodRegistry.RegistryMethod? Method { get; set; }
        public object? Strategy { get; set; }
        public VersioningAttribute? RequiredVersion { get; set; }

        public RegistryParseResult(DelegateMethodRegistry registry)
        {
            Registry = registry;
        }
    }

    /// <summary>
    /// Registers a new custom Windowing strategy into the CLI parser.
    /// </summary>
    public RegistryMethod AddSource(Delegate func, string name, string help, string[] parameterNames,
        string[] defaultValues)
    {
        if (Strategies.ContainsKey(name)) throw new ArgumentException($"Collision on window strategy {name}");

        var methodDef = new RegistryMethod
        {
            Name = name,
            Method = func,
            Help = help,
        };

        var methodInfo = func.Method;
        var reflectionParams = methodInfo.GetParameters();

        if (parameterNames.Length != reflectionParams.Length || defaultValues.Length != reflectionParams.Length)
        {
            throw new ArgumentException(
                $"Parameter count mismatch for strategy {name}. Expected {reflectionParams.Length}.");
        }

        for (int i = 0; i < reflectionParams.Length; i++)
        {
            methodDef.Parameters.Add(new RegistryMethod.Parameter
            {
                Name = parameterNames[i],
                Type = reflectionParams[i].ParameterType,
                Default = defaultValues[i]
            });
        }

        Strategies[name] = methodDef;
        return methodDef;
    }

    /// <summary>
    /// Scans host type for static methods with the correct attributes and loads them into the registry.
    /// </summary>
    public virtual DelegateMethodRegistry AddFromHostType(Type host)
    {
        var methods = host.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.ReturnType == RegistryType);

        foreach (var method in methods)
        {
            var helpAttr =
                method.GetCustomAttributes(typeof(StringHelpAttribute), false).FirstOrDefault() as
                    StringHelpAttribute;
            var versionAttr =
                method.GetCustomAttributes(typeof(VersioningAttribute), false).FirstOrDefault() as
                    VersioningAttribute;

            var parameters = method.GetParameters();
            string[] paramNames = new string[parameters.Length];
            string[] defValues = new string[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                paramNames[i] = parameters[i].Name ?? $"arg{i}";
                var defAttr =
                    parameters[i].GetCustomAttributes(typeof(StringDefaultAttribute), false).FirstOrDefault() as
                        StringDefaultAttribute;
                defValues[i] = defAttr?.Value ?? "";
            }

            // Create a generic delegate targeting the static method
            Type delegateType = UtilT.GetDelegateType(parameters, method.ReturnType);
            Delegate del = Delegate.CreateDelegate(delegateType, method);

            AddSource(
                del,
                method.Name,
                helpAttr?.Description ?? "No description provided.",
                paramNames,
                defValues
            ).RequiredVersion = versionAttr;
        }

        return this;
    }


    public virtual bool TryParse(Option o, List<string> command_args, int index, ref int status, SOut message,
        SOut errorMessage, out RegistryParseResult? result)
    {
        result = null;

        if (index >= command_args.Count)
        {
            errorMessage(
                $"Option \'{o.Name}\' missing {(ElementDescription != "" ? (ElementDescription + " ") : "")}strategy name parameter");
            status = -1;
            return false;
        }

        result = new RegistryParseResult(this);

        result.MethodName = command_args[index];
        command_args.RemoveAt(index);
        result.ArgValues.Clear();

        if (result.MethodName == "list")
        {
            message(List(o), false);
            o.Enabled = false;
            o.WantExit = true;
            return true;
        }

        if (result.MethodName.ToLower() == "def")
        {
            result.MethodName =
                UtilT.ThrowIfNull((from v in Strategies.Values where v.IsDefault select v.Name).FirstOrDefault(),
                    "Default strategy not found");
            if (this.Strategies[result.MethodName].Parameters.Count > 0) result.ArgValues.Add("def");
        }
        else
        {
            if (!Strategies.ContainsKey(result.MethodName))
            {
                errorMessage(
                    $"Error: Unknown {(ElementDescription != "" ? (ElementDescription + " ") : "")}strategy '{result.MethodName}'.");
                status = -1;
                return false;
            }

            int expectedParams = Strategies[result.MethodName].Parameters.Count;


            // Slurp up the required number of parameters
            for (int i = 0; i < expectedParams; i++)
            {

                if (index >= command_args.Count)
                {
                    errorMessage(
                        $"Option '{o.Name}' {result.MethodName} is missing parameter {i + 1} of {expectedParams}.");
                    status = -1;
                    return false;
                }

                if (TypeHandlers.TryGetValue(Strategies[result.MethodName].Parameters[i].Type, out var handler))
                {
                    if (!handler.TryParse(o, command_args, index, ref status, message, errorMessage, out var res) ||
                        status != 0)
                    {
                        errorMessage(
                            $"Option '{o.Name}' {result.MethodName} parameter {i + 1} failed to parse: {message}");
                        return false;
                    }

                    if (res == null)
                    {
                        errorMessage(
                            $"Option '{o.Name}' {result.MethodName} parameter {i + 1} failed to parse: {message}");
                        return false;
                    }

                    if (o.WantExit) return true;

                    result.ArgValues.Add(res);
                    continue;

                }

                result.ArgValues.Add(command_args[index]);
                command_args.RemoveAt(index);

                if (result.ArgValues.Last() as string == "def") break;
            }
        }

        CompileStrategy(result, errorMessage, ref status);
        return true;
    }


    protected virtual void CompileStrategy(RegistryParseResult result, SOut errorMessage, ref int exitStatus)
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
                if (result.ArgValues.Count > 0 && result.ArgValues[defI++] as string == "def")
                    rawVal = paramDef.Default;
                else
                {
                    rawVal = result.ArgValues[i] as string;
                    defI++;
                }

                // Handle string type directly
                if (paramDef.Type == typeof(string))
                {
                    parsedArgs[i] = UtilT.ThrowIfNull(rawVal, "rawVal");
                }
                else if (rawVal == null)
                {
                    if (result == null || result.ArgValues == null) throw new ArgumentNullException(nameof(result), "Result or ArgValues cannot be null");
                    parsedArgs[i] = UtilT.ThrowIfNull(((RegistryParseResult)result.ArgValues[i]).Strategy, "Strategy");
                }
                else
                {
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

            result.Strategy = result.strategyDef.Method.DynamicInvoke(parsedArgs);
            result.RequiredVersion = result.strategyDef.RequiredVersion;
        }
        catch (Exception ex)
        {
            errorMessage(
                $"Error compiling {(ElementDescription != "" ? (ElementDescription + " ") : "")}strategy: {ex.Message}");
            exitStatus = -1;
        }
    }

    public virtual string Info(Option o, RegistryParseResult result)
    {
        if (!Strategies.TryGetValue(result.MethodName, out var def)) return "Error";

        string isDefault = (result.strategyDef?.IsDefault == true) ? "(default)" : "";

        List<string> formattedArgs = new List<string>();
        int argIndex = 0;

        for (int i = 0; i < def.Parameters.Count; i++)
        {
            var param = def.Parameters[i];
            string valStr;

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
                else if (arg is RegistryParseResult nestedResult &&
                         TypeHandlers.TryGetValue(param.Type, out var handler))
                {
                    // Recursively grab the info from the nested registry result
                    string nestedInfo = handler.Info(o, nestedResult).Trim();

                    // Indent the nested info for a clean tree-like display
                    valStr = $"[\n    {nestedInfo.Replace("\n", "\n    ")}\n]";
                    argIndex++;
                }
                else
                {
                    valStr = arg?.ToString() ?? "null";
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

        string joinedArgs = formattedArgs.Count > 0 ? string.Join(", ", formattedArgs) : "None";

        return $@"
{ElementDescription}:  {result.MethodName}{isDefault}          //{def.Help}
Values:         {joinedArgs}
";
    }

    public virtual string List(Option o)
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.AppendLine(
            $"{o.NameString()}Available {(ElementDescription != "" ? (ElementDescription + " ") : "")}Strategies: ");

        foreach (var kvp in Strategies)
        {
            string methodTypeStr = kvp.Key;
            string defStr = "def=";

            if (kvp.Value.Parameters.Count > 0)
            {
                foreach (var param in kvp.Value.Parameters)
                {
                    string name;
                    if (TypeHandlers.TryGetValue(param.Type, out var handler)) name = handler.ElementDescription;
                    else name = param.Type.Name;

                    methodTypeStr += $" <{name}>";
                    defStr += $" \"{param.Default}\"";
                }
            }

            string versionStr = kvp.Value.RequiredVersion != null ? $"{kvp.Value.RequiredVersion.Version}" : "";

            stringBuilder.AppendLine(UtilT.PadRight("") +
                                     UtilT.PadRight(methodTypeStr + (kvp.Value.IsDefault ? " (default)" : ""), 40) +
                                     UtilT.PadRight(defStr) +
                                     versionStr);
            stringBuilder.AppendLine(UtilT.PadRight("") + $"  {kvp.Value.Help}");
        }

        return stringBuilder.ToString();
    }

    public virtual string GetHelp(Option o)
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(UtilT.PadRight($"  {o.Name} list") +
                                 $"List available {(ElementDescription != "" ? (ElementDescription + " ") : "")}strategies");
        stringBuilder.AppendLine(UtilT.PadRight($"  {o.Name} def") +
                                 $"Use default {(ElementDescription != "" ? (ElementDescription + " ") : "")}");
        stringBuilder.AppendLine(UtilT.PadRight($"  {o.Name} <string> [params...]") +
                                 $"Configure {(ElementDescription != "" ? (ElementDescription + " ") : "")}");
        stringBuilder.AppendLine(UtilT.PadRight($"  {o.Name} <string> def") +
                                 $"Configure specific {(ElementDescription != "" ? (ElementDescription + " ") : "")}with default parameters");

        return stringBuilder.ToString();
    }
    
}