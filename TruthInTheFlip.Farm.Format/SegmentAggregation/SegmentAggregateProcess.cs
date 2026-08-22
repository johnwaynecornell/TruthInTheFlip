
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public class SegmentAggregateProcess
    : SegmentProcess<SegmentStats, SegmentAggregate>
{
    public SegmentStatsProcess SegmentStatsProcess { get; init; }

    public override FarmProcess? InputProcess => SegmentStatsProcess;

    public SegmentAggregateProcess(TrackerSelector tracker, SegSelector segmentselector, AggSelector aggSelector) : base(aggSelector)
    {
        SegmentStatsProcess = new SegmentStatsProcess(tracker, segmentselector);
        SegmentStatsProcess.Projection = new MetricProjection();

    }

    public override IEnumerable<SegmentStats> source(FarmContext context)
    {
        List<SegmentStats> stats = new();
        SegmentStatsProcess.Actions.Process = (context, o) => stats.Add((SegmentStats) o);
        SegmentStatsProcess.Execute(context);
        return stats;
    }

    public override long BeginTotal(SegmentStats stats) => stats.Begin.absTotal;

    public override long EndTotal(SegmentStats stats) => stats.End.absTotal;

    public override TimeSpan BeginWallclock(SegmentStats stats) => stats.Begin.absWallclockTime;

    public override TimeSpan EndWallclock(SegmentStats stats) => stats.End.absWallclockTime;
}
