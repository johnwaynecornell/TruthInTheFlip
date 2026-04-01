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

    public delegate bool GuessChange(bool currentFlip, bool priorFlip, Tracker t, bool lastGuess, bool currentOutcome);

    public AnticipateDelegate MakeAnticipateDelegate(GuessChange guessChange)
    {
        return (ITrackerRunner store, ITracker tracker, bool currentFlip) =>
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
                            anticipate_delegate(this, scope.run, current);
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

        return (start - DateTime.Now).TotalSeconds;
    }
}