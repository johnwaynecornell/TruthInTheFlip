using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public class SegmentStatsProcess : SegmentProcess<Tracker, SegmentStats>
{
    public TrackerSelector TrackerSelector { get; init; }
    
    public SegmentStatsProcess(TrackerSelector trackerSelector, SegSelector segmentselector) : base(segmentselector)
    {
        TrackerSelector = trackerSelector;
    }


    public override IEnumerable<Tracker> source(FarmContext context)
    {
        return from v in TrackerSelector.Source().Records select (Tracker) v;
    }

    public override long BeginTotal(Tracker stats) => stats.absTotal;
    
    public override long EndTotal(Tracker stats) => stats.absTotal;
    
    public override TimeSpan BeginWallclock(Tracker stats) => stats.absWallclockTime;

    public override TimeSpan EndWallclock(Tracker stats) => stats.absWallclockTime;
}
