using FluentCommandLine;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

[KV_FA(FluentAttribute.Help, "Definition for dividing tracker records into segments")]
public class SegSelector
{
    public Func<SegmentStats, Tracker, bool> Selector { get; init; }

    public SegSelector(Func<SegmentStats, Tracker, bool> selector)
    {
        this.Selector = selector;
    }
}
