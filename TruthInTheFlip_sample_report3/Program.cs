using System.Globalization;
using TruthInTheFlip.Format;
using TruthInTheFlip.Format.Options;

int[] supported_ver = new[] { 1, 1, 0 };

List<string> cl_args = new(args);
bool showHelp = false;

Options O = new();
TrackerWindow.WindowOption windowOption;
InfoOption infoOption;
PrintOption printOption;

O.Add(infoOption = new InfoOption());
O.Add(windowOption = new TrackerWindow.WindowOption().AddDefaults());
O.Add(printOption = new PrintOption().AddDefaults());

SOut errorMessage = (s, n) => Console.Error.Write(s + (n ? "\n" : ""));
SOut message = (s, n) => Console.Write(s + (n ? "\n" : ""));

long? segTotal = 100_000_000_000L; // default: 100B
TimeSpan? segWall = null;
Grade grade = Grade.Med;

int rc = 0;
int cur = 0;
while (cur < cl_args.Count)
{
    if (O.TryParse(cl_args, cur, ref rc, message, errorMessage)) continue;
    if (cur >= cl_args.Count) continue;

    if (!cl_args[cur].StartsWith("-"))
    {
        cur++;
        continue;
    }

    string arg = cl_args[cur];

    if (arg is "-help" or "-h")
    {
        cl_args.RemoveAt(cur);
        showHelp = true;
        continue;
    }

    if (arg == "-segtotal")
    {
        cl_args.RemoveAt(cur);
        if (cur >= cl_args.Count || !long.TryParse(cl_args[cur], out long parsed) || parsed <= 0)
        {
            errorMessage("expected positive integer after -segtotal");
            rc = -1;
            showHelp = true;
        }
        else
        {
            segTotal = parsed;
            segWall = null; // segtotal wins if explicitly set after segwall
            cl_args.RemoveAt(cur);
        }
        continue;
    }

    if (arg == "-segwall")
    {
        cl_args.RemoveAt(cur);
        if (cur >= cl_args.Count || !TryParseSpan(cl_args[cur], out TimeSpan parsed) || parsed <= TimeSpan.Zero)
        {
            errorMessage("expected timespan after -segwall (examples: 01:00:00, 04:00:00, 1.00:00:00)");
            rc = -1;
            showHelp = true;
        }
        else
        {
            segWall = parsed;
            segTotal = null; // segwall wins if explicitly set after segtotal
            cl_args.RemoveAt(cur);
        }
        continue;
    }

    if (arg == "-grade")
    {
        cl_args.RemoveAt(cur);
        if (cur >= cl_args.Count || !Enum.TryParse(cl_args[cur], true, out grade))
        {
            errorMessage("expected one of: none low med high all after -grade");
            rc = -1;
            showHelp = true;
        }
        else
        {
            cl_args.RemoveAt(cur);
        }
        continue;
    }

    errorMessage($"Unknown argument: {arg}");
    cl_args.RemoveAt(cur);
    rc = -1;
    showHelp = true;
}

if (O.WantExit) return rc;

if (cl_args.Count != 1)
{
    errorMessage("expected one file path");
    showHelp = true;
}

if (showHelp || rc != 0)
{
    message("Usage: TruthInTheFlip_sample_report3");
    message();
    message(UtilT.PadRight("Supports:") + "TruthInTheFlip.v" + TrackerStore.VersionPrint(supported_ver));
    message("Arguments:");
    message(UtilT.PadRight("  <filepath>") + "Path to the tracker state file (required)");
    message(UtilT.PadRight("  -segtotal <n>") + "Segment size in flips (default: 100000000000)");
    message(UtilT.PadRight("  -segwall <ts>") + "Segment size in wallclock time (ex: 04:00:00)");
    message(UtilT.PadRight("  -grade <none|low|med|high|all>") + "Output detail level (default: med)");
    message(O.GetHelp(), false);
    message(UtilT.PadRight("  -help, -h") + "Display this help message");
    message();
    message("Description:");
    message("  Segment-oriented report focused on excursion vs settlement.");
    message("  Scores interpret segment behavior, not lifetime proof.");
    return -1;
}

