using JWCEssentials.Metadata;
using FluentCommandLine;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

[KV_FA(FluentAttribute.Help, "Selector for a tracker source")]
public class TrackerSelector
{
    public Func<TrackerStream> Source { get; init; }

    public TrackerSelector(Func<TrackerStream> source)
    {
        this.Source = source;
    }
    
    public TrackerSelector(TrackerSelector source, Func<ITracker, bool> predicate)
    {
        Source = () =>
        {
            return new TrackerStream(source.Source(), predicate);
        };
    }
}
