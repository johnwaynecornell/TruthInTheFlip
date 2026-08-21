using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public abstract class SegmentProcess<TStats, TProduct> : TrackerProcessBase where TStats : class  where TProduct : StatsBase<TStats>, new()
{
    public SelectorTemplate<TStats, TProduct> Segmentselector { get; init; }
    
    public SegmentProcess(SelectorTemplate<TStats, TProduct> segmentselector) 
    {
        Segmentselector = segmentselector;
    }

    public abstract IEnumerable<TStats> source(FarmContext context);
    
    protected override IEnumerable<TProduct> EnumerateItems(FarmContext context)
    {
        TProduct? currentSegment = null;
        long segmentIndex = 0;
            
        foreach (TStats stats in source(context))
        {
            TStats state = (TStats)stats;
                
            if (currentSegment == null || !Segmentselector.Selector(currentSegment, state))
            {
                if (currentSegment != null)
                {
                    currentSegment.IsComplete = true;
                    currentSegment.CompletionReason = SegmentCompletionReason.BoundaryReached;
                    if (Segmentselector.Use(currentSegment))
                        yield return currentSegment;
                }
                    
                currentSegment = new TProduct();
                currentSegment.Index = segmentIndex++;
                currentSegment.BeginTotal = BeginTotal(state);
                currentSegment.BeginWallclock = BeginWallclock(state);
                currentSegment.Begin = state;
            }
                
            currentSegment!.Inspect(state);
            currentSegment.EndTotal = EndTotal(state);
            currentSegment.EndWallclock = EndWallclock(state);
            currentSegment.End = state;
        }
        
        if (currentSegment != null)
        {
            currentSegment.IsComplete = false;
            currentSegment.CompletionReason = SegmentCompletionReason.SourceExhausted;
            if (Segmentselector.Use(currentSegment))
                yield return currentSegment;
        }
    }

    public override Type StatType { get => typeof(TProduct); }
    
    public abstract long BeginTotal(TStats stats);
    public abstract long EndTotal(TStats stats);

    public abstract TimeSpan BeginWallclock(TStats stats);
    public abstract TimeSpan EndWallclock(TStats stats);
}
