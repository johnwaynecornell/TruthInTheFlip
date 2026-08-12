using JWCEssentials.Metadata;
using FluentCommandLine;

namespace TruthInTheFlip.Farm.Format;

[KV_FA(FluentAttribute.Help, "Selector for a tracker source")]
public class TrackerSelector
{
    public Func<TrackerStream> Source { get; init; }

    public TrackerSelector(Func<TrackerStream> source)
    {
        this.Source = source;
    }
}
