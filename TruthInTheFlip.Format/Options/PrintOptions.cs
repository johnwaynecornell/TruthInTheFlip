using System;
using System.Collections.Generic;

namespace TruthInTheFlip.Format.Options;

public class PrintOption : TrackerOption
{
    public DelegateMethodRegistry Registry { get; set; }
    public DelegateMethodRegistry.RegistryParseResult? RegistryParseResult { get; set; }

    public PrintOption() : base("-print")
    {
        Registry = new DelegateMethodRegistry(typeof(TrackerStore.PrintDelegate), "print format");
    }

    public TrackerStore.PrintDelegate? Strategy => RegistryParseResult?.Strategy as TrackerStore.PrintDelegate;

    public PrintOption AddDefaults()
    {
        Registry.AddFromHostType(typeof(PrintOption));
        Registry.Strategies["Default"].IsDefault = true;
        return this;
    }

    // --- Helper for clean TimeSpans ---
    private static string FormatSpan(TimeSpan ts)
    {
        // Drops the fractional seconds. e.g., 1.12:30:15 instead of 1.12:30:15.1234567
        return ts.ToString(ts.Days > 0 ? @"d\.hh\:mm\:ss" : @"hh\:mm\:ss");
    }

    // --- Formats ---

    [StringHelp("The standard telemetry output with smoothed FPS.")]
    [Versioning("TruthInTheFlip.v1.1.0")]
    public static TrackerStore.PrintDelegate Default()
    {
        return (ITrackerStore store, ITracker tracker) =>
        {
            Tracker t = (Tracker)tracker;
            string projectText = "";

            if (t.wallclockTimeNs != 0)
            {
                var proof1 = t.EstimateRemainingFlipsForZScore(1.96);
                var proof2 = t.EstimateRemainingFlipsForZScore(3.00);
                
                // Using Wallclock for smooth FPS
                double durationSeconds = Math.Max(0.001, t.Source.wallclockTimeNs / 1_000_000_000.0); 
                double fps = t.Source.total / durationSeconds; // Lifetime FPS based on wallclock

                TimeSpan ts1 = new TimeSpan((long)((proof1 / fps) * TimeSpan.TicksPerSecond));
                TimeSpan ts2 = new TimeSpan((long)((proof2 / fps) * TimeSpan.TicksPerSecond));

                string project1 = proof1 == -1 ? "unreachable" : $"in {FormatSpan(ts1)}";
                string project2 = proof2 == -1 ? "unreachable" : $"in {FormatSpan(ts2)}";

                projectText = $" | fps: {fps:F0} | Z(1.96) {project1} | Z(3.00) {project2}";
            }

            TimeSpan wallclockSpan = new TimeSpan(t.Source.wallclockTimeNs / 100);

            return $"{t.Source.total} flips → " +
                   $"a: {Tracker.FormatOffset(t.AnticipatedPercentage, "0.0e+00")} | " +
                   $"Z: {Tracker.FormatWithPlus(t.ZScore, "F4")} | " +
                   $"[RATE] H/T: {Tracker.FormatOffset(t.BetHeadsWinRate, "0.0e+00")}/ {Tracker.FormatOffset(t.BetTailsWinRate, "0.0e+00")} " +
                   $"S/D: {Tracker.FormatOffset(t.BetSameWinRate, "0.0e+00")}/ {Tracker.FormatOffset(t.BetDiffWinRate, "0.0e+00")} | " +
                   $"wallclock: {FormatSpan(wallclockSpan)}" +
                   projectText;
        };
    }

    
    
