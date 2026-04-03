using System.Reflection;
using System.Text;
using TruthInTheFlip.Format.Options;

namespace TruthInTheFlip.Format;

public class TrackerWindow
{
    public TrackerStore store;
    public Func<Tracker, Tracker, bool> bound;

    public UtilT.LinkedList<Tracker> States { get; set; } = new UtilT.LinkedList<Tracker>();
    
    public int[]? ver = null;

    /// <summary>
    /// Creates a bounding function that defines a window based on a maximum number of total flips.
    /// </summary>
    /// <param name="window">The maximum allowed difference in total flips between the head and tail of the window.</param>
    /// <returns>A predicate function evaluating if the window constraints are met.</returns>
    [StringHelp("Creates a bounding function that defines a window based on a maximum number of total flips.")]
    [Versioning("TruthInTheFlip.v1.0.1")]
    public static Func<Tracker, Tracker, bool> WindowByTotal(
        [StringDefault("100000000000")] long window = 100000000000) =>
        (A, B) => (A.total - B.total) <= window;

    /// <summary>
    /// Creates a bounding function that defines a window based on a maximum number of 'heads' flips.
    /// </summary>
    /// <param name="window">The maximum allowed difference in heads between the head and tail of the window.</param>
    /// <returns>A predicate function evaluating if the window constraints are met.</returns>
    [StringHelp("Creates a bounding function that defines a window based on a maximum number of 'heads' flips.")]
    [Versioning("TruthInTheFlip.v1.0.1")]
    public static Func<Tracker, Tracker, bool> WindowByHeads(
        [StringDefault("100000000000")] long window = 100000000000) =>
        (A, B) => (A.heads - B.heads) <= window;

    /// <summary>
    /// Creates a bounding function that defines a window based on a maximum number of 'tails' flips.
    /// </summary>
    /// <param name="window">The maximum allowed difference in tails between the head and tail of the window.</param>
    /// <returns>A predicate function evaluating if the window constraints are met.</returns>
    [StringHelp("Creates a bounding function that defines a window based on a maximum number of 'tails' flips.")]
    [Versioning("TruthInTheFlip.v1.0.1")]
    public static Func<Tracker, Tracker, bool> WindowByTails(
        [StringDefault("100000000000")] long window = 100000000000) =>
        (A, B) => (A.tails - B.tails) <= window;

    /// <summary>
    /// Creates a bounding function that defines a window based on a maximum number of anticipated matches.
    /// </summary>
    /// <param name="window">The maximum allowed difference in anticipated flips between the head and tail of the window.</param>
    /// <returns>A predicate function evaluating if the window constraints are met.</returns>
    [StringHelp("Creates a bounding function that defines a window based on a maximum number of anticipated matches.")]
    [Versioning("TruthInTheFlip.v1.0.1")]
    public static Func<Tracker, Tracker, bool> WindowByAnticipated(
        [StringDefault("100000000000")] long window = 100000000000) =>
        (A, B) => (A.anticipated - B.anticipated) <= window;

    /// <summary>
    /// Creates a bounding function that defines a window based on a precise amount of nanoseconds of wallclock compute time.
    /// </summary>
    /// <param name="window">The maximum allowed difference in nanoseconds between the head and tail of the window.</param>
    /// <returns>A predicate function evaluating if the window constraints are met.</returns>
    [StringHelp("Creates a bounding function that defines a window based on a precise amount of nanoseconds of wallclock compute time.")]
    [Versioning("TruthInTheFlip.v1.1.0")]
    public static Func<Tracker, Tracker, bool> WindowByWallclockTimeNs(
        [StringDefault("3600000000000")] long window = 3600000000000) =>
        (A, B) => (A.wallclockTimeNs - B.wallclockTimeNs) <= window;

