using System.Runtime.CompilerServices;
using TruthInTheFlip.Format.Options;

namespace TruthInTheFlip.Format;

// <summary>
/// A collection of heuristics and guessing algorithms designed to test negentropy hypotheses.
/// </summary>
/// <remarks>
/// The strategies defined here represent different approaches to predicting the next outcome 
/// in a massive random sequence. By pitting various simple algorithms (like always guessing "Same", 
/// alternating, or chasing streaks) against the PRNG over billions of flips, TruthInTheFlip can 
/// measure if specific sequences or relationships occur more frequently than pure chance allows.
/// 
/// These static methods are automatically discovered and loaded into the CLI using 
/// the <see cref="DelegateMethodRegistry"/>. You can test any strategy from the command line 
/// simply by passing its method name to the <c>-anticipation</c> flag (e.g., <c>-anticipation ClassicMetaGuess</c>).
/// 
/// Note: All strategies must return a <see cref="TrackerRunner.GuessChange"/> delegate to ensure 
/// completely thread-safe, lock-free evaluation during highly parallelized workloads.
/// </remarks>
public static class AnticipationStrategies
{
    /// <summary>
    /// The Classic TruthInTheFlip Baseline.
    /// Anticipates that if the last two flips were the SAME, the next will be DIFFERENT.
    /// If the last two flips were DIFFERENT, the next will be the SAME.
    /// </summary>
    [StringHelp("Anticipates that if the last two flips were the SAME, the next will be DIFFERENT. If the last two flips were DIFFERENT, the next will be the SAME.")]
    public static TrackerRunner.GuessChange ClassicMetaGuess()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return currentFlip == priorFlip;
        };
    }

    /// <summary>
    /// The Alternator (SDSD).
    /// Reverses the anticipation logic every single flip, regardless of the outcome.
    /// If we guessed "Same" last time, we guess "Different" this time.
    /// </summary>
    [StringHelp("Reverses the anticipation logic every single flip, regardless of the outcome. If we guessed \"Same\" last time, we guess \"Different\" this time.")]
    public static TrackerRunner.GuessChange AlternatingMetaGuess()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return !t.guessAnticipateChange;
        };
    }
    
    /// <summary>
    /// The Streak Clinger.
    /// Always anticipates that the sequence will stay the same (bets on long runs).
    /// </summary>
    [StringHelp("Always anticipates that the sequence will stay the same (bets on long runs).")]
    public static TrackerRunner.GuessChange AlwaysGuessSame()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return false;
        };
    }

    /// <summary>
    /// The Chaos Agent.
    /// Always anticipates that the sequence will flip (bets against any runs).
    /// </summary>
    [StringHelp("Always anticipates that the sequence will flip (bets against any runs).")]
    public static TrackerRunner.GuessChange AlwaysGuessDifferent()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return true;
        };
    }

    /// <summary>
    /// The Stubborn Heads Guesser.
    /// Never anticipates a change if the last flip was Heads.
    /// Always anticipates a change if the last flip was Tails.
    /// Effectively guesses "Heads" 100% of the time.
    /// </summary>
    [StringHelp("Never anticipates a change if the last flip was Heads. Always anticipates a change if the last flip was Tails. Effectively guesses \"Heads\" 100% of the time.")]
    public static TrackerRunner.GuessChange AlwaysGuessHeads()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return priorFlip; // If it was True (Heads), don't change. If it was False (Tails), change it.
        };
    }

    /// <summary>
    /// The Stubborn Tails Guesser.
    /// Effectively guesses "Tails" 100% of the time.
    /// </summary>
    [StringHelp("Effectively guesses \"Tails\" 100% of the time.")]
    public static TrackerRunner.GuessChange AlwaysGuessTails()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return !priorFlip;
        };
    }

    /// <summary>
    /// The Alternator (Odd/Even).
    /// Always anticipates that the next flip will be different from the current one.
    /// Bets on the sequence: H, T, H, T, H, T...
    /// </summary>
    [StringHelp("Always anticipates that the next flip will be different from the current one. Bets on the sequence: H, T, H, T, H, T...")]
    public static TrackerRunner.GuessChange AlwaysAnticipateChange()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return true;
        };
    }

    /// <summary>
    /// The Clinger.
    /// Always anticipates that the next flip will be exactly the same as the current one.
    /// Bets on long runs: H, H, H, H, H...
    /// </summary>
    [StringHelp("Always anticipates that the next flip will be exactly the same as the current one. Bets on long runs: H, H, H, H, H...")]
    public static TrackerRunner.GuessChange NeverAnticipateChange()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return false;
        };
    }

    /// <summary>
    /// The Gambler's Fallacy.
    /// If we just lost a bet, strongly anticipate that the *opposite* of our last guess will happen.
    /// If we won, keep doing what we did.
    /// </summary>
    [StringHelp("If we just lost a bet, strongly anticipate that the *opposite* of our last guess will happen. If we won, keep doing what we did.")]
    public static TrackerRunner.GuessChange ChaseTheLoss()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            if (!currentOutcome) // We lost
            {
                // If our last guess was to change, now guess to stay the same.
                return !lastGuess;
            }
            return lastGuess; // We won, stick to the plan.
        };
    }

    /// <summary>
    /// The Reversal (Anti-Classic).
    /// Anticipates that if the last two flips were the SAME, the next will also be the SAME.
    /// If the last two flips were DIFFERENT, the next will be DIFFERENT.
    /// </summary>
    [StringHelp("Anticipates that if the last two flips were the SAME, the next will also be the SAME. If the last two flips were DIFFERENT, the next will be DIFFERENT.")]
    public static TrackerRunner.GuessChange AntiMetaGuess()
    {
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return currentFlip != priorFlip;
        };
    }


    /// <summary>
    /// Random Heads/Tails.
    /// </summary>
    [StringHelp("Random Heads/Tails")]
    public static TrackerRunner.GuessChange RandomHT(BitFactory bitFactory)
    {
        // Create a ThreadLocal that initializes a new Consumer for each thread that accesses it
        ThreadLocal<BitFactory.Consumer> consumer = new ThreadLocal<BitFactory.Consumer>(
            () => new BitFactory.Consumer(bitFactory)
        );
    
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            // .Value will automatically initialize the Consumer for the current thread if it doesn't exist
            return priorFlip ^ consumer.Value!.getBit();
        };
    }

    /// <summary>
    /// Random Same/Different.
    /// </summary>
    [StringHelp("Random Same/Different")]
    public static TrackerRunner.GuessChange RandomSD(BitFactory bitFactory)
    {
        // Capture a new ThreadLocal instance specific to this delegate
        ThreadLocal<BitFactory.Consumer> consumer = new ThreadLocal<BitFactory.Consumer>(
            () => new BitFactory.Consumer(bitFactory)
        );
    
        return (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
        {
            return consumer.Value!.getBit();
        };
    }
    
    public class InnerAnticipation
    {
        public TrackerRunner.GuessChange InnerStrategy;
        public AnticipationLifecycle AnticipationLifecycle;

        public Tracker? InnerMaster;

        // subordinate tracker associated with each outer worker
        public ConditionalWeakTable<Tracker, Tracker> Workers = new();
        
        public TrackerRunner.AnticipateDelegate? InnerAnticipateDelegate;

        /// <summary>
        /// Conditionally Initializes the inner master tracker at the beginning of each tracker run.
        /// </summary>
        /// <param name="host_master">The host master tracker for the current run.</param>
        
        public virtual void Begin(Tracker host_master)
        {
            if (InnerMaster == null) InnerMaster = (Tracker) host_master.Store.NewTracker();
            
            var meth = AnticipationLifecycle?.Begin;
            if (meth != null) meth(host_master);
        }
        
        public virtual void BatchMemberBegin(Tracker host_tkr)
        {
            Tracker workerT = (Tracker)host_tkr.Store.NewTracker();
                
            Workers.AddOrUpdate((Tracker) host_tkr, workerT);
                
            var meth = AnticipationLifecycle?.BatchMemberBegin;
            if (meth != null) meth(workerT);
            else workerT.BatchMemberBegin();
        }

        public virtual void BatchMemberEnd(Tracker host_tkr)
        {
            if (!Workers.TryGetValue((Tracker) host_tkr, out var workerT)) throw new Exception("Worker not found");

            var meth = AnticipationLifecycle?.BatchMemberEnd;
            if (meth != null) meth(workerT);
            else workerT.BatchMemberEnd();
        }
        
        public virtual void WorkerMerge(Tracker host_master, Tracker host_tkr)
        {
            if (!Workers.TryGetValue((Tracker) host_tkr, out var workerT)) throw new Exception("Worker not found");

            var meth = AnticipationLifecycle?.WorkerMerge;
            if (meth != null) meth(InnerMaster, workerT);
            else InnerMaster.Merge(workerT);
        }

        public virtual void PostMerge(Tracker host_master)
        {
            AnticipationLifecycle?.PostMerge?.Invoke(InnerMaster);
        }
    }
    
    public class BetPersistenceState
    {
        public TrackerWindow? Window { get; set; }
        public bool full = false;
        public bool guessChange = false;
        
        public InnerAnticipation innerAnticipation;
    }

    /// <summary>
    /// Predicts Same or Different from the BetSame win rate of a completed tracker window. Using an assigned inner anticipation so as not be susceptible to internal feedback 
    /// </summary>
    [StringHelp(
        "Windowed BetSame persistence: predict Same when the completed window's BetSameWinRate is at least 50%, otherwise Different. Based on the inner anticipation")]
    public static TrackerRunner.GuessChange BetSamePersistence2(TrackerRunner.GuessChange innerAnticipation, Func<Tracker, Tracker, bool> windowStrategy)
    {
        AnticipationStrategies.TryGetLifecycle(innerAnticipation, out var innerCycle);
        
        BetPersistenceState state = new BetPersistenceState();
        state.innerAnticipation = new InnerAnticipation()
        {
            InnerStrategy = innerAnticipation,
            InnerAnticipateDelegate = TrackerRunner._MakeAnticipateDelegate(innerAnticipation),
            AnticipationLifecycle = innerCycle
            
        };

        TrackerRunner.GuessChange guess =
            (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
            {
                if (!state.innerAnticipation.Workers.TryGetValue(t, out var workerT)) throw new Exception("Worker not found");
                
                state.innerAnticipation.InnerAnticipateDelegate(null, workerT, currentFlip);
                return state.guessChange;
            };

        bool once = true;
        
        RegisterLifecycle(guess, new AnticipationLifecycle(){ 
            Begin = (tkr) =>
            {
                state.innerAnticipation.Begin((Tracker)tkr);  
            },
            
            BatchMemberBegin = (tkr) =>
            {
                tkr.BatchMemberBegin();
                state.innerAnticipation.BatchMemberBegin((Tracker) tkr);                
            },
            
            BatchMemberEnd = (tkr) =>
            {
                tkr.BatchMemberEnd();
                state.innerAnticipation.BatchMemberEnd((Tracker) tkr);
            },
            
            WorkerMerge = (master, worker) =>
            {
                lock (master)
                {

                    master.Merge(worker);
                    state.innerAnticipation.WorkerMerge((Tracker) master, (Tracker) worker);

                }

            },
            
            PostMerge =   (master) =>
            {
                state.innerAnticipation.PostMerge((Tracker) master);
                
                if (state.Window == null)
                {
                    state.Window = new TrackerWindow((TrackerStore)master.Store,
                        UtilT.ThrowIfNull(windowStrategy, "windowStrategy"));
                }

                if (state.Window.ForwardAdd((Tracker) state.innerAnticipation.InnerMaster)) state.full = true;

                if (state.full)
                {
                    if (once) {
                        Console.Error.WriteLine("BetSamePersistence Anticipation active");
                        once = false;
                    }
                    
                    // Guess same when BetSameWinRate is >= 50
                    state.guessChange = ((Tracker)state.Window.Final()).BetSameWinRate < 50.0;
                }
            }
            
        });

        return guess;
    }
    
    /// <summary>
    /// Predicts Same or Different from the BetSame win rate of a completed tracker window. It exists in it's own feedback
    /// </summary>
    [StringHelp(
        "Windowed BetSame persistence: predict Same when the completed window's BetSameWinRate is at least 50%, otherwise Different. Internal feedback")]
    public static TrackerRunner.GuessChange BetSamePersistence(Func<Tracker, Tracker, bool> windowStrategy)
    {
        BetPersistenceState state = new BetPersistenceState();

        TrackerRunner.GuessChange guess =
            (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
            {
                return state.guessChange;
            };

        bool once = true;
        
        RegisterLifecycle(guess, new AnticipationLifecycle(){ 
            PostMerge =   (tkr) =>
            {
                if (state.Window == null)
                {
                    state.Window = new TrackerWindow((TrackerStore)tkr.Store,
                        UtilT.ThrowIfNull(windowStrategy, "windowStrategy"));
                }

                if (state.Window.ForwardAdd((Tracker) tkr)) state.full = true;

                if (state.full)
                {
                    if (once) {
                        Console.Error.WriteLine("BetSamePersistence Anticipation active");
                        once = false;
                    }
                    
                    // Guess same when BetSameWinRate is >= 50
                    state.guessChange = ((Tracker)state.Window.Final()).BetSameWinRate < 50.0;
                }
            }
            
        });

        return guess;
    }

    /// <summary>
    /// Predicts Same or Different from the observed Same/Different balance
    /// of a completed tracker window.
    /// </summary>
    /// <remarks>
    /// This strategy uses the underlying transition structure of the source stream,
    /// independent of which side the anticipation strategy previously selected.
    /// A window with SamePercentage >= 50% predicts Same; otherwise it predicts Different.
    /// </remarks>
    [StringHelp(
        "Windowed Same/Different persistence: predict Same when the completed window's " +
        "SamePercentage is at least 50%, otherwise Different.")]
    public static TrackerRunner.GuessChange SamePersistence(
        Func<Tracker, Tracker, bool> windowStrategy)
    {
        BetPersistenceState state = new BetPersistenceState();

        TrackerRunner.GuessChange guess =
            (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
            {
                return state.guessChange;
            };

        bool once = true;

        RegisterLifecycle(guess, new AnticipationLifecycle
        {
            PostMerge = (tkr) =>
            {
                if (state.Window == null)
                {
                    state.Window = new TrackerWindow((TrackerStore)tkr.Store,
                        UtilT.ThrowIfNull(windowStrategy, "windowStrategy"));
                }

                if (state.Window.ForwardAdd((Tracker) tkr)) state.full = true;

                if (state.full)
                {
                    if (once)
                    {
                        Console.Error.WriteLine("SamePersistence Anticipation active");
                        once = false;
                    }

                    // Guess same when SamePercentage is >= 50
                    state.guessChange = ((Tracker)state.Window.Final()).SamePercentage < 50.0;
                }
            }
        });

        return guess;
    }

    public class AnticipationLifecycle
    {
        public Action<ITracker>? Begin;
        
        // Overrides the normal tracker batch begin behavior.
        // Implementations should invoke tracker.BatchMemberBegin()
        // if the outer tracker should retain normal accounting.
        public Action<ITracker>? BatchMemberBegin;

        // Overrides the normal tracker batch end behavior.
        public Action<ITracker>? BatchMemberEnd;

        // Overrides the normal worker -> master merge.
        public Action<ITracker, ITracker>? WorkerMerge;

        // Runs after all workers have been merged.
        public Action<ITracker>? PostMerge;
    }
    
    private static readonly ConditionalWeakTable<
        TrackerRunner.GuessChange,
        AnticipationLifecycle> _anticipationMethods = new();

    public static bool TryGetLifecycle(
        TrackerRunner.GuessChange guessChange,
        out AnticipationLifecycle? cycle)
    {
        return _anticipationMethods.TryGetValue(
            guessChange,
            out cycle);
    }

    private static void RegisterLifecycle(
        TrackerRunner.GuessChange guessChange,
        AnticipationLifecycle cycle)
    {
        _anticipationMethods.Add(
            guessChange,
            cycle);
    }
    
    public class AnticipationOption : TrackerOption
    {
        public DelegateMethodRegistry Registry { get; set; }
        public DelegateMethodRegistry.RegistryParseResult? RegistryParseResult { get; set; }
        
        public AnticipationOption(DelegateMethodRegistry random_sources) : base("-anticipation")
        {
            Registry = new DelegateMethodRegistry(typeof(TrackerRunner.GuessChange), "anticipation");
            Registry.AddTypeHandler(random_sources);
        }
        
        public TrackerRunner.GuessChange? Strategy => RegistryParseResult?.Strategy as TrackerRunner.GuessChange;
        
        /// <summary>
        /// Scans TrackerWindow for static methods with the correct attributes and loads them into the registry.
        /// </summary>
        public AnticipationOption AddDefaults()
        {
            Registry.AddFromHostType(typeof(AnticipationStrategies));
            Registry.Strategies["ClassicMetaGuess"].IsDefault = true;
            
            DelegateMethodRegistry Registry2 = new DelegateMethodRegistry(typeof(Func<Tracker, Tracker, bool>), "window method");
            
            Registry.TypeHandlers[typeof(Func<Tracker, Tracker, bool>)] = Registry2;
            Registry2.AddFromHostType(typeof(TrackerWindow));
            Registry2.Strategies["WindowByTotal"].IsDefault = true;
            
            return this;
        }

        /// <summary>
        /// Attempts to consume the -window flag and its arguments.
        /// </summary>
        public override bool TryParse(List<string> command_args, int index, ref int status, SOut message, SOut errorMessage)
        {
            if (!base.TryParse(command_args, index, ref status, message, errorMessage))
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
            return $"{NameString()}Disabled (Using default Anticipation processing)\n";
        }
    }
}