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
        [KV_FA(FluentAttribute.Help, "Creates a bounding function that defines a window based on a maximum number of total flips.")]
        public static TrackerWindow ByTotal(
            [KV_FA(FluentAttribute.Def, "100B")]
            [KV_FA(FluentAttribute.Help, "The maximum allowed difference in total flips between the head and tail of the window.")]
            Count length)
        {
            return new TrackerWindow((A, B) => (A.total - B.total) <= length);
        }

        [FluentMethod("by_heads")]
        [KV_FA(FluentAttribute.Help, "Creates a bounding function that defines a window based on a maximum number of 'heads' flips.")]
        public static TrackerWindow ByHeads(
            [KV_FA(FluentAttribute.Def, "100B")]
            [KV_FA(FluentAttribute.Help, "The maximum allowed difference in heads between the head and tail of the window.")]
            Count length)
        {
            return new TrackerWindow((A, B) => (A.heads - B.heads) <= length);
        }

        [FluentMethod("by_tails")]
        [KV_FA(FluentAttribute.Help, "Creates a bounding function that defines a window based on a maximum number of 'tails' flips.")]
        public static TrackerWindow ByTails(
            [KV_FA(FluentAttribute.Def, "100B")]
            [KV_FA(FluentAttribute.Help, "The maximum allowed difference in tails between the head and tail of the window.")]
            Count length)
        {
            return new TrackerWindow((A, B) => (A.tails - B.tails) <= length);
        }

        [FluentMethod("by_anticipated")]
        [KV_FA(FluentAttribute.Help, "Creates a bounding function that defines a window based on a maximum number of anticipated matches.")]
        public static TrackerWindow ByAnticipated(
            [KV_FA(FluentAttribute.Def, "100B")]
            [KV_FA(FluentAttribute.Help, "The maximum allowed difference in anticipated flips between the head and tail of the window.")]
            Count length)
        {
            return new TrackerWindow((A, B) => (A.anticipated - B.anticipated) <= length);
        }

        [FluentMethod("by_wallclock_ns")]
        [KV_FA(FluentAttribute.Help, "Creates a bounding function that defines a window based on a precise amount of nanoseconds of wallclock compute time.")]
        public static TrackerWindow ByWallclockTimeNs(
            [KV_FA(FluentAttribute.Def, "3600000000000")]
            [KV_FA(FluentAttribute.Help, "The maximum allowed difference in nanoseconds between the head and tail of the window.")]
            long length)
        {
            return new TrackerWindow((A, B) => (A.wallclockTimeNs - B.wallclockTimeNs) <= length);
        }

        [FluentMethod("by_elapsed")]
        [KV_FA(FluentAttribute.Help, "Creates a bounding function that defines a window based on a specific duration of wallclock compute time.")]
        public static TrackerWindow ByElapsed(
            [KV_FA(FluentAttribute.Def, "01:00:00")]
            [KV_FA(FluentAttribute.Help, "The TimeSpan representing the maximum allowed duration between the head and tail of the window.")]
            TimeSpan length)
        {
            return new TrackerWindow((A, B) => (A.WallclockTime - B.WallclockTime) <= length);
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
        });
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
