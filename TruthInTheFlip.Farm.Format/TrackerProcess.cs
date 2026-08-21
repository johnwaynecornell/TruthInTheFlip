using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public abstract class TrackerProcessBase
    : FarmProcess
{
    protected TrackerProcessBase()
    {

    }
}

public class TrackerProcess : TrackerProcessBase
{
    protected TrackerSelector Tracker { get; }
    
    public TrackerProcess(TrackerSelector tracker) : base()
    {
        Tracker = tracker;
    }

    protected override IEnumerable<object> EnumerateItems(
        FarmContext context)
    {
        using TrackerStream source = Tracker.Source();
        foreach (object item in EnumerateItems(context, source))
        {
            yield return item;
        }
    }

    protected virtual IEnumerable<Tracker> EnumerateItems(FarmContext context, TrackerStream source)
    {
        foreach (ITracker tracker in source.Records)
        {
            yield return (Tracker)tracker;
        }
    }

    public override Type StatType { get => typeof(Tracker); }
    
}