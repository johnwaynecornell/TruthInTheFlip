using System.Reflection;
using System.Text;
using TruthInTheFlip_sample_report2;
using TruthInTheFlip.Format;
using TruthInTheFlip.Format.Options;

public class TrackerWindow
{
    public TrackerStore store;
    public Func<Tracker, Tracker, bool> bound;

    public UtilT.LinkNode<Tracker>? head = null;
    public UtilT.LinkNode<Tracker>? tail = null;

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

        head = tail = new UtilT.LinkNode<Tracker>((Tracker)store.NewTracker());

        ver = TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version);
        if (ver == null) throw new NullReferenceException();
    }

    public Tracker Add(Tracker In)
    {
        UtilT.LinkNode<Tracker> node = new(In);
        if (tail == null || head == null || ver == null) throw new NullReferenceException("Neither head nor tail nor ver can be null"); 
        tail = tail.Next = node;

        while (head.Next != null && !bound(In, UtilT.ThrowIfNull(head.Value, "head.Value"))) head = head.Next;

        return UtilT.Subtract(store, ver, In, UtilT.ThrowIfNull(head.Value, "head.Value"));
    }

    public class WindowOption : TrackerOption
    {
        // Storing the parsed parameters
        public string MethodName { get; private set; } = "WindowByTotal"; // Default
        public string ArgValue { get; private set; } = "100000000000"; // Default 100B

        // The actual compiled delegate ready for use
        public Func<Tracker, Tracker, bool>? WindowStrategy { get; private set; }

        public WindowOption() : base("-window")
        {
            
        }
        
        /// <summary>
        /// Attempts to consume the -window flag and its arguments.
        /// </summary>
        /// <returns>True if the flag was found and consumed; otherwise, false.</returns>
        public override bool TryParse(List<string> command_args, int index, ref int status, SOut message, SOut errorMessage)
        {
            if (!base.TryParse(command_args, index, ref status, message, errorMessage) || status != 0)
            {
                return false;
                
            }
            
            if (index >= command_args.Count)
            {
                errorMessage($"Option \'{Name}\' missing parameters");
                status = -1;
                return false;
            }

            MethodName = command_args[index];
            command_args.RemoveAt(index);
            if (MethodName.ToLower() == "def")
            {
                MethodName = "def";
                ArgValue = "def";
            }
            else if (MethodName == "list")
            {
                message(List(), false);
            
                Enabled = false;
                WantExit = true;
                
                return true;
            } else
            {
                if (index >= command_args.Count)
                {
                    errorMessage($"Option \'{Name}\' {MethodName} missing parameter");
                    status = -1;
                    return false;
                }

                ArgValue = command_args[index];
                command_args.RemoveAt(index);
            }
            
            CompileStrategy(errorMessage, ref status);
            return true;
        }

        private void CompileStrategy(SOut errorMessage, ref int exitStatus)
        {
            if (exitStatus != 0) return; // Don't bother if we are already in an error state

            try
            {
                var method = GetWindowPredicateCreateMethod();
                if (method == null)
                {
                    errorMessage($"Error: Unknown window strategy '{MethodName}'.");
                    exitStatus = -1;
                    return;
                }
                
                var paramType = method.GetParameters().FirstOrDefault()?.ParameterType;
                object? parsedArgument = null;
                
                string practicleArgValue = this.ArgValue;

                if (practicleArgValue == "def")
                {
                    if (!TryGet(method, out var help, out var argValue)) throw new Exception($"Failed to retrieve default argument for '{MethodName}'.");
                    practicleArgValue = argValue;
                }
                
                if (paramType == typeof(long)) parsedArgument = long.Parse(practicleArgValue);
                else if (paramType == typeof(TimeSpan)) parsedArgument = TimeSpan.Parse(practicleArgValue);
                else
                {
                    errorMessage($"Error: Unsupported parameter type for '{MethodName}'.");
                    exitStatus = -1;
                    return;
                }

                WindowStrategy = UtilT.ThrowIfNull(
                    (Func<Tracker, Tracker, bool>?)method.Invoke(null, new[] { parsedArgument }), 
                    "WindowStrategy");
                
                var versionAttr = method.GetCustomAttributes(typeof(VersioningAttribute), false).FirstOrDefault() as VersioningAttribute;
                RequiredVersion = TrackerStore.ReadVersion("TruthInTheFlip.v", UtilT.ThrowIfNull(versionAttr?.Version, "versionAttr?.Version"));
            }
            catch (Exception ex)
            {
                errorMessage($"Error parsing window arguments: {ex.Message}");
                exitStatus = -1;
            }
        }

        public virtual MethodInfo GetWindowPredicateCreateMethod()
        {
            string practicalMethodName = MethodName == "def" ? "WindowByTotal" : MethodName;
            
            return UtilT.ThrowIfNull(
                typeof(TrackerWindow).GetMethod(practicalMethodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                , "GetMethod(practicleMethodName");
        }
        
        public override string Info()
        {
            var method = GetWindowPredicateCreateMethod();
            
            if (method == null || !TryGet(method, out var help, out var def)) throw new Exception("We shouldnt have made it this far");
            string ArgMore = "";
            if (ArgValue == "def") ArgMore = $" = \"{def}\"";
            
            string isDefault = method.Name=="WindowByTotal" ? "(default)" : "";
            
            return $@"
TrackerWindow:  {method.Name}{isDefault}          //{help}
Value:          {ArgValue}{ArgMore}
";
        }
        
       
        public virtual bool TryGet(MethodInfo method, out string help, out string def)
        {
            help = "";
            def = "";
            var ps = method.GetParameters();
            if (ps.Length != 1) return false;
            
            var helpAttr = method.GetCustomAttributes(typeof(StringHelpAttribute), false).FirstOrDefault() as StringHelpAttribute;
            var param = UtilT.ThrowIfNull(
                method.GetParameters().FirstOrDefault(),
                "method must have 1 parameter");
            
            var defAttr = param.GetCustomAttributes(typeof(StringDefaultAttribute), false).FirstOrDefault() as StringDefaultAttribute;
                
            if (helpAttr != null) help = helpAttr.Description; else return false;
            if (defAttr != null) def = defAttr.Value;

            return true;
        }
        
        public virtual string List()
        {
            StringBuilder stringBuilder = new StringBuilder();
            
            stringBuilder.AppendLine($"{NameString()}Available Windowing Strategies: ");
            var methods = typeof(TrackerWindow).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(m => m.ReturnType == typeof(Func<Tracker, Tracker, bool>));

            foreach (var method in methods)
            {
                var helpAttr = method.GetCustomAttributes(typeof(StringHelpAttribute), false).FirstOrDefault() as StringHelpAttribute;
                var param = UtilT.ThrowIfNull(
                    method.GetParameters().FirstOrDefault(),
                    "Method must have one parameter");
                
                var defAttr = param.GetCustomAttributes(typeof(StringDefaultAttribute), false).FirstOrDefault() as StringDefaultAttribute;
                
                if (defAttr == null) throw new Exception($"{method.Name} {param.Name} must have a StringDefaultAttribute within this system");
                
                string paramType = param != null ? param.ParameterType.Name : "None";
        
                var versionAttr = method.GetCustomAttributes(typeof(VersioningAttribute), false).FirstOrDefault() as VersioningAttribute;
                if (versionAttr == null) throw new Exception($"{method.Name} must have a VersioningAttribute within this system");
                
                int[]? ver = TrackerStore.ReadVersion("TruthInTheFlip.v", versionAttr.Version);
                if (ver == null) throw new Exception($"version {versionAttr} not supported");

                stringBuilder.AppendLine(UtilT.PadRight(method.Name + $" <{paramType}>", 40) +UtilT.PadRight($"def=\"{defAttr.Value}\" ") + $"v"+TrackerStore.VersionPrint(ver));
                if (helpAttr != null)
                {       
                    stringBuilder.AppendLine($"  {helpAttr.Description}");
                }
            }
            
            return stringBuilder.ToString();

        }
        
        public override string GetHelp()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(UtilT.PadRight($"  {Name} list")+"List random sources");
            stringBuilder.AppendLine(UtilT.PadRight($"  {Name} def")+"Use default view window");
            stringBuilder.AppendLine(UtilT.PadRight($"  {Name} <string> <parameter>")+"Configure view window");
            
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
