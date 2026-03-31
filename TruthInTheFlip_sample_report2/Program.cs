// See https://aka.ms/new-console-template for more information

using System.ComponentModel;
using System.Text;
using TruthInTheFlip_sample_report2;
using TruthInTheFlip.Format;
using TruthInTheFlip.Format.Options;

int[] supported_ver = new int[] { 1, 1, 0 };

List<String> cl_args = new(args);

bool showHelp = false;

Options O = new Options();
TrackerWindow.WindowOption windowOption;
InfoOption infoOption;

O.Add(infoOption = new InfoOption());
O.Add(windowOption = new TrackerWindow.WindowOption());

SOut errorMessage = (s, n) => Console.Error.Write(s +(n ? "\n" : ""));
SOut message =  (s, n) => Console.Write(s +(n ? "\n" : ""));

int rc = 0;
int cur = 0;
while (cur < cl_args.Count)
{
    if (O.TryParse(cl_args, cur, ref rc, message, errorMessage)) continue;
    if (cur >= cl_args.Count) continue;
    
    if (cl_args[cur].StartsWith("-"))
    {
        if (cl_args[cur] == "-help" || cl_args[cur] == "-h")
        {
            cl_args.RemoveAt(cur);
            showHelp = true;
            continue;
        }
        
        errorMessage($"Unknown argument: {args[cur]}");
        cl_args.RemoveAt(cur);

        rc = -1;
        showHelp = true;
    }
    else cur++;
}

if (O.WantExit) 
    return rc;


if (cl_args.Count() != 1)
{
    errorMessage("expected one file path");
    showHelp = true;
}

if (showHelp || rc != 0)
{
    message("Usage: TruthInTheFlip_sample_csv");
    message();
    message(UtilT.PadRight("Supports:") + "TruthInTheFlip.v" + TrackerStore.VersionPrint(supported_ver));
    message("Arguments:");
    message(UtilT.PadRight("  <filepath>") + $"Path to the tracker state file (required)");
    message(O.GetHelp(), false);
    message(UtilT.PadRight("  -help, -h") + "Display this help message");
    message();
    message("Description:");
    message("  This program outputs csv with a header for the file version.");
            
    return cl_args.Count() != 1 || (cl_args[0].StartsWith("-") && cl_args[0] != "-h" && cl_args[0] != "--help") ? -1 : 0;

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
    errorMessage("\"fileName\" not a TrackerRecord file");
    return -1; 
}
        
int[]? ver = TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version);

if (ver == null) throw new NullReferenceException();
if (TrackerStore.VersionCompare(ver, 1, 1, 0) > 0) HandleError($"{store.Path} Version {ver} newer than program");
if (TrackerStore.VersionCompare(ver, 1, 1, 0) < 0) HandleError($"{store.Path} Version {ver} lower than the v1.1.0 required by this util");


bool validate_error = false;
foreach (Option o in O)
{
    if (o is TrackerOption tracker_o) validate_error = !tracker_o.ValidateVersion(ver, HandleError) || validate_error;
}

if (validate_error) return -1;

Tracker tail= (Tracker)store.LoadOrCreate(null);
Tracker? t = tail;

TimeSpan wallclockTimeLength = new TimeSpan(t.wallclockTimeNs / 100);

if (infoOption.Enabled)
{
    message("=== Run Configuration Info ===");
    message(UtilT.PadRight("File:") + fileName);
    message(UtilT.PadRight("record:") + store.Record);

    message(UtilT.PadRight("Supports:") + "TruthInTheFlip.v" + TrackerStore.VersionPrint(supported_ver));
    
    // Quick peek at the file metadata (without running the full Enumerate)
    if (store.Version != null)
    {
        Tracker firstRecord = (Tracker) store.Enumerate().First(); 

        message(UtilT.PadRight($"Tracker Version:") + TrackerStore.VersionPrint(ver));
        // You could load the tail here just to print the total lifetime flips/times
        message(UtilT.PadRight("Total Flips:") + $"{tail.total:N0}");
        message(UtilT.PadRight("Start Time:") + firstRecord.UtcBeginTime +" UTC");
        message(UtilT.PadRight("End Time:") + tail.UtcEndTime +" UTC");
        message();
    }
    
    message("[Options]");
    Console.Write(O.Info());
    message("==============================\n");
}

List<WatchVariables> watchers = new List<WatchVariables>();
Dictionary<WatchVariables, TimeSpan> watchTimes = new Dictionary<WatchVariables, TimeSpan>();
Dictionary<WatchVariables, string> labels = new();

Func<TimeSpan, string, WatchVariables?> addWindow = (TimeSpan window, string label) =>
{
    TimeSpan start = wallclockTimeLength - window;
    if (start >= TimeSpan.Zero)
    {
        WatchVariables variables = new();

        variables.watchers["Z"] = new Watch(tracker => tracker.ZScore);
        variables.watchers["a"] = new Watch(tracker => tracker.AnticipatedPercentage);

        watchers.Add(variables);
    
        labels.Add(variables, label);
        watchTimes.Add(variables, start);
        return variables;
    }

    return null;
};

int hr = 1;
for (int i=0; i<4; i++) 
{ 
    addWindow(TimeSpan.FromHours(hr), $"last {hr}hr");
    hr *= 2;
}

int day = 1;
for (int i=0; i<2; i++) 
{ 
    addWindow(TimeSpan.FromDays(day), $"last {day}day");
    day *= 2;
}

WatchVariables w = UtilT.ThrowIfNull(addWindow(wallclockTimeLength, "lifetime"), "lifetime can not be null");

