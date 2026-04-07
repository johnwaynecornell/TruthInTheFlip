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
        public DelegateMethodRegistry<Func<Tracker, Tracker, bool>> Registry { get; set; }
        public DelegateMethodRegistry<Func<Tracker, Tracker, bool>>.RegistryParseResult? RegistryParseResult { get; set; }
        
        public WindowOption() : base("-window")
        {
            Registry = new DelegateMethodRegistry<Func<Tracker, Tracker, bool>>("window method");
        }
        
        public Func<Tracker, Tracker, bool>? Strategy => RegistryParseResult?.Strategy;
        
        /// <summary>
        /// Scans TrackerWindow for static methods with the correct attributes and loads them into the registry.
        /// </summary>
        public WindowOption AddDefaults()
        {
            Registry.AddFromHostType(typeof(TrackerWindow));
            Registry.Strategies["WindowByTotal"].IsDefault = true;
            return this;

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

            if (!Registry.TryParse(this, command_args, index, ref status, message, errorMessage, out var res)) return false;
            RegistryParseResult = res;
            
            return true;
        }

        public override string Info()
        {
            var res = UtilT.ThrowIfNull(RegistryParseResult, "RegistryParseResult");
            return Registry.Info(this, res);
        }
        
        public virtual string List()
        {
            return Registry.List(this);
        }
        
        public override string GetHelp()
        {
            return Registry.GetHelp(this);
        }
        
        public override string DisabledInfo()
        {
            return $"{NameString()}Disabled (Using default Lifetime processing)\n";
        }
    }
}