    [StringHelp("Detailed telemetry including underlying PRNG drift (ZHeads).")]
    [Versioning("TruthInTheFlip.v1.1.0")]
    public static TrackerStore.PrintDelegate Detailed()
    {
        return (ITrackerStore store, ITracker tracker) =>
        {
            Tracker t = (Tracker)tracker;
            string projectText = "";

            if (t.wallclockTimeNs != 0)
            {
                var proof1 = t.EstimateRemainingFlipsForZScore(1.96);
                var proof2 = t.EstimateRemainingFlipsForZScore(3.00);
                
                // Using Wallclock for smooth FPS
                double durationSeconds = Math.Max(0.001, t.Source.wallclockTimeNs / 1_000_000_000.0); 
                double fps = t.Source.total / durationSeconds; // Lifetime FPS based on wallclock

                TimeSpan ts1 = new TimeSpan((long)((proof1 / fps) * TimeSpan.TicksPerSecond));
                TimeSpan ts2 = new TimeSpan((long)((proof2 / fps) * TimeSpan.TicksPerSecond));

                string project1 = proof1 == -1 ? "unreachable" : $"in {FormatSpan(ts1)}";
                string project2 = proof2 == -1 ? "unreachable" : $"in {FormatSpan(ts2)}";

                projectText = $" | fps: {fps:F0} | Z(1.96) {project1} | Z(3.00) {project2}";
            }

            TimeSpan wallclockSpan = new TimeSpan(t.Source.wallclockTimeNs / 100);

            return $"{t.Source.total} flips → " +
                   $"heads: {Tracker.FormatOffset(t.HeadsPercentage, "0.0e+00")} (Z:{Tracker.FormatWithPlus(t.ZScoreHeads, "F4")}) | " +
                   $"a: {Tracker.FormatOffset(t.AnticipatedPercentage, "0.0e+00")} (Z:{Tracker.FormatWithPlus(t.ZScore, "F4")}) | " +
                   $"aHeads: {Tracker.FormatOffset(t.AnticipatedHeadsPercentage, "0.0e+00")} | " +
                   $"aTails: {Tracker.FormatOffset(t.AnticipatedTailsPercentage, "0.0e+00")} | " +
                   $"aSame: {Tracker.FormatOffset(t.AnticipatedSamePercentage, "0.0e+00")} | " +
                   $"aDiff: {Tracker.FormatOffset(t.AnticipatedDiffPercentage, "0.0e+00")} | " +
                   $"[RATE] H/T: {Tracker.FormatOffset(t.BetHeadsWinRate, "0.0e+00")}/ {Tracker.FormatOffset(t.BetTailsWinRate, "0.0e+00")} " +
                   $"S/D: {Tracker.FormatOffset(t.BetSameWinRate, "0.0e+00")}/ {Tracker.FormatOffset(t.BetDiffWinRate, "0.0e+00")} | " +
                   $"wallclock: {FormatSpan(wallclockSpan)}" +
                   projectText;
        };
        
    }

    [StringHelp("A highly condensed output prioritizing minimal screen real estate.")]
    [Versioning("TruthInTheFlip.v1.0.1")]
    public static TrackerStore.PrintDelegate Minimal()
    {
        return (ITrackerStore store, ITracker tracker) =>
        {
            Tracker t = (Tracker)tracker;
            return $"{t.Source.total} flips | a: {Tracker.FormatOffset(t.AnticipatedPercentage, "0.0e+00")} | Z: {t.ZScore:F6}";
        };
    }

    // --- Option Overrides ---

    public override bool TryParse(List<string> command_args, int index, ref int status, SOut message, SOut errorMessage)
    {
        if (!base.TryParse(command_args, index, ref status, message, errorMessage)) return false;
        if (!Registry.TryParse(this, command_args, index, ref status, message, errorMessage, out var res)) return false;
        RegistryParseResult = res;
        return true;
    }

    public override string Info()
    {
        var res = UtilT.ThrowIfNull(RegistryParseResult, "RegistryParseResult");
        return Registry.Info(this, res);
    }

    public virtual string List() => Registry.List(this);
    public override string GetHelp() => Registry.GetHelp(this);
    public override string DisabledInfo() => $"{NameString()}Disabled (Using default print format)\n";
    
}