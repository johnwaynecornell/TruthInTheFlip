using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public class SegmentAggregate : StatsBase<SegmentStats>
{
    private readonly List<SegmentStats> _segments = new List<SegmentStats>();
    
    private List<double> _best => _segments.Select(s => s.BestTrueZ).OrderBy(v => v).ToList();
    private List<double> _end => _segments.Select(s => s.EndTrueZ).OrderBy(v => v).ToList();
    private List<double> _mean => _segments.Select(s => s.MeanTrueZ).OrderBy(v => v).ToList();
    private List<double> _endZH => _segments.Select(s => s.EndZHeads).OrderBy(v => v).ToList();
    private List<double> _endA => _segments.Select(s => s.EndA).OrderBy(v => v).ToList();
    
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The best TrueZ from the SegmentStats.")]
    public SegmentStats? Z  => _segments.OrderByDescending(s => s.BestTrueZ).First();
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The average best TrueZ from the SegmentStats.")]
    public double AvgBestTrueZ => _segments.Average(s => s.BestTrueZ);
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The median best TrueZ from the SegmentStats.")]
    public double MedianBestTrueZ => Median(_best);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The average end TrueZ from the SegmentStats.")]
    public double AvgEndTrueZ => _segments.Average(s => s.EndTrueZ);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The median end TrueZ from the SegmentStats.")]
    public double MedianEndTrueZ => Median(_end);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The average mean TrueZ from the SegmentStats.")]
    public double AvgMeanTrueZ => _segments.Average(s => s.MeanTrueZ);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("the average Percentage of records where anticipation was better than chance.")]
    public double AvgPctAbove50 => _segments.Average(s => s.PctAbove50);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The average mean anticipation from the SegmentStats.")]
    public double AvgMeanA => _segments.Average(s => s.MeanA);
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The average end anticipation from the SegmentStats.")]
    public double AvgEndA => _segments.Average(s => s.EndA);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The median end anticipation from the SegmentStats.")]
    public double MedianEndA => Median(_endA);
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The average percentage of anticipation above 50% from the SegmentStats.")]
    public double AvgPctAAtLeast50 => _segments.Average(s => s.PctAAtLeast50);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The average mean ZHeads from the SegmentStats.")]
    public double AvgMeanZHeads => _segments.Average(s => s.MeanZHeads);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The average end ZHeads from the SegmentStats.")]
    public double AvgEndZHeads => _segments.Average(s => s.EndZHeads);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The median end ZHeads from the SegmentStats.")]
    public double MedianEndZHeads => Median(_endZH);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The average percentage of ZHeads above zero from the SegmentStats.")]
    public double AvgPctZHeadsAbove0 => _segments.Average(s => s.PctZHeadsAbove0);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of end anticipation >= 50% from the SegmentStats.")]
    public double PctEndAAtLeast_50 => PctEndAAtLeast(50);
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of best TrueZ >= 1.96 from the SegmentStats.")]
    public double PctBestAtLeast_1_96 => PctBestAtLeast(1.96);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of end TrueZ >= 1.96 from the SegmentStats.")]
    public double PctEndAtLeast_1_96 => PctEndAtLeast(1.96);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of mean TrueZ >= 1.96 from the SegmentStats.")]
    public double PctMeanAtLeast_1_96 => PctMeanAtLeast(1.96);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of end ZHeads >= 1.96 from the SegmentStats.")]
    public double PctEndZHeadsAtLeast_1_96 => PctEndZHeadsAtLeast(1.96);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of absolute end ZHeads >= 1.96 from the SegmentStats.")]
    public double PctAbsEndZHeadsAtLeast_1_96 => PctAbsEndZHeadsAtLeast(1.96);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of best TrueZ >= 3.00 from the SegmentStats.")]
    public double PctBestAtLeast_3_00 => PctBestAtLeast(3.00);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of end TrueZ >= 3.00 from the SegmentStats.")]
    public double PctEndAtLeast_3_00 => PctEndAtLeast(3.00);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of mean TrueZ >= 3.00 from the SegmentStats.")]
    public double PctMeanAtLeast_3_00 => PctMeanAtLeast(3.00);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of end ZHeads >= 3.00 from the SegmentStats.")]
    public double PctEndZHeadsAtLeast_3_00 => PctEndZHeadsAtLeast(3.00);
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("The percent of absolute end ZHeads >= 3.00 from the SegmentStats.")]
    public double PctAbsEndZHeadsAtLeast_3_00 => PctAbsEndZHeadsAtLeast(3.00);

    
    public double PctBestAtLeast(double threshold) =>
        100.0 * _segments.Count(s => s.BestTrueZ >= threshold) / _segments.Count;

    public double PctEndAtLeast(double threshold) =>
        100.0 * _segments.Count(s => s.EndTrueZ >= threshold) / _segments.Count;

    public double PctMeanAtLeast(double threshold) =>
        100.0 * _segments.Count(s => s.MeanTrueZ >= threshold) / _segments.Count;

    public double PctEndAAtLeast(double threshold) =>
        100.0 * _segments.Count(s => s.EndA >= threshold) / _segments.Count;

    public double PctEndZHeadsAtLeast(double threshold) =>
        100.0 * _segments.Count(s => s.EndZHeads >= threshold) / _segments.Count;

    public double PctAbsEndZHeadsAtLeast(double threshold) =>
        100.0 * _segments.Count(s => Math.Abs(s.EndZHeads) >= threshold) / _segments.Count;

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return double.NaN;
        int mid = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[mid - 1] + values[mid]) / 2.0
            : values[mid];
    }

    public override void Inspect(SegmentStats stats)
    {
        _segments.Add(stats);
    }
}