using System.Globalization;
using FluentCommandLine;
using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public class SegmentStatsReport
{
    [FluentMethod("segment_report")]
    [KV_FA(FluentAttribute.Help, "Report on tracker records as segments.")]
    public static FarmCommand SegmentReport(
        [KV_FA(FluentAttribute.Help, "The grade of the report.")]
        GradeArgument grade,
        [KV_FA(FluentAttribute.Help, "Tracker source to process.")]
        TrackerSelector tracker,
        [KV_FA(FluentAttribute.Help, "Method used to divide tracker records into segments.")]
        SegSelector segmentation)
    {

        var res = FluentEnvironment.Current.CurrentParseResult;
        string info = res.Registry.Info(FluentEnvironment.Current, res);

        var process = new SegmentStatsProcess(
            tracker,
            segmentation);

        return new FarmDelegateCommand((ctx) =>
        {
            //collect the statistics
            List<SegmentStats> segments = new List<SegmentStats>();
            process.Actions.Process = (ctx, record) => { segments.Add((SegmentStats)record); };

            process.Execute(ctx);
            
            if (segments.Count == 0)
            {
                ctx.ErrorOutput.WriteLine(
                    "There are no segments matching this report configuration.");
                return;
            }
            
            SegmentStatsReport report = new SegmentStatsReport();
            report.message = (s, nl) => { ctx.Output.Write(s); if (nl) ctx.Output.WriteLine(); };
            report.errorMessage = (s, nl) => { ctx.ErrorOutput.Write(s); if (nl) ctx.ErrorOutput.WriteLine(); };
            
            report.Report(grade.Grade, segments, info);

        });
    }

    public SOut errorMessage = (s, n) => throw new ArgumentException("errorMessage delegate must be assigned before use");
    public SOut message = (s, n) => throw new ArgumentException("message delegate must be assigned before use");
    
    public void Report(Grade grade, List<SegmentStats> segments, string info)
    {
        SegmentAggregate agg = new SegmentAggregate(segments);

        message("=== Command Configuration ===");
        message(info);
        message("=============================");
        message();
        message("=== Segment Report ===");
        
        double edgeExcursionScore = agg.MedianBestTrueZ;
        double edgeSettlementScore = agg.AvgEndTrueZ;
        double edgePersistenceIndex = agg.AvgEndTrueZ * (agg.AvgPctAbove50 / 100.0);

        if (grade >= Grade.Low)
        {
            message($"segments              : {segments.Count:N0}");
            message($"Edge Excursion Score  : {Fmt(edgeExcursionScore)}   // median(best TrueZ per segment)");
            message($"Edge Settlement Score : {Fmt(edgeSettlementScore)}   // mean(end TrueZ per segment)");
            message($"Edge Persistence Index: {Fmt(edgePersistenceIndex)}   // settlement * fraction positive");
            message();
        }

        if (grade >= Grade.Med)
        {
            message("Adjusted Anticipation Geometry:");
            message($"avgBestTrueZ          : {Fmt(agg.AvgBestTrueZ)}");
            message($"medianBestTrueZ       : {Fmt(agg.MedianBestTrueZ)}");
            message($"avgEndTrueZ           : {Fmt(agg.AvgEndTrueZ)}");
            message($"medianEndTrueZ        : {Fmt(agg.MedianEndTrueZ)}");
            message($"avgMeanTrueZ          : {Fmt(agg.AvgMeanTrueZ)}");
            message();
            message($"bestTrueZ >= 1.96     : {agg.PctBestAtLeast(1.96),8:0.0000}%");
            message($"bestTrueZ >= 3.00     : {agg.PctBestAtLeast(3.00),8:0.0000}%");
            message($"endTrueZ  >= 0.00     : {agg.PctEndAtLeast(0.00),8:0.0000}%");
            message($"endTrueZ  >= 1.96     : {agg.PctEndAtLeast(1.96),8:0.0000}%");
            message($"meanTrueZ >= 0.00     : {agg.PctMeanAtLeast(0.00),8:0.0000}%");
            message();

            message("Anticipation Path:");
            message($"avgMeanA               : {Tracker.FormatOffset(agg.AvgMeanA, "0.00000e+00")}");
            message($"avgEndA                : {Tracker.FormatOffset(agg.AvgEndA, "0.00000e+00")}");
            message($"medianEndA             : {Tracker.FormatOffset(agg.MedianEndA, "0.00000e+00")}");
            message($"avgPctAAtLeast50       : {agg.AvgPctAAtLeast50,8:0.0000}%");
            message($"endA >= 50.00          : {agg.PctEndAAtLeast(50.0),8:0.0000}%");
            message();

            message("Underlying Heads:");
            message($"avgMeanZHeads          : {Fmt(agg.AvgMeanZHeads)}");
            message($"avgEndZHeads           : {Fmt(agg.AvgEndZHeads)}");
            message($"medianEndZHeads        : {Fmt(agg.MedianEndZHeads)}");
            message($"avgPctZHeadsAbove0     : {agg.AvgPctZHeadsAbove0,8:0.0000}%");
            message($"endZHeads >= 0.00      : {agg.PctEndZHeadsAtLeast(0.00),8:0.0000}%");
            message($"|endZHeads| >= 1.96    : {agg.PctAbsEndZHeadsAtLeast(1.96),8:0.0000}%");
            message();
            
            if (grade >= Grade.All)
            {
                double corrMean =
                    PearsonCorrelation(segments.Select(s => s.MeanZHeads), segments.Select(s => s.MeanTrueZ));
                double corrEnd =
                    PearsonCorrelation(segments.Select(s => s.EndZHeads), segments.Select(s => s.EndTrueZ));

                double corrMeanAMeanZH =
                    PearsonCorrelation(segments.Select(s => s.MeanA), segments.Select(s => s.MeanZHeads));
                double corrEndAEndZH =
                    PearsonCorrelation(segments.Select(s => s.EndA), segments.Select(s => s.EndZHeads));
                double corrMeanAMeanTZ =
                    PearsonCorrelation(segments.Select(s => s.MeanA), segments.Select(s => s.MeanTrueZ));
                double corrEndAEndTZ =
                    PearsonCorrelation(segments.Select(s => s.EndA), segments.Select(s => s.EndTrueZ));

                message($"corrMeanZHeadsMeanTrueZ: {Fmt(corrMean)}");
                message($"corrEndZHeadsEndTrueZ  : {Fmt(corrEnd)}");
                message($"corrMeanAMeanZHeads    : {Fmt(corrMeanAMeanZH)}");
                message($"corrEndAEndZHeads      : {Fmt(corrEndAEndZH)}");
                message($"corrMeanAMeanTrueZ     : {Fmt(corrMeanAMeanTZ)}");
                message($"corrEndAEndTrueZ       : {Fmt(corrEndAEndTZ)}");
                message();
            }
        }

        if (grade >= Grade.High)
        {
            message(
                "idx | span                  | bestTrueZ | aAtBestZ            | endTrueZ  | meanTrueZ | endA             | meanA            | endZH     | meanZH    | %a>=50  | %ZH>=0");
            message(
                "----+-----------------------+-----------+------------------+-----------+-----------+------------------+------------------+-----------+-----------+---------+---------");

            foreach (SegmentStats s in segments)
            {
                string span = $"{s.BeginTotal:N0}..{s.EndTotal:N0}";

                message(
                    $"{s.Index,3} | " +
                    $"{Trim(span, 21),-21} | " +
                    $"{Fmt(s.BestTrueZ),9} | " +
                    $"{Tracker.FormatOffset(s.Z?.AnticipatedPercentage ?? 50.0, "0.00000e+00"),16} | " +
                    $"{Fmt(s.EndTrueZ),9} | " +
                    $"{Fmt(s.MeanTrueZ),9} | " +
                    $"{Tracker.FormatOffset(s.EndA, "0.00000e+00"),16} | " +
                    $"{Tracker.FormatOffset(s.MeanA, "0.00000e+00"),16} | " +
                    $"{Fmt(s.EndZHeads),9} | " +
                    $"{Fmt(s.MeanZHeads),9} | " +
                    $"{s.PctAAtLeast50,7:0.000}% | " +
                    $"{s.PctZHeadsAbove0,7:0.000}%"
                );
            }

            message();
        }

        if (grade >= Grade.All)
        {
            //RetainedAnticipation = Σ(segmentMeanA * pctAbove50Fraction) / Σ(pctAbove50Fraction)
            //SettlementAdjustedAnticipation = mean(segmentMeanA * clampPositive(segmentEndTrueZ))
            double sumPctAbove50Fraction = 0;
            double sumRetainedAnticipation = 0;

            double sumSegmentEndTrueZ = 0;
            double sumSettlementAdjustedAnticipation = 0;

            foreach (SegmentStats s in segments)
            {
                sumPctAbove50Fraction += s.PctAbove50;
                sumRetainedAnticipation += s.MeanA * s.PctAbove50;

                double segmentEndTrueZ = double.Max(0, s.EndTrueZ);
                sumSegmentEndTrueZ += segmentEndTrueZ;
                sumSettlementAdjustedAnticipation += s.MeanA * segmentEndTrueZ;
            }

            double retainedAnticipation =
                sumPctAbove50Fraction == 0 ? double.NaN : sumRetainedAnticipation / sumPctAbove50Fraction;
            message($"Retained Anticipation: {Tracker.FormatOffset(retainedAnticipation, "0.00000e+00")}");

            double settlementAdjustedAnticipation = sumSegmentEndTrueZ == 0
                ? double.NaN
                : sumSettlementAdjustedAnticipation / sumSegmentEndTrueZ;
            message(
                $"Settlement Adjusted Anticipation: {Tracker.FormatOffset(settlementAdjustedAnticipation, "0.00000e+00")}");

            message();

            SegmentStats? bestExcursion = segments.OrderByDescending(s => s.BestTrueZ).FirstOrDefault();
            SegmentStats? bestSettlement = segments.OrderByDescending(s => s.EndTrueZ).FirstOrDefault();
            SegmentStats? worstSettlement = segments.OrderBy(s => s.EndTrueZ).FirstOrDefault();

            DumpSegment("Best excursion segment", bestExcursion);
            DumpSegment("Best settlement segment", bestSettlement);
            DumpSegment("Worst settlement segment", worstSettlement);
        }

        message($"File compute time: {segments[segments.Count-1].End.absWallclockTime}");
        message("=============================");
    }

    void DumpSegment(string label, SegmentStats? s)
    {
        if (s == null) return;
        message(label + ":");
        message($"  idx         : {s.Index}");
        message();

        message("  TrueZ:");
        message($"    bestTrueZ   : {Fmt(s.BestTrueZ)}");
        message($"    meanTrueZ   : {Fmt(s.MeanTrueZ)}");
        message($"    endTrueZ    : {Fmt(s.EndTrueZ)}");
        message();

        message("  Anticipation:");
        if (s.Z != null)
        {
            message(
                $"    aAtTrueZ    : {Tracker.FormatOffset(s.Z.AnticipatedPercentage, "0.00000e+00")}");
        }

        message($"    mean a      : {Tracker.FormatOffset(s.MeanA, "0.00000e+00")}");
        message($"    min a       : {Tracker.FormatOffset(s.MinA, "0.00000e+00")}");
        message($"    max a       : {Tracker.FormatOffset(s.MaxA, "0.00000e+00")}");
        message($"    end a       : {Tracker.FormatOffset(s.EndA, "0.00000e+00")}");
        message($"    pct a >= 50 : {s.PctAAtLeast50:0.0000}%");
        message();

        message("  Heads:");
        if (s.Z != null)
        {
            message($"    ZH at best TrueZ : {Fmt(s.Z.ZScoreHeads)}");
        }

        message($"    mean ZHeads : {Fmt(s.MeanZHeads)}");
        message($"    min ZHeads  : {Fmt(s.MinZHeads)}");
        message($"    max ZHeads  : {Fmt(s.MaxZHeads)}");

        if (s.End != null)
        {
            message($"    end ZHeads  : {Fmt(s.End.ZScoreHeads)}");
        }

        message($"    pct ZH >= 0 : {s.PctZHeadsAbove0:0.0000}%");
        message();
    }
    
    static string Fmt(double value) => Tracker.FormatWithPlus(value, "F6");

    static string Trim(string s, int max)
    {
        if (s.Length <= max) return s;
        return s[..(max - 1)] + "…";
    }

    static double PearsonCorrelation(
        IEnumerable<double> xs,
        IEnumerable<double> ys)
    {
        var xList = xs.ToList();
        var yList = ys.ToList();
        if (xList.Count < 2 || xList.Count != yList.Count) return double.NaN;

        double avgX = xList.Average();
        double avgY = yList.Average();

        double sumSqX = 0;
        double sumSqY = 0;
        double sumCo = 0;

        for (int i = 0; i < xList.Count; i++)
        {
            double dx = xList[i] - avgX;
            double dy = yList[i] - avgY;
            sumSqX += dx * dx;
            sumSqY += dy * dy;
            sumCo += dx * dy;
        }

        if (sumSqX <= 0 || sumSqY <= 0) return double.NaN;

        return sumCo / Math.Sqrt(sumSqX * sumSqY);
    }
    
    public enum Grade
    {
        None,
        Low,
        Med,
        High,
        All
    }
    
    [KV_FA(FluentAttribute.Help, "Report detail level")]
    public record GradeArgument(Grade Grade);

    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Show only report metadata.")]
    public static GradeArgument None()
    {
        return new(Grade.None);
    }

    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Show edge statistics.")]
    public static GradeArgument Low()
    {
        return new(Grade.Low);
    }

    [FluentMethod]
    [KV_FA(FluentAttribute.Help,
        "Show Low plus anticipation and heads geometry.")]
    public static GradeArgument Med()
    {
        return new(Grade.Med);
    }

    [FluentMethod]
    [KV_FA(FluentAttribute.Help,
        "Show Med plus segment details.")]
    public static GradeArgument High()
    {
        return new(Grade.High);
    }

    [FluentMethod]
    [KV_FA(FluentAttribute.Help,
        "Show High plus Pearson correlations, additional segment statistics, and standout segments.")]
    public static GradeArgument All()
    {
        return new(Grade.All);
    }
    
    sealed class SegmentAggregate
    {
        private readonly List<SegmentStats> _segments;
        private readonly List<double> _best;
        private readonly List<double> _end;
        private readonly List<double> _mean;
        private readonly List<double> _endZH;
        private readonly List<double> _endA;

        public SegmentAggregate(List<SegmentStats> segments)
        {
            _segments = segments;
            _best = segments.Select(s => s.BestTrueZ).OrderBy(v => v).ToList();
            _end = segments.Select(s => s.EndTrueZ).OrderBy(v => v).ToList();
            _mean = segments.Select(s => s.MeanTrueZ).OrderBy(v => v).ToList();
            _endZH = segments.Select(s => s.EndZHeads).OrderBy(v => v).ToList();
            _endA = segments.Select(s => s.EndA).OrderBy(v => v).ToList();
        }

        public double AvgBestTrueZ => _segments.Average(s => s.BestTrueZ);
        public double MedianBestTrueZ => Median(_best);

        public double AvgEndTrueZ => _segments.Average(s => s.EndTrueZ);
        public double MedianEndTrueZ => Median(_end);

        public double AvgMeanTrueZ => _segments.Average(s => s.MeanTrueZ);
        public double AvgPctAbove50 => _segments.Average(s => s.PctAbove50);

        public double AvgMeanA => _segments.Average(s => s.MeanA);
        public double AvgEndA => _segments.Average(s => s.EndA);
        public double MedianEndA => Median(_endA);
        public double AvgPctAAtLeast50 => _segments.Average(s => s.PctAAtLeast50);

        public double AvgMeanZHeads => _segments.Average(s => s.MeanZHeads);
        public double AvgEndZHeads => _segments.Average(s => s.EndZHeads);
        public double MedianEndZHeads => Median(_endZH);
        public double AvgPctZHeadsAbove0 => _segments.Average(s => s.PctZHeadsAbove0);

        public double PctBestAtLeast(double threshold) =>
            100.0 * _segments.Count(s => s.BestTrueZ >= threshold) / _segments.Count;

        public double PctEndAtLeast(double threshold) =>
            100.0 * _segments.Count(s => s.EndTrueZ >= threshold) / _segments.Count;

        public double PctMeanAtLeast(double threshold) =>
            100.0 * _segments.Count(s => s.MeanTrueZ >= threshold) / _segments.Count;

        public double PctEndAAtLeast(double threshold) =>
            100.0 * _segments.Count(s => s.EndA >= threshold) / _segments.Count;

        public double PctEndZHeadsAtLeast(double threshold) =>
            100.0 * _segments.Count(s => s.EndZHeads >= threshold) / _segments.Count;

        public double PctAbsEndZHeadsAtLeast(double threshold) =>
            100.0 * _segments.Count(s => Math.Abs(s.EndZHeads) >= threshold) / _segments.Count;

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return double.NaN;
            int mid = values.Count / 2;
            return values.Count % 2 == 0
                ? (values[mid - 1] + values[mid]) / 2.0
                : values[mid];
        }
    }
}
