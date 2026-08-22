using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public abstract class StatsBase<TStats> : MetricFunctionsAggregate where TStats : class
{
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Sequential index of the segment.")]
    public long Index;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Total flips at the start of the segment.")]
    public long BeginTotal;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Total flips at the end of the segment.")]
    public long EndTotal;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Wallclock time at the start of the segment.")]
    public TimeSpan BeginWallclock;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Wallclock time at the end of the segment.")]
    public TimeSpan EndWallclock;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Beginning record of the segment.")]
    public TStats? Begin;
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Ending record of the segment.")]
    public TStats? End;
    
    public SegmentCompletionReason CompletionReason { get; set; } = SegmentCompletionReason.Pending;
    public bool IsComplete { get; set; } = false;

    public abstract void Inspect(TStats stats);
    
}