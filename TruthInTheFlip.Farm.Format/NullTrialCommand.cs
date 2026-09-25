using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Fluent command for executing a single deterministic conditional-null trial.
/// </summary>
public class NullTrialCommand
{
    [FluentMethod("null_trial")]
    [KV_FA(FluentAttribute.Help, "Run a single deterministic conditional-null experiment trial.")]
    public static FarmCommand NullTrial(
        [KV_FA(FluentAttribute.Help, "Deterministic random seed for the trial.")]
        ulong seed,
        [KV_FA(FluentAttribute.Help, "Conditional null specification with historical tracker source.")]
        ConditionalNullSpec condition,
        [KV_FA(FluentAttribute.Help, "Rolling window bounds.")]
        TrackerWindows.TrackerWindow window,
        [KV_FA(FluentAttribute.Help, "Segmentation definition.")]
        SegSelector segmentation)
    {
        var syntheticSelector = condition.CreateSyntheticSelector(seed);
        var windowedSelector = TrackerWindows.Window(window, syntheticSelector);
        var process = new SegmentStatsProcess(windowedSelector, segmentation);

        return new FarmDelegateCommand((ctx) =>
        {
            List<SegmentStats> segments = new List<SegmentStats>();
            process.Actions.Process = (c, record) =>
            {
                segments.Add((SegmentStats)record);
            };

            process.Execute(ctx);

            if (segments.Count == 0)
            {
                ctx.ErrorOutput.WriteLine("No segments were produced by this null trial configuration.");
                return;
            }

            long recordCount = segments.Sum(s => s.Count);

            SegmentAggregate agg = new SegmentAggregate();
            foreach (var segment in segments)
            {
                agg.Inspect(segment);
            }

            string sourceIdentity = "in-memory";
            try
            {
                using var peekStream = condition.HistoricalSource.Source();
                if (!string.IsNullOrEmpty(peekStream.Store.Path))
                {
                    sourceIdentity = peekStream.Store.Path;
                }
            }
            catch
            {
            }

            ctx.Output.WriteLine("=== Null Trial ===");
            ctx.Output.WriteLine($"Seed                  : {seed}");
            ctx.Output.WriteLine($"Source Tracker        : {sourceIdentity}");
            ctx.Output.WriteLine($"Synthetic Records     : {recordCount:N0}");
            ctx.Output.WriteLine($"Segments              : {segments.Count:N0}");
            ctx.Output.WriteLine();
            ctx.Output.WriteLine($"Edge Excursion Score  : {Tracker.FormatWithPlus(agg.EdgeExcursionScore, "F6")}   // median(best TrueZ per segment)");
            ctx.Output.WriteLine($"Edge Settlement Score : {Tracker.FormatWithPlus(agg.EdgeSettlementScore, "F6")}   // mean(end TrueZ per segment)");
            ctx.Output.WriteLine($"Edge Persistence Index: {Tracker.FormatWithPlus(agg.EdgePersistenceIndex, "F6")}   // settlement * fraction positive");
            ctx.Output.WriteLine();
            ctx.Output.WriteLine($"avgMeanA              : {Tracker.FormatOffset(agg.AvgMeanA, "0.00000e+00")}");
            ctx.Output.WriteLine($"avgEndA               : {Tracker.FormatOffset(agg.AvgEndA, "0.00000e+00")}");
            ctx.Output.WriteLine($"avgMeanZHeads         : {Tracker.FormatWithPlus(agg.AvgMeanZHeads, "F6")}");
            ctx.Output.WriteLine($"avgEndZHeads          : {Tracker.FormatWithPlus(agg.AvgEndZHeads, "F6")}");
            ctx.Output.WriteLine();
            ctx.Output.WriteLine($"Retained Anticipation : {Tracker.FormatOffset(agg.RetainedAnticipation, "0.00000e+00")}");
            ctx.Output.WriteLine($"Settlement Adjusted A : {Tracker.FormatOffset(agg.SettlementAdjustedAnticipation, "0.00000e+00")}");
            ctx.Output.WriteLine("==================");
        });
    }
}
