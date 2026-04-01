namespace TruthInTheFlip.Format;

public static class AnticipationStrategies
{
    /* At the moment, there is no CLI for this, but it's begging for one :-) will be Attributed */
    
    /// <summary>
    /// The Classic TruthInTheFlip Baseline.
    /// Anticipates that if the last two flips were the SAME, the next will be DIFFERENT.
    /// If the last two flips were DIFFERENT, the next will be the SAME.
    /// </summary>
    public static bool ClassicMetaGuess(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        return currentFlip == priorFlip; 
    }
    
    // <summary>
    /// The Alternator (SDSD).
    /// Reverses the anticipation logic every single flip, regardless of the outcome.
    /// If we guessed "Same" last time, we guess "Different" this time.
    /// </summary>
    public static bool AlternatingMetaGuess(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        return !t.guessAnticipateChange; 
    }
    
    /// <summary>
    /// The Streak Clinger.
    /// Always anticipates that the sequence will stay the same (bets on long runs).
    /// </summary>
    public static bool AlwaysGuessSame(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        return false; 
    }

    /// <summary>
    /// The Chaos Agent.
    /// Always anticipates that the sequence will flip (bets against any runs).
    /// </summary>
    public static bool AlwaysGuessDifferent(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        return true; 
    }

    /// <summary>
    /// The Stubborn Heads Guesser.
    /// Never anticipates a change if the last flip was Heads.
    /// Always anticipates a change if the last flip was Tails.
    /// Effectively guesses "Heads" 100% of the time.
    /// </summary>
    public static bool AlwaysGuessHeads(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        return priorFlip; // If it was True (Heads), don't change. If it was False (Tails), change it.
    }

    /// <summary>
    /// The Stubborn Tails Guesser.
    /// Effectively guesses "Tails" 100% of the time.
    /// </summary>
    public static bool AlwaysGuessTails(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        return !priorFlip; 
    }

    /// <summary>
    /// The Alternator (Odd/Even).
    /// Always anticipates that the next flip will be different from the current one.
    /// Bets on the sequence: H, T, H, T, H, T...
    /// </summary>
    public static bool AlwaysAnticipateChange(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        return true; 
    }

    /// <summary>
    /// The Clinger.
    /// Always anticipates that the next flip will be exactly the same as the current one.
    /// Bets on long runs: H, H, H, H, H...
    /// </summary>
    public static bool NeverAnticipateChange(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        return false; 
    }

    /// <summary>
    /// The Gambler's Fallacy.
    /// If we just lost a bet, strongly anticipate that the *opposite* of our last guess will happen.
    /// If we won, keep doing what we did.
    /// </summary>
    public static bool ChaseTheLoss(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        if (!currentOutcome) // We lost
        {
            // If our last guess was to change, now guess to stay the same.
            return !lastGuess;
        }
        return lastGuess; // We won, stick to the plan.
    }

    /// <summary>
    /// The Reversal (Anti-Classic).
    /// Anticipates that if the last two flips were the SAME, the next will also be the SAME.
    /// If the last two flips were DIFFERENT, the next will be DIFFERENT.
    /// </summary>
    public static bool AntiMetaGuess(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome)
    {
        return currentFlip != priorFlip;
    }
}