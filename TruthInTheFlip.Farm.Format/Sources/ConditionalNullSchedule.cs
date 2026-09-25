using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// An immutable step record representing one historical snapshot delta in a conditional-null schedule.
/// </summary>
public readonly record struct ConditionalNullStep(
    long Total,
    long DeltaTotal,
    long DeltaBetHeads,
    long DeltaBetTails,
    long BetHeads,
    long BetSame,
    long BatchTotal,
    long WallclockTimeNs,
    long BatchWallclockTimeNs,
    long UtcBeginTimeMs,
    long UtcEndTimeMs,
    long CumulativeTicks,
    bool IsComplete
);

/// <summary>
/// In-memory materialized schedule of historical predictor orientation and snapshot cadence.
/// Enables deterministic, zero-disk-IO replay of synthetic conditional null trials across arbitrary seeds.
/// </summary>
public sealed class ConditionalNullSchedule
{
    public TrackerStore Store { get; }
    public IReadOnlyList<ConditionalNullStep> Steps { get; }
    public int StepCount => Steps.Count;
    public string? SourcePath => Store.Path;

    public ConditionalNullSchedule(TrackerStore store, IReadOnlyList<ConditionalNullStep> steps)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    /// <summary>
    /// Materializes a historical TrackerStream into an immutable in-memory schedule.
    /// Reads the input stream completely and disposes it.
    /// </summary>
    public static ConditionalNullSchedule Materialize(TrackerSelector historicalSource)
    {
        if (historicalSource == null) throw new ArgumentNullException(nameof(historicalSource));

        using TrackerStream input = historicalSource.Source();
        TrackerStore store = input.Store;

        var steps = new List<ConditionalNullStep>();

        long prevTotal = 0;
        long prevBetHeads = 0;

        foreach (ITracker item in input.Records)
        {
            if (item is not Tracker hist)
                continue;

            long deltaTotal = hist.total - prevTotal;
            if (deltaTotal < 0)
            {
                throw new InvalidOperationException(
                    $"Historical tracker total decreased from {prevTotal} to {hist.total}.");
            }

            long deltaBetHeads = hist.betHeads - prevBetHeads;
            if (deltaBetHeads < 0 || deltaBetHeads > deltaTotal)
            {
                throw new InvalidOperationException(
                    $"Historical tracker deltaBetHeads ({deltaBetHeads}) is invalid for deltaTotal ({deltaTotal}).");
            }

            long deltaBetTails = deltaTotal - deltaBetHeads;

            steps.Add(new ConditionalNullStep(
                Total: hist.total,
                DeltaTotal: deltaTotal,
                DeltaBetHeads: deltaBetHeads,
                DeltaBetTails: deltaBetTails,
                BetHeads: hist.betHeads,
                BetSame: hist.betSame,
                BatchTotal: hist.batchTotal,
                WallclockTimeNs: hist.wallclockTimeNs,
                BatchWallclockTimeNs: hist.batchWallclockTimeNs,
                UtcBeginTimeMs: hist.utcBeginTimeMs,
                UtcEndTimeMs: hist.utcEndTimeMs,
                CumulativeTicks: hist.cumulativeTicks,
                IsComplete: hist.IsComplete
            ));

            prevTotal = hist.total;
            prevBetHeads = hist.betHeads;
        }

        return new ConditionalNullSchedule(store, steps);
    }

    /// <summary>
    /// Creates a synthetic TrackerSelector replaying this schedule under the given seed.
    /// </summary>
    public TrackerSelector CreateSelector(ulong seed, Func<ulong, IBinomialSampler>? samplerFactory = null)
    {
        return new TrackerSelector(
            () => CreateStream(seed, samplerFactory),
            isAccumulated: true);
    }

    /// <summary>
    /// Creates a single synthetic TrackerStream replaying this schedule under the given seed.
    /// </summary>
    public TrackerStream CreateStream(ulong seed, Func<ulong, IBinomialSampler>? samplerFactory = null)
    {
        IBinomialSampler sampler = samplerFactory != null
            ? samplerFactory(seed)
            : new FastBinomialSampler(seed);

        return new TrackerStream(Store, Replay(sampler));
    }

    /// <summary>
    /// Replays the in-memory schedule with a deterministic binomial sampler, yielding synthetic accumulated Tracker records.
    /// </summary>
    public IEnumerable<ITracker> Replay(IBinomialSampler sampler)
    {
        if (sampler == null) throw new ArgumentNullException(nameof(sampler));

        long cumulativeHeads = 0;
        long cumulativeTails = 0;
        long cumulativeAnticipated = 0;
        long cumulativeAnticipatedHeads = 0;
        long cumulativeAnticipatedTails = 0;

        for (int i = 0; i < Steps.Count; i++)
        {
            ConditionalNullStep step = Steps[i];

            long correctHeads = sampler.Sample(step.DeltaBetHeads, 0.5);
            long correctTails = sampler.Sample(step.DeltaBetTails, 0.5);

            long deltaAnticipatedHeads = correctHeads;
            long deltaAnticipatedTails = correctTails;
            long deltaAnticipated = correctHeads + correctTails;

            long deltaHeads = correctHeads + (step.DeltaBetTails - correctTails);
            long deltaTails = step.DeltaTotal - deltaHeads;

            cumulativeHeads += deltaHeads;
            cumulativeTails += deltaTails;
            cumulativeAnticipated += deltaAnticipated;
            cumulativeAnticipatedHeads += deltaAnticipatedHeads;
            cumulativeAnticipatedTails += deltaAnticipatedTails;

            Tracker synthetic = (Tracker)Store.NewTracker();
            synthetic.total = step.Total;
            synthetic.heads = cumulativeHeads;
            synthetic.tails = cumulativeTails;
            synthetic.anticipated = cumulativeAnticipated;
            synthetic.baseAnticipated = cumulativeAnticipated;
            synthetic.anticipatedHeads = cumulativeAnticipatedHeads;
            synthetic.anticipatedTails = cumulativeAnticipatedTails;
            synthetic.betHeads = step.BetHeads;

            // Explicit Same/Different limitation:
            // betSame is retained purely for historical provenance/orientation.
            // anticipatedSame is NOT synthesized (fixed to 0) as relational transitions are not modeled.
            synthetic.betSame = step.BetSame;
            synthetic.anticipatedSame = 0;

            synthetic.batchTotal = step.BatchTotal;
            synthetic.wallclockTimeNs = step.WallclockTimeNs;
            synthetic.batchWallclockTimeNs = step.BatchWallclockTimeNs;
            synthetic.utcBeginTimeMs = step.UtcBeginTimeMs;
            synthetic.utcEndTimeMs = step.UtcEndTimeMs;
            synthetic.cumulativeTicks = step.CumulativeTicks;
            synthetic.IsComplete = step.IsComplete;

            synthetic.Source = synthetic;
            synthetic.From = null;

            yield return synthetic;
        }
    }
}
