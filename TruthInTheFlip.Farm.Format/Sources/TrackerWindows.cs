using FluentCommandLine;
using JWCEssentials.Metadata;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public class TrackerWindows
{
    [KV_FA(FluentAttribute.Help, "Definition of a rolling tracker window")]
    public class TrackerWindow
    {
        public Func<Tracker, Tracker, bool>? WindowFilter { get; set; }

        public TrackerWindow(Func<Tracker, Tracker, bool>? windowFilter)
        {
            WindowFilter = windowFilter;
        }

        [FluentMethod("by_total")]
        [KV_FA(FluentAttribute.Help, "Defines a rolling window by maximum absolute-source flip distance.")]
        public static TrackerWindow ByTotal(
            [KV_FA(FluentAttribute.Def, "100B")]
            [KV_FA(FluentAttribute.Help, "Maximum source-total distance between the window endpoints.")]
            Count length)
        {
            return new TrackerWindow((A, B) => (A.Source.total - B.Source.total) <= length);
        }

        [FluentMethod("by_heads")]
        [KV_FA(FluentAttribute.Help, "Defines a rolling window by maximum absolute-source heads distance.")]
        public static TrackerWindow ByHeads(
            [KV_FA(FluentAttribute.Def, "100B")]
            [KV_FA(FluentAttribute.Help, "Maximum source-heads distance between the window endpoints.")]
            Count length)
        {
            return new TrackerWindow((A, B) => (A.Source.heads - B.Source.heads) <= length);
        }

        [FluentMethod("by_tails")]
        [KV_FA(FluentAttribute.Help, "Defines a rolling window by maximum absolute-source tails distance.")]
        public static TrackerWindow ByTails(
            [KV_FA(FluentAttribute.Def, "100B")]
            [KV_FA(FluentAttribute.Help, "Maximum source-tails distance between the window endpoints.")]
            Count length)
        {
            return new TrackerWindow((A, B) => (A.Source.tails - B.Source.tails) <= length);
        }

        [FluentMethod("by_anticipated")]
        [KV_FA(FluentAttribute.Help, "Defines a rolling window by maximum absolute-source anticipated distance.")]
        public static TrackerWindow ByAnticipated(
            [KV_FA(FluentAttribute.Def, "100B")]
            [KV_FA(FluentAttribute.Help, "Maximum source-anticipated distance between the window endpoints.")]
            Count length)
        {
            return new TrackerWindow((A, B) => (A.Source.anticipated - B.Source.anticipated) <= length);
        }

        [FluentMethod("by_wallclock_ns")]
        [KV_FA(FluentAttribute.Help, "Defines a rolling window by absolute-source wallclock nanoseconds.")]
        public static TrackerWindow ByWallclockTimeNs(
            [KV_FA(FluentAttribute.Def, "3600000000000")]
            [KV_FA(FluentAttribute.Help, "Maximum source-wallclock nanosecond distance between the window endpoints.")]
            long length)
        {
            return new TrackerWindow((A, B) => (A.Source.wallclockTimeNs - B.Source.wallclockTimeNs) <= length);
        }

        [FluentMethod("by_elapsed")]
        [KV_FA(FluentAttribute.Help, "Defines a rolling window by absolute-source wallclock duration.")]
        public static TrackerWindow ByElapsed(
            [KV_FA(FluentAttribute.Def, "01:00:00")]
            [KV_FA(FluentAttribute.Help, "Maximum source-wallclock duration between the window endpoints.")]
            TimeSpan length)
        {
            return new TrackerWindow((A, B) => (A.Source.WallclockTime - B.Source.WallclockTime) <= length);
        }
    }
    
    [FluentMethod("window")]
    [KV_FA(FluentAttribute.Help, "Apply a rolling window to a tracker source.")]
    public static TrackerSelector Window(
        [KV_FA(FluentAttribute.Help, "Window definition to apply.")]
        TrackerWindow bounds,
        [KV_FA(FluentAttribute.Help, "Tracker source to window.")]
        TrackerSelector source)
    {
        return new TrackerSelector(() =>
        {
            TrackerStream input = source.Source();

            var window = new TruthInTheFlip.Format.TrackerWindow(
                input.Store,
                UtilT.ThrowIfNull(
                    bounds.WindowFilter,
                    "bounds.WindowFilter"));

            IEnumerable<ITracker> records =
                ApplyWindow(window, input.Records);

            return new TrackerStream(input.Store, records);
        }, isAccumulated: false);
    }

    private static IEnumerable<ITracker> ApplyWindow(
        TruthInTheFlip.Format.TrackerWindow window,
        IEnumerable<ITracker> source)
    {
        foreach (ITracker tracker in source)
        {
            yield return window.Add((Tracker)tracker);
        }
    }
}
