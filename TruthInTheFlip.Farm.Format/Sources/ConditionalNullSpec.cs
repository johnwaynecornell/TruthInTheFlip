using FluentCommandLine;
using JWCEssentials.Metadata;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Specification for a conditional-null simulation preserving historical predictor decisions
/// while replacing outcomes with an independent fair source.
/// </summary>
[KV_FA(FluentAttribute.Help, "Specification for a conditional-null simulation preserving historical predictor decisions.")]
public class ConditionalNullSpec
{
    public TrackerSelector HistoricalSource { get; }
    public Func<ulong, IBinomialSampler>? SamplerFactory { get; init; }

    public ConditionalNullSpec(TrackerSelector historicalSource)
    {
        HistoricalSource = historicalSource ?? throw new ArgumentNullException(nameof(historicalSource));
    }

    [FluentMethod("conditioned")]
    [KV_FA(FluentAttribute.Help, "Condition null simulation on historical predictor decisions from a tracker source.")]
    public static ConditionalNullSpec Conditioned(
        [KV_FA(FluentAttribute.Help, "Historical tracker source.")]
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
