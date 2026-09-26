using System.Globalization;
using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Fluent command for executing a multi-trial null experiment and generating
/// an empirical distribution comparison report against the real observed tracker metrics.
/// </summary>
public class NullReportCommand
{
    [FluentMethod("null_report")]
    [KV_FA(FluentAttribute.Help, "Compare real observed tracker statistics against an empirical null trial population.")]
    public static FarmCommand NullReport(
        [KV_FA(FluentAttribute.Help, "Number of null trials to simulate.")]
        int trialCount,
        [KV_FA(FluentAttribute.Help, "Base 64-bit random seed.")]
        ulong baseSeed,
        [KV_FA(FluentAttribute.Help, "Null specification with historical tracker source.")]
        INullTrialSpec condition,
        [KV_FA(FluentAttribute.Help, "Rolling window bounds.")]
        TrackerWindows.TrackerWindow window,
        [KV_FA(FluentAttribute.Help, "Segmentation definition.")]
        SegSelector segmentation)
    {
        if (trialCount <= 0) throw new ArgumentOutOfRangeException(nameof(trialCount), "Trial count must be greater than zero.");

        return new FarmDelegateCommand((ctx) =>
        {
            // 1. Evaluate real observed tracker statistics through identical window & segmentation
            var windowedRealSource = TrackerWindows.Window(window, condition.HistoricalSource);
            var realProcess = new SegmentStatsProcess(windowedRealSource, segmentation);

            List<SegmentStats> realSegments = new List<SegmentStats>();
            realProcess.Actions.Process = (c, record) =>
            {
                realSegments.Add((SegmentStats)record);
            };

            realProcess.Execute(ctx);

            if (realSegments.Count == 0)
            {
                ctx.ErrorOutput.WriteLine("No segments were produced by the historical source under this window and segmentation.");
                return;
            }

            long realRecordCount = realSegments.Sum(s => s.Count);
            SegmentAggregate realAgg = new SegmentAggregate();
            foreach (var s in realSegments)
            {
                realAgg.Inspect(s);
            }

            // 2. Materialize schedule once and evaluate N null trials
            var schedule = condition.MaterializeSchedule();
            var trials = new List<NullTrialStats>(trialCount);

            for (int i = 0; i < trialCount; i++)
            {
                ulong trialSeed = NullTrialProcess.DeriveTrialSeed(baseSeed, i);
                var (agg, segCount, _) = NullTrialCommand.RunTrial(
                    schedule,
                    trialSeed,
                    window,
                    segmentation,
                    ctx,
                    condition.SamplerFactory);

                trials.Add(NullTrialStats.FromAggregate(i, trialSeed, agg, segCount));
            }

            // 3. Extract metric populations and compute empirical distribution summaries
            var summaries = new List<(EmpiricalMetricSummary Summary, bool IsOffset)>
            {
                (EmpiricalMetricSummary.Compute("EdgeExcursionScore", realAgg.EdgeExcursionScore, trials.Select(t => t.EdgeExcursionScore).ToList()), false),
                (EmpiricalMetricSummary.Compute("EdgeSettlementScore", realAgg.EdgeSettlementScore, trials.Select(t => t.EdgeSettlementScore).ToList()), false),
                (EmpiricalMetricSummary.Compute("EdgePersistenceIndex", realAgg.EdgePersistenceIndex, trials.Select(t => t.EdgePersistenceIndex).ToList()), false),
                (EmpiricalMetricSummary.Compute("RetainedAnticipation", realAgg.RetainedAnticipation, trials.Select(t => t.RetainedAnticipation).ToList()), true),
                (EmpiricalMetricSummary.Compute("SettlementAdjustedAnticipation", realAgg.SettlementAdjustedAnticipation, trials.Select(t => t.SettlementAdjustedAnticipation).ToList()), true),
                (EmpiricalMetricSummary.Compute("AvgMeanA", realAgg.AvgMeanA, trials.Select(t => t.AvgMeanA).ToList()), true),
                (EmpiricalMetricSummary.Compute("AvgEndA", realAgg.AvgEndA, trials.Select(t => t.AvgEndA).ToList()), true),
                (EmpiricalMetricSummary.Compute("AvgMeanZHeads", realAgg.AvgMeanZHeads, trials.Select(t => t.AvgMeanZHeads).ToList()), false),
                (EmpiricalMetricSummary.Compute("AvgEndZHeads", realAgg.AvgEndZHeads, trials.Select(t => t.AvgEndZHeads).ToList()), false),
            };

            string sourceIdentity = schedule.SourcePath ?? "in-memory";

            // 4. Render output
            ctx.Output.WriteLine("=== Null Distribution Report ===");
            ctx.Output.WriteLine($"Historical Source     : {sourceIdentity}");
            ctx.Output.WriteLine($"Trials                : {trialCount:N0}");
            ctx.Output.WriteLine($"Base Seed             : {baseSeed}");
            ctx.Output.WriteLine($"Segments per Trial    : {realSegments.Count:N0}");
            ctx.Output.WriteLine($"Records Evaluated     : {realRecordCount:N0}");
            ctx.Output.WriteLine();
            ctx.Output.WriteLine("Empirical Percentile  : 100 * count(null <= observed) / N");
            ctx.Output.WriteLine("Sample Corrected Tails: (count + 1) / (N + 1)");
            ctx.Output.WriteLine();
            ctx.Output.WriteLine("----------------------------------------------------------------------------------------------------------------------------------");
            ctx.Output.WriteLine($"{"Metric",-30} {"Observed",-17} {"Null Mean",-17} {"Null Median",-17} {"Null [5%, 95%]",-37} {"Percentile",10} {"Lower Tail",12} {"Upper Tail",12}");
            ctx.Output.WriteLine("----------------------------------------------------------------------------------------------------------------------------------");

            foreach (var (s, isOffset) in summaries)
            {
                string obsStr = FormatValue(s.Observed, isOffset);
                string meanStr = FormatValue(s.NullMean, isOffset);
                string medStr = FormatValue(s.NullMedian, isOffset);
                string p05Str = FormatValue(s.NullP05, isOffset);
                string p95Str = FormatValue(s.NullP95, isOffset);
                string rangeStr = $"[{p05Str}, {p95Str}]";
                string pctStr = $"{s.Percentile,8:F1}%";
                string lowerStr = $"{s.LowerTail,12:F4}";
                string upperStr = $"{s.UpperTail,12:F4}";

                ctx.Output.WriteLine($"{s.MetricName,-30} {obsStr,-17} {meanStr,-17} {medStr,-17} {rangeStr,-37} {pctStr,10} {lowerStr,12} {upperStr,12}");
            }

            ctx.Output.WriteLine("================================");
        });
    }

    private static string FormatValue(double val, bool isOffset)
    {
        if (double.IsNaN(val)) return "NaN";
        if (isOffset)
            return Tracker.FormatOffset(val, "0.00000e+00");
        return Tracker.FormatWithPlus(val, "F6");
    }
}
