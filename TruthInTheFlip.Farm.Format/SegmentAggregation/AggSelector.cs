using FluentCommandLine;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

[KV_FA(
    FluentAttribute.Help,
    "Definition for aggregating SegmentStats into larger segments")]
public class AggSelector : SelectorTemplate<SegmentStats, SegmentAggregate>
{
    public AggSelector(Func<SegmentAggregate, SegmentStats, bool> selector) : base(selector)
    {
    }
    
    public AggSelector(AggSelector source, Func<SegmentAggregate, bool> use) : base(source, use)
    {
    }
}
