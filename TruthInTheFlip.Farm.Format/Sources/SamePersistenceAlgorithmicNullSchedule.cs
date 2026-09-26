using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// An immutable step record representing one snapshot delta in an algorithmic-null schedule.
/// Preserves only experimental geometry (cadence, totals, timing, flags) from the historical tracker.
/// </summary>
public readonly record struct SamePersistenceAlgorithmicNullStep(
    long Total,
    long DeltaTotal,
    long BatchTotal,
    long WallclockTimeNs,
    long BatchWallclockTimeNs,
    long UtcBeginTimeMs,
    long UtcEndTimeMs,
    long CumulativeTicks,
    bool IsComplete
);

/// <summary>
/// In-memory materialized schedule of experiment geometry for a SamePersistence algorithmic null simulation.
/// Replays fresh fair-source bit histories under active SamePersistence strategy feedback.
/// </summary>
/// <remarks>
/// <para>
/// <b>Combinatorial Model (Exact):</b>
/// For a batch of size <c>M</c> with starting bit <c>F0</c> and transition count <c>D = M - S ~ Binomial(M, 0.5)</c>,
/// the transition positions partition the <c>M</c> flips into <c>n = D + 1</c> alternating runs of identical bits.
/// The conditional residual Heads occupancy <c>X</c> over the <c>k</c> Heads-valued run cells follows an exact
/// Negative Hypergeometric / Beta-Binomial distribution:
/// <c>X | (M, D, F0) ~ Beta-Binomial(S, alpha = k, beta = D + 1 - k)</c>,
/// where <c>k = floor(D / 2) + 1</c> if <c>F0 == 1</c> (Heads), or <c>k = floor((D + 1) / 2)</c> if <c>F0 == 0</c> (Tails).
/// </para>
/// <para>
/// <b>Large-M Sampler (Approximate):</b>
/// For mature tracker batches (<c>M = 200,000,000</c>), discrete Beta-Binomial sampling is approximated via
/// moment-matched conditional Gaussian diffusion:
/// <c>muX = S * k / (D + 1)</c>,
/// <c>varX = S * k * (D + 1 - k) / (D + 1)^2 * (M + 1) / (D + 2)</c>,
/// <c>X = clamp(round(muX + sqrt(varX) * Z), 0, S)</c>, where <c>Z ~ N(0, 1)</c>.
/// The combinatorial model and contingency identities are exact; the large-M continuous sampler is an asymptotic approximation.
/// </para>
/// <para>
/// <b>Contingency Table Coherence:</b>
/// From <c>X</c>, <c>D</c>, <c>F0</c>, and <c>FM = F0 ^ (D mod 2)</c>, the exact non-negative integer cell counts are:
/// <c>N11 = X</c>, <c>N00 = S - X</c>, <c>N01 = (D + FM - F0) / 2</c>, <c>N10 = (D - FM + F0) / 2</c>,
/// guaranteeing <c>N00 + N01 + N10 + N11 == M</c> and exact Tracker-metric coherence across all fields.
/// </para>
/// <para>
/// <b>Strategy Execution Semantics:</b>
/// Matches live <c>SamePersistence</c> execution order: batch <c>k</c> is scored under the active decision
/// (<c>predictSame = true</c> during warmup, then evaluated from the rolling 10B window); the synthetic cumulative
/// snapshot is emitted; then the 10B strategy rolling window is updated with the new snapshot to determine the decision
/// for batch <c>k+1</c>.
/// </para>
/// </remarks>
public sealed class SamePersistenceAlgorithmicNullSchedule : INullSchedule
{
    public TrackerStore Store { get; }
    public IReadOnlyList<SamePersistenceAlgorithmicNullStep> Steps { get; }
    public long StrategyWindowFlips { get; }
    public int StepCount => Steps.Count;
    public string? SourcePath => Store.Path;

