using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public sealed class SegmentStats : StatsBase<Tracker>
{
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The best TrueZ from the Segment.")]
    public Tracker? Z;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Number of tracker records in the segment.")]
    public long Count;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Number of records where anticipation was better than chance.")]
    public long Good;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Number of records with anticipation percentage at least 50%.")]
    public long AAtLeast50;

    public double SumTrueZ;
    public double SumA;
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Minimum anticipation percentage in the segment.")]
    public double MinA = double.PositiveInfinity;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Maximum anticipation percentage in the segment.")]
    public double MaxA = double.NegativeInfinity;

    public double SumZHeads;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Minimum Z-Score for heads in the segment.")]
    public double MinZHeads = double.PositiveInfinity;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Maximum Z-Score for heads in the segment.")]
    public double MaxZHeads = double.NegativeInfinity;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Count of records where Z-Score for heads was at least zero.")]
    public long ZHeadsAboveZero;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Highest True Z-Score achieved in the segment.")]
    public double BestTrueZ = double.NegativeInfinity;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average True Z-Score across the segment.")]
    public double MeanTrueZ => Count == 0 ? double.NaN : SumTrueZ / Count;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average anticipation percentage across the segment.")]
    public double MeanA => Count == 0 ? double.NaN : SumA / Count;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Anticipation percentage at the end of the segment.")]
    public double EndA => End == null ? double.NaN : End.AnticipatedPercentage;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("True Z-Score at the end of the segment.")]
    public double EndTrueZ => End == null ? double.NaN : End.ZScore - Math.Abs(End.ZScoreHeads);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Percentage of records where anticipation was better than chance.")]
    public double PctAbove50 => Count == 0 ? double.NaN : (Good / (double)Count) * 100.0;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Percentage of records with anticipation percentage at least 50%.")]
    public double PctAAtLeast50 => Count == 0 ? double.NaN : AAtLeast50 / (double)Count * 100.0;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Average Z-Score for heads across the segment.")]
    public double MeanZHeads =>
        Count == 0 ? double.NaN : SumZHeads / Count;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Z-Score for heads at the end of the segment.")]
    public double EndZHeads =>
        End == null ? double.NaN : End.ZScoreHeads;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Percentage of records where Z-Score for heads was at least zero.")]
    public double PctZHeadsAbove0 =>
        Count == 0
            ? double.NaN
            : ZHeadsAboveZero / (double)Count * 100.0;
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Count of records where anticipated percentage was the same sign as the true percentage.")]
    public long AnticipatedSameSignCount { get; private set; } = 0;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Percentage of records where anticipated percentage was the same sign as the true percentage.")]
    public double PctAnticipatedSameSign =>
        Count == 0
            ? double.NaN
            : AnticipatedSameSignCount / (double)Count * 100.0;
    
    public override void Inspect(Tracker t)
    {
        double trueZ = t.ZScore - Math.Abs(t.ZScoreHeads);

        if (trueZ > BestTrueZ)
        {
            BestTrueZ = trueZ;
            Z = t;
        }

        SumTrueZ += trueZ;
        
        double a = t.AnticipatedPercentage;
        SumA += a;
        if (a < MinA) MinA = a;
        if (a > MaxA) MaxA = a;
        if (a >= 50.0) AAtLeast50++;

        double zHeads = t.ZScoreHeads;
        SumZHeads += zHeads;
        if (zHeads < MinZHeads) MinZHeads = zHeads;
        if (zHeads > MaxZHeads) MaxZHeads = zHeads;
        
        // Use >= 0 convention for ZHeads occupancy
        if (zHeads >= 0) ZHeadsAboveZero++;
        
        bool anticipatedPositive =
            t.AnticipatedPercentage >= 50.0;

        bool samePositive =
            t.SamePercentage >= 50.0;

        if (anticipatedPositive == samePositive)
            AnticipatedSameSignCount++;

        Count++;

        if ((t.anticipated << 1) >= t.total)
            Good++;
    }
}