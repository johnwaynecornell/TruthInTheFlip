using FluentCommandLine;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

[KV_FA(FluentAttribute.Help, "Definition for dividing tracker records into segments")]
public class SegSelector
{
    //answer the question does this Tracker fit in this segment
    public Func<SegmentStats, Tracker, bool> Selector { get; init; }

    //answer the question do we use this segment
    public Func<SegmentStats, bool> Use { get; init; }
    public SegSelector(Func<SegmentStats, Tracker, bool> selector)
    {
        this.Selector = selector;
        this.Use = _ => true;
    }
    
    public SegSelector(SegSelector source, Func<SegmentStats, bool> use)
    {
        this.Selector = source.Selector;
        this.Use = use;
    }
}
