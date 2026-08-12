using JWCFarm;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public class SegmentStatsProcess : TrackerProcessBase
{
    public SegSelector Segmentselector { get; init; }
    
    public SegmentStatsProcess(TrackerSelector trackerSelector, SegSelector segmentselector) : base(trackerSelector)
    {
        Segmentselector = segmentselector;
    }
    
    protected override IEnumerable<SegmentStats> EnumerateItems(FarmContext context, TrackerStream source)
    {
        SegmentStats? currentSegment = null;
        long segmentIndex = 0;
            
        foreach (ITracker tracker in source.Records)
        {
            Tracker state = (Tracker)tracker;
                
            if (currentSegment == null || !Segmentselector.Selector(currentSegment, state))
            {
                if (currentSegment != null)
                {
                    currentSegment.IsComplete = true;
                    currentSegment.CompletionReason = SegmentCompletionReason.BoundaryReached;
                    yield return currentSegment;
                }
                    
                currentSegment = new SegmentStats();
                currentSegment.Index = segmentIndex++;
                currentSegment.BeginTotal = state.Source.total;
                currentSegment.BeginWallclock = state.Source.WallclockTime;
                currentSegment.Begin = state;
            }
                
            currentSegment!.Inspect(state);
            currentSegment.EndTotal = state.Source.total;
            currentSegment.EndWallclock = state.Source.WallclockTime;
            currentSegment.End = state;
        }
        
        if (currentSegment != null)
        {
            currentSegment.IsComplete = false;
            currentSegment.CompletionReason = SegmentCompletionReason.SourceExhausted;
            yield return currentSegment;
        }
    }

    public override Type StatType { get => typeof(SegmentStats); }
}
