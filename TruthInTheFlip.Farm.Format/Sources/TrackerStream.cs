using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public sealed class TrackerStream : IDisposable
{
    public TrackerStore Store { get; }
    public IEnumerable<ITracker> Records { get; }

    public TrackerStream(
        TrackerStore store,
        IEnumerable<ITracker> records)
    {
        Store = store;
        Records = records;
    }
    
    public TrackerStream(TrackerStream source, Func<ITracker, bool> filter)
    {
        this.Store = source.Store;
        // The deferred execution remains perfectly intact!
        this.Records = source.Records.Where(filter);
    }

    public void Dispose()
    {
        if (Records is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
