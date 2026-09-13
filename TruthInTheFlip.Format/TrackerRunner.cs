namespace TruthInTheFlip.Format;

public interface ITrackerRunner
{
    ITrackerStore store { get; }
    BitFactory bitFactory { get; }

    double Run(ITracker master, int threads = 20, int stride = 10000000);
}

public class TrackerRunner : ITrackerRunner
{
    public ITrackerStore store { get; }
    public BitFactory bitFactory { get; }
    
    public delegate bool AnticipateDelegate(ITrackerRunner store, ITracker tracker, bool currentFlip);
    public AnticipateDelegate? anticipate_delegate;
    public AnticipationStrategies.AnticipationLifecycle? lifecycle;

    public delegate bool GuessChange(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome);

    /// <summary>
    /// Creates an anticipation delegate that wraps custom guessing logic for processing coin flip sequences.
    /// This factory method takes a GuessChange delegate and returns a fully-configured AnticipateDelegate
    /// that handles all tracking, scoring, and state management for the meta-guessing algorithm.
    /// For predefined strategies and examples, see AnticipationStrategies.
    /// </summary>
    public static AnticipateDelegate _MakeAnticipateDelegate(GuessChange guessChange)
    {
        return (ITrackerRunner runner, ITracker tracker, bool currentFlip) =>
        {
            Tracker t = (Tracker)tracker;
            // If anticipating change, expect !priorFlip. If anticipating same, expect priorFlip.

            bool guess = t.guessAnticipateChange ? (!t.priorFlip) : t.priorFlip;
            bool guessOutcome = guess == currentFlip;

            if (guess) t.betHeads++;

            if (guessOutcome) t.baseAnticipated++;

            //if (trackerInner != null) result = trackerInner.Anticipate(result) ? !result : result;
            if (t.trackerInner != null)
                guessOutcome = t.trackerInner.Anticipate(guessOutcome) ? guessOutcome : !guessOutcome;

            if (!t.guessAnticipateChange) t.betSame++;

            if (guessOutcome)
            {
                if (!t.guessAnticipateChange) t.anticipatedSame++;
                if (currentFlip) t.anticipatedHeads++;
                else t.anticipatedTails++;

                t.anticipated++;
            }

            t.total++;

            if (currentFlip)
                t.heads++;
            else
                t.tails++;

            // Anticipate the relation, not the value.
            t.guessAnticipateChange = guessChange(currentFlip, t.priorFlip, t, guess, guessOutcome);

            t.priorFlip = currentFlip;

            return guessOutcome;
        };
    }

    public AnticipateDelegate MakeAnticipateDelegate(GuessChange guessChange)
    {
        if (AnticipationStrategies.TryGetLifecycle(
                guessChange,
                out var update))
        {
            lifecycle = update;
        }
        else
        {
            //
            // Important if the same runner is ever reconfigured.
            //
            lifecycle = null;
        }
        
        return _MakeAnticipateDelegate(guessChange);
    }


    public TrackerRunner(ITrackerStore store, BitFactory bitFactory)
    {
        this.store = store;
        this.bitFactory = bitFactory;
    }
    public class ForScope
    {
        public ITrackerStore store;
        public BitFactory bitFactory;
        public BitFactory.Consumer consume;
        public ITracker run;

        public ForScope(ITrackerStore store, BitFactory bitFactory)
        {
            this.store = store;
            this.bitFactory = bitFactory;
            consume = new BitFactory.Consumer(bitFactory);

            run = store.NewTracker();
            //initialize the tracker to get the first valid guess to be fair statistically.
            run.Anticipate(consume.getBit());
            run.Anticipate(consume.getBit());
            run.Reset(); // This deliberately does not reset the prior flip memory or guess.

        }
    }

    public virtual double Run(ITracker master, int threads = 20, int stride = 10000000)
    {
        DateTime start = DateTime.Now;
        
        master.WallclockBegin();

        if (anticipate_delegate != null)
        {
            lifecycle.Begin(master);
            
            Parallel.For(
                0, threads,
                () => new ForScope(store, bitFactory), // 1. localInit: Runs once per thread to initialize the state
                (index, loopState, scope) =>
                {
                    // 2. body: Runs for each iteration, using the thread-local state

                    if (lifecycle != null && lifecycle.BatchMemberBegin != null) lifecycle.BatchMemberBegin(scope.run);
                    else scope.run.BatchMemberBegin();

                    try
                    {
                        for (int i = 0; i < stride; i++)
                        {
                            bool current = scope.consume.getBit();
                            anticipate_delegate(this, scope.run, current);
                        }
                    }
                    finally
                    {
                        if (lifecycle != null && lifecycle.BatchMemberEnd != null) lifecycle.BatchMemberEnd(scope.run);
                        else scope.run.BatchMemberEnd();
                    }

                    return scope; // Pass the state to the next iteration on this thread
                },
                (scope) =>
                {
                    // 3. localFinally: Runs once per thread after all its iterations are done
                    lock (master)
                    {
                        if (lifecycle != null && lifecycle.WorkerMerge != null)  lifecycle.WorkerMerge(master, scope.run);
                        else master.Merge(scope.run);
                    }
                });
        }
        else
        {
            Parallel.For(
                0, threads,
                () => new ForScope(store, bitFactory), // 1. localInit: Runs once per thread to initialize the state
                (index, loopState, scope) =>
                {
                    // 2. body: Runs for each iteration, using the thread-local state

                    scope.run.BatchMemberBegin();

                    try
                    {
                        for (int i = 0; i < stride; i++)
                        {
                            bool current = scope.consume.getBit();
                            scope.run.Anticipate(current);
                        }
                    }
                    finally
                    {
                        scope.run.BatchMemberEnd();
                    }

                    return scope; // Pass the state to the next iteration on this thread
                },
                (scope) =>
                {
                    // 3. localFinally: Runs once per thread after all its iterations are done
                    lock (master) master.Merge(scope.run);
                });
            
        }

        master.WallclockEnd();

        //
        // Every worker has now been merged.
        // Publish the decision for the NEXT Run.
        //
        if (lifecycle != null && lifecycle.PostMerge != null)
        {
            lifecycle.PostMerge(
                (Tracker)master);
        }
        
        return (start - DateTime.Now).TotalSeconds;
    }
}
