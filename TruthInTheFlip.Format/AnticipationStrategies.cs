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
    
    
    sealed class BetPersistenceState
    {
        public TrackerWindow? Window { get; set; }
        public bool full = false;
        public bool guessChange = false;
    }

    /// <summary>
    /// Predicts Same or Different from the BetSame win rate of a completed tracker window.
    /// </summary>
    [StringHelp(
        "Windowed BetSame persistence: predict Same when the completed window's BetSameWinRate is at least 50%, otherwise Different.")]
    public static TrackerRunner.GuessChange BetSamePersistence(Func<Tracker, Tracker, bool> windowStrategy)
    {
        BetPersistenceState state = new BetPersistenceState();

        TrackerRunner.GuessChange guess =
            (bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome) =>
            {
                return state.guessChange;
            };

        bool once = true;
        
        RegisterUpdate(guess, (tkr) =>
        {
            if (state.Window == null)
            {
                state.Window = new TrackerWindow((TrackerStore)tkr.Store,
                    UtilT.ThrowIfNull(windowStrategy, "windowStrategy"));
            }

            if (state.Window.ForwardAdd(tkr)) state.full = true;

            if (state.full)
            {
                if (once) {
                    Console.Error.WriteLine("BetSamePersistence Anticipation active");
                    once = false;
                }
                
                // Guess sane when BetSameWinRate is >= 50
                state.guessChange = ((Tracker)state.Window.Final()).BetSameWinRate < 50.0;
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
        
        RegisterUpdate(guess, (tkr) =>
        {
            if (state.Window == null)
            {
                state.Window = new TrackerWindow((TrackerStore)tkr.Store,
                    UtilT.ThrowIfNull(windowStrategy, "windowStrategy"));
            }

            if (state.Window.ForwardAdd(tkr)) state.full = true;

            if (state.full)
            {
                if (once) {
                    Console.Error.WriteLine("SamePersistence Anticipation active");
                    once = false;
                }
                
                // Guess sane when SamePercentage is >= 50
                state.guessChange = ((Tracker)state.Window.Final()).SamePercentage < 50.0;
            }
        });

        return guess;
    }
    
    private static readonly ConditionalWeakTable<
        TrackerRunner.GuessChange,
        Action<Tracker>> _updates = new();

    public static bool TryGetUpdate(
        TrackerRunner.GuessChange guessChange,
        out Action<Tracker>? update)
    {
        return _updates.TryGetValue(
            guessChange,
            out update);
    }

    private static void RegisterUpdate(
        TrackerRunner.GuessChange guessChange,
        Action<Tracker> update)
    {
        _updates.Add(
            guessChange,
            update);
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