using System.Reflection;
using System.Text;

namespace TruthInTheFlip.Format.Options;

public class DelegateMethodRegistry
{
    
}
public class DelegateMethodRegistry<T> : DelegateMethodRegistry
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

    public DelegateMethodRegistry(string elementDescription)
    {
        this.ElementDescription = elementDescription;
    }

    // The registry of all available windowing strategies
    public Dictionary<string, RegistryMethod> Strategies { get; set; } = new Dictionary<string, RegistryMethod>();

    public class RegistryParseResult
    {
        public string MethodName { get; set; } = "";
        public RegistryMethod? strategyDef { get; set; }
        public List<string> ArgValues { get; set; } = new();
        public DelegateMethodRegistry<T>.RegistryMethod? Method { get; set; }
        public T? Strategy { get; set; }
        public VersioningAttribute? RequiredVersion { get; set; }
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
            .Where(m => m.ReturnType == typeof(T));

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
            errorMessage($"Option \'{o.Name}\' missing {(ElementDescription != "" ? (ElementDescription+" ") : "")}strategy name parameter");
            status = -1;
            return false;
        }
        
        result = new RegistryParseResult();

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
            result.MethodName = UtilT.ThrowIfNull((from v in Strategies.Values where v.IsDefault select v.Name).FirstOrDefault(), "Default strategy not found");
            result.ArgValues.Add("def");
        }
        else
        {
            if (!Strategies.ContainsKey(result.MethodName))
            {
                errorMessage($"Error: Unknown {(ElementDescription != "" ? (ElementDescription+" ") : "")}strategy '{result.MethodName}'.");
                status = -1;
                return false;
            }

            int expectedParams = Strategies[result.MethodName].Parameters.Count;

            // Slurp up the required number of parameters
            for (int i = 0; i < expectedParams; i++)
            {
                if (index >= command_args.Count)
                {
                    errorMessage($"Option '{o.Name}' {result.MethodName} is missing parameter {i + 1} of {expectedParams}.");
                    status = -1;
                    return false;
                }

                result.ArgValues.Add(command_args[index]);
                command_args.RemoveAt(index);

                if (result.ArgValues.Last() == "def") break;
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
                errorMessage($"Error: {(ElementDescription != "" ? (ElementDescription+" ") : "")}Strategy '{result.MethodName}' not found in registry.");
                exitStatus = -1;
                return;
            }
            result.strategyDef = _strategyDef;

            object[] parsedArgs = new object[result.strategyDef.Parameters.Count];

            int defI = 0;
            for (int i = 0; i < result.strategyDef.Parameters.Count; i++)
            {
                var paramDef = result.strategyDef.Parameters[i];
                string rawVal;
                if (result.ArgValues.Count > 0 && result.ArgValues[defI++] == "def") rawVal = paramDef.Default;
                else
                {
                    rawVal = result.ArgValues[i];
                    defI++;
                }

                // Handle string type directly
                if (paramDef.Type == typeof(string))
                {
                    parsedArgs[i] = rawVal;
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

            result.Strategy = (T?)result.strategyDef.Method.DynamicInvoke(parsedArgs);
            result.RequiredVersion = result.strategyDef.RequiredVersion;
        }
        catch (Exception ex)
        {
            errorMessage($"Error compiling {(ElementDescription != "" ? (ElementDescription+" ") : "")}strategy: {ex.Message}");
            exitStatus = -1;
        }
    }
    
    public virtual string Info(Option o, RegistryParseResult result)
    {
        if (!Strategies.TryGetValue(result.MethodName, out var def)) return "Error";
            
        string isDefault = (result.strategyDef?.IsDefault == true) ? "(default)" : "";
        string joinedArgs = string.Join(" ", result.ArgValues);
        if ((result.ArgValues.Count > 0) && (result.ArgValues[0] == "def")) joinedArgs += $" = \"{def.Parameters[0].Default}\"";
            
        return $@"
TrackerWindow:  {result.MethodName}{isDefault}          //{def.Help}
Values:         {joinedArgs}
";
    }

    public virtual string List(Option o)
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"{o.NameString()}Available {(ElementDescription != "" ? (ElementDescription+" ") : "")}Strategies: ");

        foreach (var kvp in Strategies)
        {
            string methodTypeStr = kvp.Key;
            string defStr = "def=";

            if (kvp.Value.Parameters.Count > 0)
            {
                foreach (var param in kvp.Value.Parameters)
                {
                    methodTypeStr += $" <{param.Type.Name}>";
                    defStr += $" \"{param.Default}\"";
                }
            }

            string versionStr = kvp.Value.RequiredVersion != null ? $"{kvp.Value.RequiredVersion.Version}" : "";

            stringBuilder.AppendLine(UtilT.PadRight("") + UtilT.PadRight(methodTypeStr +(kvp.Value.IsDefault ? " (default)" : ""), 40) + UtilT.PadRight(defStr) +
                                     versionStr);
            stringBuilder.AppendLine(UtilT.PadRight("") + $"  {kvp.Value.Help}");
        }

        return stringBuilder.ToString();
    }

    public virtual string GetHelp(Option o)
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(UtilT.PadRight($"  {o.Name} list") + $"List available {(ElementDescription != "" ? (ElementDescription+" ") : "")}strategies");
        stringBuilder.AppendLine(UtilT.PadRight($"  {o.Name} def") + $"Use default {(ElementDescription != "" ? (ElementDescription+" ") : "")}");
        stringBuilder.AppendLine(UtilT.PadRight($"  {o.Name} <string> [params...]") + $"Configure {(ElementDescription != "" ? (ElementDescription+" ") : "")}");
        stringBuilder.AppendLine(UtilT.PadRight($"  {o.Name} <string> def") + $"Configure specific {(ElementDescription != "" ? (ElementDescription+" ") : "")}with default parameters");
            
        return stringBuilder.ToString();
    }


}