string fileName = cl_args[0];

if (!windowOption.Enabled)
    errorMessage("warning viewing without window expect drift. Enable a window with -window def");

if (!File.Exists(fileName))
{
    errorMessage($"could not find \"{fileName.Replace("\"", "\\\"")}\"");
    return -1;
}

TrackerStore store = TrackerStore.Default(fileName);
if (store.Version == null)
{
    errorMessage($"\"{fileName}\" not a TrackerRecord file");
    return -1;
}

int[]? ver = TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version);
if (ver == null) throw new NullReferenceException();

if (TrackerStore.VersionCompare(ver, 1, 1, 0) > 0) HandleError($"{store.Path} Version {store.Version} newer than program");
if (TrackerStore.VersionCompare(ver, 1, 1, 0) < 0) HandleError($"{store.Path} Version {store.Version} lower than the v1.1.0 required by this util");

if (printOption.Enabled) store.print_delegate = printOption.Strategy;

bool validate_error = !O.ValidateVersion(store.Version, HandleError);
if (validate_error) return -1;

Tracker tail = (Tracker)store.LoadOrCreate(null);
TimeSpan wallclockTimeLength = new TimeSpan(tail.wallclockTimeNs / 100);

if (infoOption.Enabled)
{
    Tracker firstRecord = (Tracker)store.Enumerate().First();

    message("=== Run Configuration Info ===");
    message(UtilT.PadRight("File:") + fileName);
    message(UtilT.PadRight("record:") + store.Record);
    message(UtilT.PadRight("Supports:") + "TruthInTheFlip.v" + TrackerStore.VersionPrint(supported_ver));
    message(UtilT.PadRight("Tracker Version:") + TrackerStore.VersionPrint(ver));
    message(UtilT.PadRight("Total Flips:") + $"{tail.total:N0}");
    message(UtilT.PadRight("Start Time:") + firstRecord.UtcBeginTime + " UTC");
    message(UtilT.PadRight("End Time:") + tail.UtcEndTime + " UTC");
    message(UtilT.PadRight("Segment Mode:") + (segWall != null ? "wallclock" : "total"));
    message(UtilT.PadRight("Segment Size:") + (segWall != null ? segWall.ToString() : $"{segTotal:N0} flips"));
    message(UtilT.PadRight("Grade:") + grade.ToString().ToLowerInvariant());
    message();
    message("[Options]");
    Console.Write(O.Info());
    message("==============================");
    message();
}

TrackerWindow? window = null;
if (windowOption.Enabled)
{
    window = new TrackerWindow(
        store,
        UtilT.ThrowIfNull(windowOption.Strategy, "windowOption.Strategy"));
}

List<SegmentStats> segments = new();
SegmentStats? currentSegment = null;
long currentSegmentIndex = long.MinValue;

Tracker? finalState = null;

foreach (ITracker fromStore in store.Enumerate())
{
    Tracker state = (Tracker)fromStore;
    if (window != null) state = window.Add(state);

    long idx;
    if (segWall != null) idx = (state.Source.WallclockTime.Ticks / segWall.Value.Ticks);
    else if (segTotal != null) idx = state.Source.total / segTotal.Value;
    else throw new Exception("segWall and segTotal cannot both be null");
    
    if (idx != currentSegmentIndex)
    {
        currentSegment = new SegmentStats
        {
            Index = idx,
            BeginTotal = state.Source.total,
            BeginWallclock = state.Source.WallclockTime
        };
        segments.Add(currentSegment);
        currentSegmentIndex = idx;
    }

    currentSegment!.Inspect(state);
    currentSegment.EndTotal = state.Source.total;
    currentSegment.EndWallclock = state.Source.WallclockTime;
    currentSegment.EndState = state;

    finalState = state;
}

if (finalState == null || segments.Count == 0)
    throw new Exception("File cannot be empty");

