using FluentCommandLine;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public class TrackerBoundarys
{
    [KV_FA(FluentAttribute.Help, "Boundary for selecting a position in a tracker stream")]
    public readonly record struct TrackerBoundary(
        Func<Tracker, bool> From,
        Func<Tracker, bool> To);
    
    public static IEnumerable<ITracker> ApplyFilter(IEnumerable<ITracker> source, Func<Tracker, bool> filter)
    {
        return source.Where((record) => filter((Tracker) record));
    }

    public static TrackerSelector TrackerFilter(TrackerSelector source, Func<Tracker, bool> filter)
    {
        return new TrackerSelector(() =>
        {
            TrackerStream input = source.Source();
            
            return new TrackerStream(input.Store,  ApplyFilter(input.Records, filter));
        });
    }
    
    [FluentMethod]
    [KV_FA(FluentAttribute.Help,
        "Select records from the specified tracker position.")]
    public static TrackerSelector from(
        [KV_FA(FluentAttribute.Help, "Boundary defining the starting position.")]
        TrackerBoundary boundary,
        [KV_FA(FluentAttribute.Help, "Tracker source to filter.")]
        TrackerSelector source)
    {
        return TrackerFilter(source, boundary.From);
    }
    
    [FluentMethod]
    [KV_FA(FluentAttribute.Help,
        "Select records through the specified tracker position.")]
    public static TrackerSelector to(
        [KV_FA(FluentAttribute.Help, "Boundary defining the ending position.")]
        TrackerBoundary boundary,
        [KV_FA(FluentAttribute.Help, "Tracker source to filter.")]
        TrackerSelector source)
    {
        return TrackerFilter(source, boundary.To);
    }
    
    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Define a boundary by absolute flip count.")]
    public static TrackerBoundary absTotal(
        [KV_FA(FluentAttribute.Help, "Absolute flip count used as the boundary.")]
        Count value)
    {
        return new TrackerBoundary(
            From: tracker => 
                tracker.absTotal >= value,
            To: tracker => 
                tracker.absTotal <= value);
    }
    
    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Define a boundary by absolute wall-clock duration.")]
    public static TrackerBoundary absWallclock(
        [KV_FA(FluentAttribute.Help, "Absolute wall-clock duration used as the boundary.")]
        TimeSpan value)
    {
        return new TrackerBoundary(
            From: tracker => 
                tracker.absWallclockTime >= value,
            To: tracker => 
                tracker.absWallclockTime <= value);
    }
    
    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Define a boundary by absolute wall-clock nanoseconds.")]
    public static TrackerBoundary absWallclockNs(
        [KV_FA(FluentAttribute.Help, "Absolute wall-clock nanoseconds used as the boundary.")]
        long value)
    {
        return new TrackerBoundary(
            From: tracker => 
                tracker.absWallclockTimeNs >= value,
            To: tracker => 
                tracker.absWallclockTimeNs <= value);
    }
    
    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Define a boundary by record UTC end time.")]
    public static TrackerBoundary utcEndTime(
        [KV_FA(FluentAttribute.Help, "UTC timestamp or offset timestamp used as the boundary.")]
        DateTimeOffset value)
    {
        DateTime boundary = value.UtcDateTime;

        return new TrackerBoundary(
            From: tracker => tracker.UtcEndTime >= boundary,
            To:   tracker => tracker.UtcEndTime <= boundary);
    }
    
    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Define a boundary by record UTC begin time.")]
    public static TrackerBoundary utcBeginTime(
        [KV_FA(FluentAttribute.Help, "UTC timestamp or offset timestamp used as the boundary.")]
        DateTimeOffset value)
    {
        DateTime boundary = value.UtcDateTime;

        return new TrackerBoundary(
            From: tracker => tracker.UtcBeginTime >= boundary,
            To:   tracker => tracker.UtcBeginTime <= boundary);
    }
}