    public SamePersistenceAlgorithmicNullSchedule(
        TrackerStore store,
        IReadOnlyList<SamePersistenceAlgorithmicNullStep> steps,
        long strategyWindowFlips = 10_000_000_000L)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Steps = steps ?? throw new ArgumentNullException(nameof(steps));
        StrategyWindowFlips = strategyWindowFlips > 0
            ? strategyWindowFlips
            : throw new ArgumentOutOfRangeException(nameof(strategyWindowFlips), "Strategy window flips must be positive.");
    }

    /// <summary>
    /// Materializes historical tracker stream geometry into an immutable in-memory schedule.
    /// Reads the input stream completely and disposes it.
    /// </summary>
    public static SamePersistenceAlgorithmicNullSchedule Materialize(
        TrackerSelector historicalSource,
        long strategyWindowFlips = 10_000_000_000L)
    {
        if (historicalSource == null) throw new ArgumentNullException(nameof(historicalSource));

        using TrackerStream input = historicalSource.Source();
        TrackerStore store = input.Store;

        var steps = new List<SamePersistenceAlgorithmicNullStep>();
        long prevTotal = 0;

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

            steps.Add(new SamePersistenceAlgorithmicNullStep(
                Total: hist.total,
                DeltaTotal: deltaTotal,
                BatchTotal: hist.batchTotal,
                WallclockTimeNs: hist.wallclockTimeNs,
                BatchWallclockTimeNs: hist.batchWallclockTimeNs,
                UtcBeginTimeMs: hist.utcBeginTimeMs,
                UtcEndTimeMs: hist.utcEndTimeMs,
                CumulativeTicks: hist.cumulativeTicks,
                IsComplete: hist.IsComplete
            ));

            prevTotal = hist.total;
        }

        return new SamePersistenceAlgorithmicNullSchedule(store, steps, strategyWindowFlips);
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
    /// Replays the in-memory schedule with a deterministic sampler, generating fresh fair-source outcomes
    /// and active SamePersistence decisions to yield synthetic accumulated Tracker records.
    /// </summary>
    public IEnumerable<ITracker> Replay(IBinomialSampler sampler)
    {
        if (sampler == null) throw new ArgumentNullException(nameof(sampler));

        long cumulativeTotal = 0;
        long cumulativeHeads = 0;
        long cumulativeTails = 0;
        long cumulativeAnticipated = 0;
        long cumulativeBaseAnticipated = 0;
        long cumulativeAnticipatedHeads = 0;
        long cumulativeAnticipatedTails = 0;
        long cumulativeBetHeads = 0;
        long cumulativeBetSame = 0;
        long cumulativeAnticipatedSame = 0;

        bool priorBit = false;
        bool predictSame = true;

        var strategyWindow = new TrackerWindow(Store, (A, B) => (A.total - B.total) <= StrategyWindowFlips);
        bool windowFull = false;

        for (int i = 0; i < Steps.Count; i++)
        {
            SamePersistenceAlgorithmicNullStep step = Steps[i];
            long M = step.DeltaTotal;

            if (M < 0)
            {
                throw new InvalidOperationException($"DeltaTotal cannot be negative ({M}).");
            }

            long D;
            long S;
            bool endBit;
            int f0 = priorBit ? 1 : 0;
            int fm;
            long X;
            long n01;
            long n10;
            long n11;
            long n00;
            long H;
            long T;

            if (M == 0)
            {
                D = 0;
                S = 0;
                endBit = priorBit;
                fm = f0;
                X = 0;
                n01 = 0;
                n10 = 0;
                n11 = 0;
                n00 = 0;
                H = 0;
                T = 0;
            }
            else
            {
                // 1. Sample D ~ Binomial(M, 0.5), S = M - D
                D = sampler.Sample(M, 0.5);
                S = M - D;

                // 2. Boundary bit F0 is priorBit
                // 3. Compute FM = F0 XOR (D mod 2)
                endBit = priorBit ^ ((D & 1) == 1);
                fm = endBit ? 1 : 0;

                // 4. n = D + 1, k = number of Heads-valued run cells
                long k = (f0 == 1) ? (D / 2) + 1 : (D + 1) / 2;

                // 5. Conditional residual Heads occupancy X | (M, D, F0) ~ Beta-Binomial(S, k, D + 1 - k)
                if (S <= 0)
                {
                    X = 0;
                }
                else if (D <= 0)
                {
                    X = (f0 == 1) ? S : 0;
                }
                else
                {
                    double muX = (double)S * k / (D + 1);
                    double varX = (double)S * k * (D + 1 - k) / ((double)(D + 1) * (D + 1)) * ((double)(M + 1) / (D + 2));
                    double sigmaX = Math.Sqrt(Math.Max(0.0, varX));

                    if (sigmaX < 1e-9)
                    {
                        X = (long)Math.Round(muX);
                    }
                    else
                    {
                        double z = sampler.NextGaussian();
                        X = (long)Math.Round(muX + sigmaX * z);
                    }

                    X = Math.Clamp(X, 0L, S);
                }

                // 6. Construct exact contingency cells
                n11 = X;
                n00 = S - X;
                n01 = (D + fm - f0) / 2;
                n10 = (D - fm + f0) / 2;

                // 7. Derive H, T
                H = n11 + n01;
                T = M - H;
            }

            // 8. Populate tracker deltas according to the active SamePersistence decision for this batch
            long deltaAnticipated;
            long deltaBaseAnticipated;
            long deltaBetSame;
            long deltaAnticipatedSame;
            long deltaBetHeads;
            long deltaAnticipatedHeads;
            long deltaAnticipatedTails;

            if (predictSame)
            {
                deltaAnticipated = S;
                deltaBaseAnticipated = S;
                deltaBetSame = M;
                deltaAnticipatedSame = S;

                deltaBetHeads = n10 + n11;
                deltaAnticipatedHeads = n11;
                deltaAnticipatedTails = n00;
            }
            else
            {
                deltaAnticipated = D;
                deltaBaseAnticipated = D;
                deltaBetSame = 0;
                deltaAnticipatedSame = 0;

                deltaBetHeads = n00 + n01;
                deltaAnticipatedHeads = n01;
                deltaAnticipatedTails = n10;
            }

            cumulativeTotal += M;
            cumulativeHeads += H;
            cumulativeTails += T;
            cumulativeAnticipated += deltaAnticipated;
            cumulativeBaseAnticipated += deltaBaseAnticipated;
            cumulativeAnticipatedHeads += deltaAnticipatedHeads;
            cumulativeAnticipatedTails += deltaAnticipatedTails;
            cumulativeBetHeads += deltaBetHeads;
            cumulativeBetSame += deltaBetSame;
            cumulativeAnticipatedSame += deltaAnticipatedSame;

            Tracker synthetic = (Tracker)Store.NewTracker();
            synthetic.total = cumulativeTotal;
            synthetic.heads = cumulativeHeads;
            synthetic.tails = cumulativeTails;
            synthetic.anticipated = cumulativeAnticipated;
            synthetic.baseAnticipated = cumulativeBaseAnticipated;
            synthetic.anticipatedHeads = cumulativeAnticipatedHeads;
            synthetic.anticipatedTails = cumulativeAnticipatedTails;
            synthetic.betHeads = cumulativeBetHeads;
            synthetic.betSame = cumulativeBetSame;
            synthetic.anticipatedSame = cumulativeAnticipatedSame;

            synthetic.batchTotal = step.BatchTotal;
            synthetic.wallclockTimeNs = step.WallclockTimeNs;
            synthetic.batchWallclockTimeNs = step.BatchWallclockTimeNs;
            synthetic.utcBeginTimeMs = step.UtcBeginTimeMs;
            synthetic.utcEndTimeMs = step.UtcEndTimeMs;
            synthetic.cumulativeTicks = step.CumulativeTicks;
            synthetic.IsComplete = step.IsComplete;

            synthetic.Source = synthetic;
            synthetic.From = null;

            // Advance boundary bit for next batch
            priorBit = endBit;

            // Emit snapshot
            yield return synthetic;

            // Update strategy window AFTER snapshot is emitted
            if (strategyWindow.ForwardAdd(synthetic))
            {
                windowFull = true;
            }

            if (windowFull)
            {
                Tracker windowView = strategyWindow.Final();
                predictSame = windowView.SamePercentage >= 50.0;
            }
        }
    }
}