SegmentAggregate agg = new SegmentAggregate(segments);

message($"{Path.GetFileName(fileName)} : {finalState.UtcEndTime:yyyyMMdd_HHmmss.ff} | {store.Print(finalState)}");
message();

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
    message($"avgBestTrueZ          : {Fmt(agg.AvgBestTrueZ)}");
    message($"medianBestTrueZ       : {Fmt(agg.MedianBestTrueZ)}");
    message($"avgEndTrueZ           : {Fmt(agg.AvgEndTrueZ)}");
    message($"medianEndTrueZ        : {Fmt(agg.MedianEndTrueZ)}");
    message($"avgMeanTrueZ          : {Fmt(agg.AvgMeanTrueZ)}");
    message($"avgPctAbove50         : {agg.AvgPctAbove50,8:0.0000}%");
    message();
    message($"bestTrueZ >= 1.96     : {agg.PctBestAtLeast(1.96),8:0.0000}%");
    message($"bestTrueZ >= 3.00     : {agg.PctBestAtLeast(3.00),8:0.0000}%");
    message($"endTrueZ  >= 0.00     : {agg.PctEndAtLeast(0.00),8:0.0000}%");
    message($"endTrueZ  >= 1.96     : {agg.PctEndAtLeast(1.96),8:0.0000}%");
    message($"meanTrueZ >= 0.00     : {agg.PctMeanAtLeast(0.00),8:0.0000}%");
    message();
}

