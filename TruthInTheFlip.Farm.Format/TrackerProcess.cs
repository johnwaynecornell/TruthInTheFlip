using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public abstract class TrackerProcessBase
    : FarmProcess
{
    protected TrackerSelector Tracker { get; }

    protected TrackerProcessBase(TrackerSelector tracker)
    {
        Tracker = tracker;
    }

    protected sealed override IEnumerable<object> EnumerateItems(
        FarmContext context)
    {
        using TrackerStream source = Tracker.Source();
        foreach (object item in EnumerateItems(context, source))
        {
            yield return item;
        }
    }

    protected abstract IEnumerable<object> EnumerateItems(
        FarmContext context,
        TrackerStream source);
}

public class TrackerProcess : TrackerProcessBase
{
    
    public TrackerProcess(TrackerSelector tracker) : base(tracker)
    {
    }

    protected override IEnumerable<Tracker> EnumerateItems(FarmContext context, TrackerStream source)
    {
        foreach (ITracker tracker in source.Records)
        {
            yield return (Tracker)tracker;
        }
    }

    public override Type StatType { get => typeof(Tracker); }
}