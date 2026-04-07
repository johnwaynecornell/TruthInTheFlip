using TruthInTheFlip.Format.Options;

namespace TruthInTheFlip.Format;

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
    
    public class AnticipationOption : TrackerOption
    {
        public DelegateMethodRegistry<TrackerRunner.GuessChange> Registry { get; set; }
        public DelegateMethodRegistry<TrackerRunner.GuessChange>.RegistryParseResult? RegistryParseResult { get; set; }
        
        public AnticipationOption() : base("-anticipation")
        {
            Registry = new DelegateMethodRegistry<TrackerRunner.GuessChange>("anticipation");
        }
        
        public TrackerRunner.GuessChange? Strategy => RegistryParseResult?.Strategy;
        
        /// <summary>
        /// Scans TrackerWindow for static methods with the correct attributes and loads them into the registry.
        /// </summary>
        public AnticipationOption AddDefaults()
        {
            Registry.AddFromHostType(typeof(AnticipationStrategies));
            Registry.Strategies["ClassicMetaGuess"].IsDefault = true;
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