if (grade >= Grade.High)
{
    message("idx | span                  | bestTrueZ | aAtBestTrueZ     | endTrueZ  | meanTrueZ | %>50");
    message("----+-----------------------+-----------+------------------+-----------+-----------+---------");

    foreach (SegmentStats s in segments)
    {
        string span = segWall != null
            ? $"{s.BeginWallclock}..{s.EndWallclock}"
            : $"{s.BeginTotal:N0}..{s.EndTotal:N0}";

        message(
            $"{s.Index,3} | " +
            $"{Trim(span, 21),-21} | " +
            $"{Fmt(s.BestTrueZ),9} | " +
            $"{Tracker.FormatOffset(s.BestTrueZHolder?.AnticipatedPercentage ?? 50.0, "0.00000e+00"),16} | " +
            $"{Fmt(s.EndTrueZ),9} | " +
            $"{Fmt(s.MeanTrueZ),9} | " +
            $"{s.PctAbove50,7:0.000}%"
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
    
    double retainedAnticipation = sumRetainedAnticipation / sumPctAbove50Fraction;
    message($"Retained Anticipation: {Tracker.FormatOffset(retainedAnticipation, "0.00000e+00")}");

    double settlementAdjustedAnticipation = sumSettlementAdjustedAnticipation / sumSegmentEndTrueZ;
    message($"Settlement Adjusted Anticipation: {Tracker.FormatOffset(settlementAdjustedAnticipation, "0.00000e+00")}");
    
    message();

    SegmentStats? bestExcursion = segments.OrderByDescending(s => s.BestTrueZ).FirstOrDefault();
    SegmentStats? bestSettlement = segments.OrderByDescending(s => s.EndTrueZ).FirstOrDefault();
    SegmentStats? worstSettlement = segments.OrderBy(s => s.EndTrueZ).FirstOrDefault();

    DumpSegment("Best excursion segment", bestExcursion);
    DumpSegment("Best settlement segment", bestSettlement);
    DumpSegment("Worst settlement segment", worstSettlement);
}

message($"File compute time: {wallclockTimeLength}");
return 0;

void DumpSegment(string label, SegmentStats? s)
{
    if (s == null) return;
    message(label + ":");
    message($"  idx         : {s.Index}");
    message($"  bestTrueZ   : {Fmt(s.BestTrueZ)}");
    message($"  meanTrueZ   : {Fmt(s.MeanTrueZ)}");
    message($"  endTrueZ    : {Fmt(s.EndTrueZ)}");
    message($"  pctAbove50  : {s.PctAbove50:0.0000}%");

    if (s.BestTrueZHolder != null)
    {
        message($"  best ZHeads : {Fmt(s.BestTrueZHolder.ZScoreHeads)}");
        message($"  aAtTrueZ    : {Tracker.FormatOffset(s.BestTrueZHolder.AnticipatedPercentage, "0.00000e+00")}");
    }

    if (s.EndState != null)
    {
        message($"  end ZHeads  : {Fmt(s.EndState.ZScoreHeads)}");
        message($"  end a       : {Tracker.FormatOffset(s.EndState.AnticipatedPercentage, "0.00000e+00")}");
    }

    message();
}

static bool TryParseSpan(string s, out TimeSpan span)
{
    return TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out span);
}

static string Fmt(double value) => Tracker.FormatWithPlus(value, "F6");

static string Trim(string s, int max)
{
    if (s.Length <= max) return s;
    return s[..(max - 1)] + "…";
}

void HandleError(string s, bool nl = true)
{
    errorMessage(s, nl);
    Environment.Exit(-1);
}

enum Grade
{
    None,
    Low,
    Med,
    High,
    All
}

sealed class SegmentStats
{
    public long Index;
    public long BeginTotal;
    public long EndTotal;
    public TimeSpan BeginWallclock;
    public TimeSpan EndWallclock;

    public Tracker? EndState;
    public Tracker? BestTrueZHolder;

    public long Count;
    public long Good;
    public double SumTrueZ;
    public double SumA;

    public double BestTrueZ = double.NegativeInfinity;

    public double MeanTrueZ => Count == 0 ? double.NaN : SumTrueZ / Count;
    public double MeanA => Count == 0 ? double.NaN : SumA / Count;
    public double EndTrueZ => EndState == null ? double.NaN : EndState.ZScore - Math.Abs(EndState.ZScoreHeads);
    public double PctAbove50 => Count == 0 ? double.NaN : (Good / (double)Count) * 100.0;

    public void Inspect(Tracker t)
    {
        double trueZ = t.ZScore - Math.Abs(t.ZScoreHeads);

        if (trueZ > BestTrueZ)
        {
            BestTrueZ = trueZ;
            BestTrueZHolder = t;
        }

        SumTrueZ += trueZ;
        SumA += t.AnticipatedPercentage;
        Count++;

        if ((t.anticipated << 1) >= t.total)
            Good++;
    }
}

sealed class SegmentAggregate
{
    private readonly List<SegmentStats> _segments;
    private readonly List<double> _best;
    private readonly List<double> _end;
    private readonly List<double> _mean;

    public SegmentAggregate(List<SegmentStats> segments)
    {
        _segments = segments;
        _best = segments.Select(s => s.BestTrueZ).OrderBy(v => v).ToList();
        _end = segments.Select(s => s.EndTrueZ).OrderBy(v => v).ToList();
        _mean = segments.Select(s => s.MeanTrueZ).OrderBy(v => v).ToList();
    }

    public double AvgBestTrueZ => _segments.Average(s => s.BestTrueZ);
    public double MedianBestTrueZ => Median(_best);

    public double AvgEndTrueZ => _segments.Average(s => s.EndTrueZ);
    public double MedianEndTrueZ => Median(_end);

    public double AvgMeanTrueZ => _segments.Average(s => s.MeanTrueZ);
    public double AvgPctAbove50 => _segments.Average(s => s.PctAbove50);

    public double PctBestAtLeast(double threshold) =>
        100.0 * _segments.Count(s => s.BestTrueZ >= threshold) / _segments.Count;

    public double PctEndAtLeast(double threshold) =>
        100.0 * _segments.Count(s => s.EndTrueZ >= threshold) / _segments.Count;

    public double PctMeanAtLeast(double threshold) =>
        100.0 * _segments.Count(s => s.MeanTrueZ >= threshold) / _segments.Count;

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return double.NaN;
        int mid = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[mid - 1] + values[mid]) / 2.0
            : values[mid];
    }
}