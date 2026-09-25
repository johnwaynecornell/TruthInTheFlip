using JWCEssentials.Metadata;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Strongly-typed results for one deterministic conditional-null trial.
/// Exposes metrics via [IsMetric] for reflection in Farm processes (csv, json, pretty).
/// </summary>
public sealed class NullTrialStats : MetricFunctions
{
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("0-based index of this trial in the population.")]
    public int TrialIndex { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Deterministic 64-bit random seed used for this trial.")]
    public ulong Seed { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Total number of segments evaluated in this trial.")]
    public int SegmentCount { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Edge Excursion Score: median of best TrueZ per segment.")]
    public double EdgeExcursionScore { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Edge Settlement Score: mean of end TrueZ per segment.")]
    public double EdgeSettlementScore { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Edge Persistence Index: settlement * fraction positive.")]
    public double EdgePersistenceIndex { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average of best TrueZ across segments.")]
    public double AvgBestTrueZ { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Median of best TrueZ across segments.")]
    public double MedianBestTrueZ { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average of end TrueZ across segments.")]
    public double AvgEndTrueZ { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Median of end TrueZ across segments.")]
    public double MedianEndTrueZ { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average of mean TrueZ across segments.")]
    public double AvgMeanTrueZ { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average of mean anticipation percentage across segments.")]
    public double AvgMeanA { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average of end anticipation percentage across segments.")]
    public double AvgEndA { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average percentage of records where anticipation >= 50%.")]
    public double AvgPctAAtLeast50 { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average of mean ZHeads across segments.")]
    public double AvgMeanZHeads { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average of end ZHeads across segments.")]
    public double AvgEndZHeads { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Retained Anticipation: weighted anticipation mean by fraction of time above 50%.")]
    public double RetainedAnticipation { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Settlement Adjusted Anticipation: anticipation mean weighted by positive settlement.")]
    public double SettlementAdjustedAnticipation { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Percent of segments with best TrueZ >= 1.96.")]
    public double PctBestAtLeast_1_96 { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Percent of segments with best TrueZ >= 3.00.")]
    public double PctBestAtLeast_3_00 { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Percent of segments with end TrueZ >= 0.00.")]
    public double PctEndAtLeast_0 { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Percent of segments with end TrueZ >= 1.96.")]
    public double PctEndAtLeast_1_96 { get; init; }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Percent of segments with mean TrueZ >= 0.00.")]
    public double PctMeanAtLeast_0 { get; init; }

    /// <summary>
    /// Creates a NullTrialStats instance by projecting metrics from a evaluated SegmentAggregate.
    /// </summary>
    public static NullTrialStats FromAggregate(int trialIndex, ulong seed, SegmentAggregate agg, int segmentCount)
    {
        return new NullTrialStats
        {
            TrialIndex = trialIndex,
            Seed = seed,
            SegmentCount = segmentCount,
            EdgeExcursionScore = agg.EdgeExcursionScore,
            EdgeSettlementScore = agg.EdgeSettlementScore,
            EdgePersistenceIndex = agg.EdgePersistenceIndex,
            AvgBestTrueZ = agg.AvgBestTrueZ,
            MedianBestTrueZ = agg.MedianBestTrueZ,
            AvgEndTrueZ = agg.AvgEndTrueZ,
            MedianEndTrueZ = agg.MedianEndTrueZ,
            AvgMeanTrueZ = agg.AvgMeanTrueZ,
            AvgMeanA = agg.AvgMeanA,
            AvgEndA = agg.AvgEndA,
            AvgPctAAtLeast50 = agg.AvgPctAAtLeast50,
            AvgMeanZHeads = agg.AvgMeanZHeads,
            AvgEndZHeads = agg.AvgEndZHeads,
            RetainedAnticipation = agg.RetainedAnticipation,
            SettlementAdjustedAnticipation = agg.SettlementAdjustedAnticipation,
            PctBestAtLeast_1_96 = agg.PctBestAtLeast_1_96,
            PctBestAtLeast_3_00 = agg.PctBestAtLeast_3_00,
            PctEndAtLeast_0 = agg.PctEndAtLeast_0,
            PctEndAtLeast_1_96 = agg.PctEndAtLeast_1_96,
            PctMeanAtLeast_0 = agg.PctMeanAtLeast_0
        };
    }
}
