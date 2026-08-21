using FluentCommandLine;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

[KV_FA(FluentAttribute.Help, "Definition for dividing tracker records into segments")]
public class SegSelector : SelectorTemplate<Tracker, SegmentStats>
{
    public SegSelector(Func<SegmentStats, Tracker, bool> selector) : base(selector)
    {
    }
    
    public SegSelector(SegSelector source, Func<SegmentStats, bool> use) : base(source, use)
    {
    }
}