    /// <summary>
    /// Creates a bounding function that defines a window based on a specific duration of wallclock compute time.
    /// </summary>
    /// <param name="window">The TimeSpan representing the maximum allowed duration between the head and tail of the window.</param>
    /// <returns>A predicate function evaluating if the window constraints are met.</returns>
    [StringHelp("Creates a bounding function that defines a window based on a specific duration of wallclock compute time.")]
    [Versioning("TruthInTheFlip.v1.1.0")]
    public static Func<Tracker, Tracker, bool> WindowByWallclockTime(
        [StringDefault("1:0:0")] TimeSpan window) =>
        (A, B) => (A.WallclockTime - B.WallclockTime) <= window;

    
    public TrackerWindow(TrackerStore store, Func<Tracker, Tracker, bool> bound)
    {
        this.store = store;
        this.bound = bound;

        if (store.Version == null) throw new Exception("At this point store.Version must not be null");
        
        States.Add((Tracker)store.NewTracker());

        ver = TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version);
        if (ver == null) throw new NullReferenceException();
    }
    
    public Tracker Relative(Tracker raw)
    {
        var Head = UtilT.ThrowIfNull(States.Head, "States.Head");
        return UtilT.Subtract(store, UtilT.ThrowIfNull(ver,"ver"), raw, UtilT.ThrowIfNull(Head.Value, "head.Value"));
    }
    
    public bool MaintainWindow()
    {
        bool rc = false;
        while ((States.Head != null) && (States.Tail != null) && !bound(UtilT.ThrowIfNull(States.Tail.Value, "States.Tail.Value"), UtilT.ThrowIfNull(States.Head.Value, "head.Value"))) 
        {
            States.PopHead();
            rc = true;
        }
        return rc;
    }

    public Tracker Add(Tracker In)
    {
        Tracker clone = store.Clone(In);
        States.Add(clone); 
        
        MaintainWindow();
        return Relative(clone);
    }

    public bool ReverseAdd(Tracker In)
    { 
        var clone = new UtilT.LinkNode<Tracker>(store.Clone(In));
        States.AddHead(clone); 
            
        return MaintainWindow();
    }
    
    public bool ForwardAdd(Tracker In)
    { 
        var clone = new UtilT.LinkNode<Tracker>(store.Clone(In));
        States.AddTail(clone); 
                
        return MaintainWindow();
    }
    
    public Tracker Final()
    {
        var Tail = UtilT.ThrowIfNull(States.Tail, "States.Tail"); 
        var Value = UtilT.ThrowIfNull(Tail.Value, "States.Tail.Value"); 
        return Relative(Value);
    }
    
    public class WindowOption : TrackerOption
    {
        public class CreateWindowMethod
        {
            public Delegate Method { get; set; } = default!;
            public string Help { get; set; } = "";
            public int[]? RequiredVersion { get; set; }

            public class Parameter
            {
                public string Name { get; set; } = "";
                public string Type { get; set; } = "";
                public string Default { get; set; } = "";
            }

            public List<Parameter> Parameters { get; set; } = new List<Parameter>();
        }

        // The registry of all available windowing strategies
        public Dictionary<string, CreateWindowMethod> Strategies { get; set; } = new Dictionary<string, CreateWindowMethod>();

        // Storing the parsed selection
        public string MethodName { get; private set; } = "WindowByTotal"; // Default
        public List<string> ArgValues { get; private set; } = new List<string> { "100000000000" }; // Default 100B

        // The actual compiled delegate ready for use
        public Func<Tracker, Tracker, bool>? WindowStrategy { get; private set; }

        public WindowOption() : base("-window")
        {
        }

        /// <summary>
        /// Registers a new custom Windowing strategy into the CLI parser.
        /// </summary>
        public void AddSource(Delegate func, string name, string help, int[]? requiredVersion, string[] parameterNames, string[] defaultValues)
        {
            if (Strategies.ContainsKey(name)) throw new ArgumentException($"Collision on window strategy {name}");

            var methodDef = new CreateWindowMethod
            {
                Method = func,
                Help = help,
                RequiredVersion = requiredVersion
            };

            var methodInfo = func.Method;
            var reflectionParams = methodInfo.GetParameters();

            if (parameterNames.Length != reflectionParams.Length || defaultValues.Length != reflectionParams.Length)
            {
                throw new ArgumentException($"Parameter count mismatch for strategy {name}. Expected {reflectionParams.Length}.");
            }

            for (int i = 0; i < reflectionParams.Length; i++)
            {
                methodDef.Parameters.Add(new CreateWindowMethod.Parameter
                {
                    Name = parameterNames[i],
                    Type = reflectionParams[i].ParameterType.Name,
                    Default = defaultValues[i]
                });
            }

            Strategies[name] = methodDef;
        }

        /// <summary>
        /// Scans host type for static methods with the correct attributes and loads them into the registry.
        /// </summary>
        public virtual WindowOption AddFromHostType(Type host)
        {
            var methods = host.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(m => m.ReturnType == typeof(Func<Tracker, Tracker, bool>));

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

                int[]? reqVer = versionAttr != null
                    ? TrackerStore.ReadVersion("TruthInTheFlip.v", versionAttr.Version)
                    : null;

                // Create a generic delegate targeting the static method
                Type delegateType = UtilT.GetDelegateType(parameters, method.ReturnType);
                Delegate del = Delegate.CreateDelegate(delegateType, method);

                AddSource(
                    del,
                    method.Name,
                    helpAttr?.Description ?? "No description provided.",
                    reqVer,
                    paramNames,
                    defValues
                );
            }

            return this;

        }

        /// <summary>
        /// Scans TrackerWindow for static methods with the correct attributes and loads them into the registry.
        /// </summary>
        public WindowOption AddDefaults()
        {
            return AddFromHostType(typeof(TrackerWindow));
            
        }

        /// <summary>
        /// Attempts to consume the -window flag and its arguments.
        /// </summary>
        public override bool TryParse(List<string> command_args, int index, ref int status, SOut message, SOut errorMessage)
        {
            if (!base.TryParse(command_args, index, ref status, message, errorMessage) || status != 0)
            {
                return false;
            }
            
            if (index >= command_args.Count)
            {
                errorMessage($"Option \'{Name}\' missing strategy name parameter");
                status = -1;
                return false;
            }

            MethodName = command_args[index];
            command_args.RemoveAt(index);
            ArgValues.Clear();

            if (MethodName == "list")
            {
                message(List(), false);
                Enabled = false;
                WantExit = true;
                return true;
            }

            if (MethodName.ToLower() == "def")
            {
                MethodName = "WindowByTotal";
                ArgValues.Add("def");
            }
            else
            {
                if (!Strategies.ContainsKey(MethodName))
                {
                    errorMessage($"Error: Unknown window strategy '{MethodName}'.");
                    status = -1;
                    return false;
                }

                int expectedParams = Strategies[MethodName].Parameters.Count;

                // Slurp up the required number of parameters
                for (int i = 0; i < expectedParams; i++)
                {
                    if (index >= command_args.Count)
                    {
                        errorMessage($"Option '{Name}' {MethodName} is missing parameter {i + 1} of {expectedParams}.");
                        status = -1;
                        return false;
                    }
                    ArgValues.Add(command_args[index]);
                    command_args.RemoveAt(index);
                    
                    if (ArgValues.Last() == "def") break;
                }
            }
            
            CompileStrategy(errorMessage, ref status);
            return true;
        }

        protected virtual void CompileStrategy(SOut errorMessage, ref int exitStatus)
        {
            if (exitStatus != 0) return; 

            try
            {
                if (!Strategies.TryGetValue(MethodName, out var strategyDef))
                {
                    errorMessage($"Error: Strategy '{MethodName}' not found in registry.");
                    exitStatus = -1;
                    return;
                }

                object[] parsedArgs = new object[strategyDef.Parameters.Count];

                int defI=0;
                for (int i = 0; i < strategyDef.Parameters.Count; i++)
                {
                    var paramDef = strategyDef.Parameters[i];
                    string rawVal;
                    if (ArgValues.Count > 0 && ArgValues[defI++] == "def") rawVal = paramDef.Default;
                    else
                    { 
                        rawVal = ArgValues[i];
                        defI++;
                    }

                    if (paramDef.Type == "Int64" || paramDef.Type == "long") parsedArgs[i] = long.Parse(rawVal);
                    else if (paramDef.Type == "TimeSpan") parsedArgs[i] = TimeSpan.Parse(rawVal);
                    else if (paramDef.Type == "Int32" || paramDef.Type == "int") parsedArgs[i] = int.Parse(rawVal);
                    else if (paramDef.Type == "Double" || paramDef.Type == "double") parsedArgs[i] = double.Parse(rawVal);
                    else if (paramDef.Type == "String" || paramDef.Type == "string") parsedArgs[i] = rawVal;
                    else
                    {
                        errorMessage($"Error: Unsupported parameter type '{paramDef.Type}' for '{MethodName}'.");
                        exitStatus = -1;
                        return;
                    }
                }

                WindowStrategy = (Func<Tracker, Tracker, bool>?)strategyDef.Method.DynamicInvoke(parsedArgs);
                RequiredVersion = strategyDef.RequiredVersion;
            }
            catch (Exception ex)
            {
                errorMessage($"Error compiling window strategy: {ex.Message}");
                exitStatus = -1;
            }
        }

        public override string Info()
        {
            if (!Strategies.TryGetValue(MethodName, out var def)) return "Error";
            
            string isDefault = (MethodName == "WindowByTotal" && ArgValues.Count > 0 && ArgValues[0] == "def") ? "(default)" : "";
            string joinedArgs = string.Join(" ", ArgValues);
            if (ArgValues.Count > 0 && ArgValues[0] == "def") joinedArgs += $" = \"{def.Parameters[0].Default}\"";
            
            return $@"
TrackerWindow:  {MethodName}{isDefault}          //{def.Help}
Values:         {joinedArgs}
";
        }
        
        public virtual string List()
        {
            StringBuilder stringBuilder = new StringBuilder();
            
            stringBuilder.AppendLine($"{NameString()}Available Windowing Strategies: ");

            foreach (var kvp in Strategies)
            {
                string methodTypeStr = kvp.Key;
                string defStr = "def=";
                
                if (kvp.Value.Parameters.Count > 0)
                {
                    foreach (var param in kvp.Value.Parameters)
                    {
                        methodTypeStr += $" <{param.Type}>";
                        defStr += $" \"{param.Default}\"";
                    }
                }

                string versionStr = kvp.Value.RequiredVersion != null ? $"v{TrackerStore.VersionPrint(kvp.Value.RequiredVersion)}" : "";

                stringBuilder.AppendLine(UtilT.PadRight("") + UtilT.PadRight(methodTypeStr, 40) + UtilT.PadRight(defStr) + versionStr);
                stringBuilder.AppendLine(UtilT.PadRight("") + $"  {kvp.Value.Help}");
            }
            
            return stringBuilder.ToString();
        }
        
        public override string GetHelp()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(UtilT.PadRight($"  {Name} list") + "List available window strategies");
            stringBuilder.AppendLine(UtilT.PadRight($"  {Name} def") + "Use default view window");
            stringBuilder.AppendLine(UtilT.PadRight($"  {Name} <string> [params...]") + "Configure view window");
            stringBuilder.AppendLine(UtilT.PadRight($"  {Name} <string> def") + "Configure specific view window with default parameters");
            
            return stringBuilder.ToString();
        }
        
        public override string DisabledInfo()
        {
            return $"{NameString()}Disabled (Using default Lifetime processing)\n";
        }

        public override bool ValidateVersion(int[] target_version, SOut? errorMessage)
        {
            if (Enabled && RequiredVersion != null)
            {
                if (TrackerStore.VersionCompare(target_version, RequiredVersion) < 0)
                {
                    errorMessage?.Invoke($"Error: The chosen window strategy '{MethodName}' requires a TrackerRecord file of at least version {TrackerStore.VersionPrint(RequiredVersion)}, but the loaded file is version {TrackerStore.VersionPrint(target_version)}.");
                    return false;
                }
            }
            return true;
        }
    }
}
