using JWCEssentials.Metadata;
using FluentCommandLine;
using JWCFarm;
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
    
    public static TrackerSelector Join(params TrackerSelector[] sources)
    {
        return new TrackerSelector(() =>
        {
            if (sources.Length == 0)
                throw new FarmInputException("Join requires at least one source.");

            TrackerStream first = sources[0].Source();

            return new TrackerStream(
                first.Store,
                JoinRecords(first, sources.Skip(1)));
        });
    }
    
    private static IEnumerable<ITracker> JoinRecords(
        TrackerStream first,
        IEnumerable<TrackerSelector> remainingSources)
    {
        TrackerStore store = first.Store;
        int[] ver =
            TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version!)
            ?? throw new InvalidOperationException();

        Tracker? offset = null;

        IEnumerable<TrackerStream> GetStreams()
        {
            yield return first;
            foreach (TrackerSelector selector in remainingSources)
            {
                yield return selector.Source();
            }
        }

        foreach (TrackerStream stream in GetStreams())
        {
            using (stream)
            {
                Tracker? final = null;

                foreach (Tracker raw in stream.Records.Cast<Tracker>())
                {
                    Tracker output;

                    if (offset == null) output = raw;
                    else
                    {
                        var n = store.Clone(raw);
                        n.Merge(offset);
                        output = n;
                    }

                    final = output;
                    yield return output;
                }

                if (final != null)
                    offset = final;
            }
        }
    }
}