TrackerWindow? window = null;

if (windowOption.Enabled) window = new TrackerWindow(store, UtilT.ThrowIfNull(windowOption.WindowStrategy, "windowOption.WindowStrategy"));

t = null;
foreach (ITracker fromStore in store.Enumerate())
//foreach (var t1 in Util.Enumerate(store, 1000000000000L))
{
    t = (Tracker)fromStore;
    if (window != null) t = window.Add(t);
    for (int i = watchers.Count -1; i >=0 && ((Tracker)fromStore).WallclockTime >= watchTimes[watchers[i]]; i--) watchers[i].Inspect(t);
}

if (t == null)
{
    throw new Exception("File cannot be empty");
}

DateTime utcBeginTime = t.UtcBeginTime;
DateTime utcEndTime = t.UtcEndTime;

message(Path.GetFileName(fileName) + " : " + utcEndTime.ToString("yyyyMMdd_HHmmss.ff") + " | " + store.Print(t));

{
    message();

    Console.Write("total,heads,tails,anticipated,baseAnticipated,anticipatedHeads,anticipatedTails,cumulativeTicks");
    if (TrackerStore.VersionCompare(ver, 1, 1, 0) >= 0)
        Console.Write(
            ",batchTotal,wallclockTimeNs,batchWallclockTimeNs,utcBeginTimeMs,utcEndTimeMs,betHeads,betSame,anticipatedSame");
    message();

    {
        Console.Write(
            $"{t.total},{t.heads},{t.tails},{t.anticipated},{t.baseAnticipated},{t.anticipatedHeads},{t.anticipatedTails},{t.cumulativeTicks}");

        if (TrackerStore.VersionCompare(ver, 1, 1, 0) >= 0)
            Console.Write(
                $",{t.batchTotal},{t.wallclockTimeNs},{t.batchWallclockTimeNs},{t.utcBeginTimeMs},{t.utcEndTimeMs},{t.betHeads},{t.betSame},{t.anticipatedSame}");
        message();
    }
    
    message();

}

Func<WatchVariables, string> print = (wv) =>
{
    StringBuilder builder = new();
    //builder.Append($"{(string.Format("{0,8:0.0000}", wv.good / (double)wv.cnt * 100))}% time above 50%");
    builder.Append($"{wv.good / (double)wv.cnt * 100,8:0.0000}% time above 50%");
    builder.Append($" | minZ= {Tracker.FormatWithPlus(wv["Z"].min, "F6")}");
    builder.Append($" | maxZ= {Tracker.FormatWithPlus(wv["Z"].max, "F6")}");
    Tracker? maxHolder = wv["Z"].maxHolder;
    
    if (maxHolder != null)
    {
        builder.Append($" | ZHeadsAtMaxZ= {Tracker.FormatWithPlus(maxHolder.ZScoreHeads, "F6")}");
        builder.Append($" | aAtMaxZ= {Tracker.FormatOffset(maxHolder.AnticipatedPercentage, "0.00000e+00")}");
    }

    builder.Append($" | z= {Tracker.FormatWithPlus(wv["Z"].average, "F6")}");
    
    builder.Append($" | a= {Tracker.FormatOffset(wv["a"].average, "0.000e+00")}");

    return builder.ToString();
};

int l1 = int.MinValue; foreach (var w2 in watchers) l1 = int.Max(l1, labels[w2].Length);

foreach (var w2 in watchers) message($"{labels[w2] + new string(' ', l1 - labels[w2].Length)} | {print(w2)}");
message();

// The fixed state from your tracker
long observed = t.anticipated;
long total = t.total;

Func<long, long, double, double> ZForClaim = (observed, total, p) =>
{
    double expected = total * p;

    // Standard deviation for binomial distribution
    double stdDev = Math.Sqrt(total * p * (1.0 - p));

    // Calculate Z
    return (observed - expected) / stdDev;
};

Func<long, long, double, double> ZCompass = (observed, total, targetZ) =>
{
// Define the search space bounds
    double upperP = 0.5125; // Yields a lower Z
    double lowerP = 0.4875; // Yields a higher Z

    while (lowerP> 0.000001 && ZForClaim(observed, total, lowerP) < targetZ) lowerP -= 0.000001;
    
    double p  = double.NegativeInfinity;
    double currentZ;

    // 100 iterations is more than enough to exhaust double-precision float limits
    for (int i = 0; i < 100; i++)
    {
        p = (upperP + lowerP) / 2.0;
        currentZ = ZForClaim(observed, total, p);

        // Break early if we hit the desired precision
        if (Math.Abs(currentZ - targetZ) < 0.000001) break;

        if (currentZ < targetZ)
        {
            // Z is too low, meaning our 'p' claim is too high. Pull the upper bound down.
            upperP = p;
        }
        else
        {
            // Z is too high, meaning our 'p' claim is too low. Push the lower bound up.
            lowerP = p;
        }
    }

    return p;
};

Tracker ? ZMax = w["Z"].maxHolder;

message($"To achieve exactly Z=3.0, we can claim a win rate of: {Tracker.FormatOffset(ZCompass(tail.anticipated, tail.total, 3.0) * 100, "0.00000e+00")}");
if (ZMax != null)
{
    message($"To achieve from ZMax we can claim a win rate of: {Tracker.FormatOffset(ZCompass(ZMax.anticipated, ZMax.total, 3.0) * 100, "0.00000e+00")}");
}

message($"File compute time: {wallclockTimeLength}");

return 0;

void HandleError(string message, bool nl = true)
{
    errorMessage(message, nl);
    Environment.Exit(-1);
}

