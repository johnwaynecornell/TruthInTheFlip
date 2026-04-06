// See https://aka.ms/new-console-template for more information

using System.Text;
using Report1_tst2;
using TruthInTheFlip.Format;

List<String> cl_args = new(args);

bool showHelp = false;
bool force = false;
int rc = 0;

int cur = 0;
while (cur < cl_args.Count)
{
    if (cl_args[cur].StartsWith("-"))
    {
        if (cl_args[cur] == "-force")
        {
            cl_args.RemoveAt(cur);
            continue;
        }
        
        if (cl_args[cur] == "-help" || cl_args[cur] == "-h" || cl_args[cur]=="--help")
        {
            cl_args.RemoveAt(cur);
            showHelp = true;
            continue;
        }

        Console.Error.WriteLine($"Unknown argument: {cl_args[cur]}");
        cl_args.RemoveAt(cur);
        showHelp = true;
        rc = -1;
        continue;
    }
    else cur++;
}

if (cl_args.Count() != 1 && !(showHelp || (rc !=0)))
{
    Console.Error.WriteLine("expected one file path");
    rc = -1;
    showHelp = true;
}

if (showHelp)
{
    Console.WriteLine("Usage: TruthInTheFlip_sample_report");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <filepath>          Path to the tracker state file (required)");
    Console.WriteLine("  -force              enable processing trackers newer than v1.1.0 if binary supports it");
    Console.WriteLine();
    Console.WriteLine("Description:");
    Console.WriteLine("  This program outputs a report with time windows relative to end of a v1.1.0 '.tkr'.");
            
    return rc;
}

string fileName = cl_args[0];

if (!File.Exists(fileName))
{
    Console.Error.WriteLine($"could not find \"{fileName.Replace("\"", "\\\"")}\""); 
    return -1;
}

TrackerStore store = TrackerStore.Default(fileName);
if (store.Version == null) 
{
    Console.Error.WriteLine("\"fileName\" not a TrackerRecord file");
    return -1; 
}
        
int[]? ver = TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version);

if (ver == null) throw new NullReferenceException();
if (!force & TrackerStore.VersionCompare(ver, 1, 1, 0) > 0) HandleError($"{store.Path} Version {store.Version} newer than program");
if (TrackerStore.VersionCompare(ver, 1, 1, 0) < 0) HandleError($"{store.Path} Version {store.Version} lower than the v1.1.0 required by this util");


Tracker? t = (Tracker)store.LoadOrCreate(null);

//in .net it is simpler to just use TimeSpan than doing our own conversions with t.wallclockTimeNs
TimeSpan wallclockTimeLength = t.WallclockTime;

List<WatchVariables> watchers = new List<WatchVariables>();
Dictionary<WatchVariables, TimeSpan> watchTimes = new Dictionary<WatchVariables, TimeSpan>();
Dictionary<WatchVariables, string> labels = new();

Action<TimeSpan, string> addWindow = (TimeSpan window, string label) =>
{
    watchers.Add(new WatchVariables());
    labels.Add(watchers.Last(), label);
    watchTimes.Add(watchers.Last(), wallclockTimeLength - window);
};

int hr = 1;
for (int i=0; i<4; i++) 
{ 
    addWindow(TimeSpan.FromHours(hr), hr + "hr");
    hr *= 2;
}

WatchVariables w = new WatchVariables();

t = null;
foreach (var t1 in store.Enumerate())
{
    t = (Tracker)t1;
    TimeSpan tWallclockTime = new TimeSpan(t.wallclockTimeNs / 100);
    w.Inspect(t);
    foreach (WatchVariables w2 in watchers) if (tWallclockTime>=watchTimes[w2]) w2.Inspect(t);
}

if (t == null)
{
    Console.Error.WriteLine("File cannot be empty");
    return -1;
}

DateTime utcBeginTime = t.UtcBeginTime;
DateTime utcEndTime = t.UtcEndTime;

Console.WriteLine(Path.GetFileName(fileName) + " : " + utcEndTime.ToString("yyyyMMdd_HHmmss.ff") + " | " + store.Print(t));

{
    Console.Write("total,heads,tails,anticipated,baseAnticipated,anticipatedHeads,anticipatedTails,cumulativeTicks");
    if (TrackerStore.VersionCompare(ver, 1, 1, 0) >= 0)
        Console.Write(
            ",batchTotal,wallclockTimeNs,batchWallclockTimeNs,utcBeginTimeMs,utcEndTimeMs,betHeads,betSame,anticipatedSame");
    Console.WriteLine();

    {
        Console.Write(
            $"{t.total},{t.heads},{t.tails},{t.anticipated},{t.baseAnticipated},{t.anticipatedHeads},{t.anticipatedTails},{t.cumulativeTicks}");

        if (TrackerStore.VersionCompare(ver, 1, 1, 0) >= 0)
            Console.Write(
                $",{t.batchTotal},{t.wallclockTimeNs},{t.batchWallclockTimeNs},{t.utcBeginTimeMs},{t.utcEndTimeMs},{t.betHeads},{t.betSame},{t.anticipatedSame}");
        Console.WriteLine();
    }
}

Func<WatchVariables, string> print = (wv) =>
{
    StringBuilder builder = new();
    //builder.Append($"{(string.Format("{0,8:0.0000}", wv.good / (double)wv.cnt * 100))}% time above 50%");
    builder.Append($"{wv.good / (double)wv.cnt * 100,8:0.0000}% time above 50%");
    builder.Append($" | minZ= {Tracker.FormatWithPlus(wv.minZ, "F6")}");
    builder.Append($" | maxZ= {Tracker.FormatWithPlus(wv.maxZ, "F6")}");
    builder.Append($" | a= {Tracker.FormatOffset(wv.a / wv.cnt, "0.0e+00")}");
    builder.Append($" | z= {Tracker.FormatWithPlus(wv.Z / wv.cnt, "F6")}");

    return builder.ToString();
};

foreach (var w2 in watchers) Console.WriteLine($"last {labels[w2]} | {print(w2)}");
Console.WriteLine($"lifetime | {print(w)}");

return 0;

void HandleError(string message)
{
    Console.Error.WriteLine(message);
    Environment.Exit(-1);
}

