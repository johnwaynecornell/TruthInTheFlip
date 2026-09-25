using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Specification for a conditional-null simulation preserving historical predictor decisions
/// while replacing outcomes with an independent fair source.
/// </summary>
/// <remarks>
/// <para>
/// <b>Conditional Null Semantics:</b>
/// Given the exact historical predictor decisions (<c>betHeads</c> and <c>betTails = total - betHeads</c>)
/// recorded by the strategy at each snapshot step, this generator samples the joint distribution of source
/// outcomes (<c>heads</c>, <c>tails</c>) and anticipation correctness (<c>anticipated</c>, <c>anticipatedHeads</c>,
/// <c>anticipatedTails</c>) under the null hypothesis that the source bits are i.i.d. fair (<c>p = 0.5</c>).
/// </para>
/// <para>
/// <b>Joint Heads / Anticipation Reconstruction:</b>
/// For each step increment:
/// <list type="bullet">
/// <item><description><c>correctHeads ~ Binomial(deltaBetHeads, 0.5)</c></description></item>
/// <item><description><c>correctTails ~ Binomial(deltaBetTails, 0.5)</c></description></item>
/// <item><description><c>deltaAnticipated = correctHeads + correctTails</c></description></item>
/// <item><description><c>deltaHeads = correctHeads + (deltaBetTails - correctTails)</c></description></item>
/// </list>
/// This rigorously preserves the exact covariance structure between <c>ZScoreHeads</c> and <c>ZScore</c> across all
/// windowing, segmentation, and summary metrics (<c>TrueZ</c>, <c>EdgeExcursionScore</c>, <c>EdgeSettlementScore</c>, etc.).
/// </para>
/// <para>
/// <b>Same/Different Limitations:</b>
/// The synthetic tracker reproduces only the Heads/Tails and Anticipation contingency table.
/// Relational Same/Different state (<c>S_t = 1[F_t == F_{t-1}]</c>) is NOT synthesized under this batch model:
/// <c>betSame</c> is copied strictly as historical provenance / orientation record, while <c>anticipatedSame</c>
/// is fixed at 0. Consequently, Same/Different-derived metrics (<c>same</c>, <c>diff</c>, <c>anticipatedSame</c>,
/// <c>anticipatedDiff</c>, <c>BetSameWinRate</c>, <c>BetDiffWinRate</c>, <c>ZScoreSame</c>, <c>ZScoreDiff</c>)
/// on synthetic records are NOT statistically meaningful and must not be consumed for null inference.
/// Core <c>null_trial</c> and segment aggregate metrics do not consume these fields.
/// </para>
/// </remarks>
[KV_FA(FluentAttribute.Help, "Specification for a conditional-null simulation preserving historical predictor decisions.")]
public class ConditionalNullSpec
{
    public TrackerSelector HistoricalSource { get; }
    public Func<ulong, IBinomialSampler>? SamplerFactory { get; init; }

    public ConditionalNullSpec(TrackerSelector historicalSource)
    {
        HistoricalSource = historicalSource ?? throw new ArgumentNullException(nameof(historicalSource));

        if (!HistoricalSource.IsAccumulated)
        {
            throw new FarmInputException(
                "conditioned requires an accumulated historical tracker source. " +
                "Condition the null simulation on the accumulated source before applying window; " +
                "an already-windowed or interval-relative source cannot be used as the conditioning source.");
        }
    }

    [FluentMethod("conditioned")]
    [KV_FA(FluentAttribute.Help, "Condition null simulation on historical predictor decisions from an accumulated tracker source.")]
    public static ConditionalNullSpec Conditioned(
        [KV_FA(FluentAttribute.Help, "Historical accumulated tracker source.")]
        TrackerSelector source)
    {
        return new ConditionalNullSpec(source);
    }

    /// <summary>
    /// Creates a synthetic TrackerSelector for a single deterministic null trial under the given seed.
    /// </summary>
    public TrackerSelector CreateSyntheticSelector(ulong seed)
    {
        return new TrackerSelector(
            () => CreateSyntheticStream(seed),
            isAccumulated: true);
    }

    /// <summary>
    /// Creates a single synthetic TrackerStream for a deterministic null trial under the given seed.
    /// </summary>
    public TrackerStream CreateSyntheticStream(ulong seed)
    {
        TrackerStream input = HistoricalSource.Source();
        IBinomialSampler sampler = SamplerFactory != null
            ? SamplerFactory(seed)
            : new FastBinomialSampler(seed);

        return new TrackerStream(
            input.Store,
            GenerateSyntheticRecords(input, sampler));
    }

    private static IEnumerable<ITracker> GenerateSyntheticRecords(
        TrackerStream input,
        IBinomialSampler sampler)
    {
        using (input)
        {
            TrackerStore store = input.Store;
            long prevTotal = 0;
            long prevBetHeads = 0;

            long cumulativeHeads = 0;
            long cumulativeTails = 0;
            long cumulativeAnticipated = 0;
            long cumulativeAnticipatedHeads = 0;
            long cumulativeAnticipatedTails = 0;

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

                long correctHeads = sampler.Sample(deltaBetHeads, 0.5);
                long correctTails = sampler.Sample(deltaBetTails, 0.5);

                long deltaAnticipatedHeads = correctHeads;
                long deltaAnticipatedTails = correctTails;
                long deltaAnticipated = correctHeads + correctTails;

                long deltaHeads = correctHeads + (deltaBetTails - correctTails);
                long deltaTails = deltaTotal - deltaHeads;

                cumulativeHeads += deltaHeads;
                cumulativeTails += deltaTails;
                cumulativeAnticipated += deltaAnticipated;
                cumulativeAnticipatedHeads += deltaAnticipatedHeads;
                cumulativeAnticipatedTails += deltaAnticipatedTails;

                prevTotal = hist.total;
                prevBetHeads = hist.betHeads;

                Tracker synthetic = store.Clone(hist);
                synthetic.total = hist.total;
                synthetic.heads = cumulativeHeads;
                synthetic.tails = cumulativeTails;
                synthetic.anticipated = cumulativeAnticipated;
                synthetic.baseAnticipated = cumulativeAnticipated;
                synthetic.anticipatedHeads = cumulativeAnticipatedHeads;
                synthetic.anticipatedTails = cumulativeAnticipatedTails;
                synthetic.betHeads = hist.betHeads;
                
                // Explicit Same/Different limitation:
                // betSame is retained purely for historical provenance/orientation.
                // anticipatedSame is NOT synthesized (fixed to 0) as relational transitions are not modeled.
                synthetic.betSame = hist.betSame;
                synthetic.anticipatedSame = 0;
                
                synthetic.Source = synthetic;
                synthetic.From = null;
                synthetic.IsComplete = hist.IsComplete;

                yield return synthetic;
            }
        }
    }
}
