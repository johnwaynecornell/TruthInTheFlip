using JWCFarm;
using TruthInTheFlip.Farm.Format;

namespace TruthInTheFlip.Farm.Tests;

public class TrackerJoinTests
{
    [Fact]
    public void JoinRejectsWindowedTrackerSources()
    {
        var accumulated = new TrackerSelector(
            () => throw new InvalidOperationException(
                "The guard should run before opening the source."));

        var windowed = TrackerWindows.Window(
            TrackerWindows.TrackerWindow.ByTotal(new Count(100)),
            accumulated);

        var joined = TrackerSelector.Join(
            accumulated,
            windowed);

        FarmInputException error =
            Assert.Throws<FarmInputException>(
                () => joined.Source());

        Assert.Contains(
            "only accumulated tracker sources",
            error.Message);
    }
    
    [Fact]
    public void WindowProducesIntervalRelativeSelector()
    {
        var source = new TrackerSelector(
            () => throw new Exception());

        var windowed = TrackerWindows.Window(
            TrackerWindows.TrackerWindow.ByTotal(new Count(100)),
            source);

        Assert.True(source.IsAccumulated);
        Assert.False(windowed.IsAccumulated);
    }
}