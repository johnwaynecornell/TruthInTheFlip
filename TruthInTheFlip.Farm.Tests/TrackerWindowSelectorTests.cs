using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public sealed class TrackerWindowSelectorTests
{
    [Fact]
    public void AccumulatedWindowBoundsUseAbsoluteSourceCoordinates()
    {
        TrackerStore store = TrackerStore.Default(
            Path.Combine(Path.GetTempPath(), $"TruthInTheFlip_Farm_Window_{Guid.NewGuid():N}.tkr"));
        store.Version = TrackerStore.latest;

        Tracker sourceBegin = (Tracker)store.NewTracker();
        sourceBegin.total = 100;
        sourceBegin.heads = 40;
        sourceBegin.tails = 60;
        sourceBegin.anticipated = 45;
        sourceBegin.wallclockTimeNs = TimeSpan.FromMinutes(10).Ticks * 100;

        Tracker sourceEnd = (Tracker)store.NewTracker();
        sourceEnd.total = 300;
        sourceEnd.heads = 150;
        sourceEnd.tails = 150;
        sourceEnd.anticipated = 155;
        sourceEnd.wallclockTimeNs = TimeSpan.FromMinutes(30).Ticks * 100;

        Tracker relativeBegin = (Tracker)store.NewTracker();
        relativeBegin.Source = sourceBegin;
        relativeBegin.total = relativeBegin.heads = relativeBegin.tails = relativeBegin.anticipated = 5;
        relativeBegin.wallclockTimeNs = TimeSpan.FromMinutes(1).Ticks * 100;

        Tracker relativeEnd = (Tracker)store.NewTracker();
        relativeEnd.Source = sourceEnd;
        relativeEnd.total = relativeEnd.heads = relativeEnd.tails = relativeEnd.anticipated = 5;
        relativeEnd.wallclockTimeNs = TimeSpan.FromMinutes(1).Ticks * 100;

        Assert.False(TrackerWindows.TrackerWindow.ByTotal(new Count(150)).WindowFilter!(relativeEnd, relativeBegin));
        Assert.False(TrackerWindows.TrackerWindow.ByHeads(new Count(100)).WindowFilter!(relativeEnd, relativeBegin));
        Assert.True(TrackerWindows.TrackerWindow.ByTails(new Count(100)).WindowFilter!(relativeEnd, relativeBegin));
        Assert.False(TrackerWindows.TrackerWindow.ByAnticipated(new Count(100)).WindowFilter!(relativeEnd, relativeBegin));
        Assert.False(TrackerWindows.TrackerWindow.ByWallclockTimeNs(TimeSpan.FromMinutes(15).Ticks * 100).WindowFilter!(relativeEnd, relativeBegin));
        Assert.False(TrackerWindows.TrackerWindow.ByElapsed(TimeSpan.FromMinutes(15)).WindowFilter!(relativeEnd, relativeBegin));

        var engine = new TruthInTheFlip.Format.TrackerWindow(store, (_, _) => true);
        Tracker emitted = engine.Add(relativeEnd);

        Assert.Same(sourceEnd, emitted.Source);
        Assert.Equal(300, emitted.absTotal);
    }
}
