// See https://aka.ms/new-console-template for more information

using System.Globalization;
using System.Reflection;
using FluentCommandLine;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip_CSV_Farm;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;

FluentEnvironment env = new FluentEnvironment();
env.AddModule<HelpCommand>();
env.AddModule<InfoCommand>();
env.AddModule<Commands>();
env.AddModule<TruthInTheFlip_Fluent>();

/* Here is an example of programmatically adding a metric method to the catalog for SegmentStats.*/
//
// env.Context.Get<MetricCatalogs>().TryGet(typeof(SegmentStats), out var catalog);
// catalog.Add(new MetricDescriptor(
//     "foo",
//     typeof(double),
//     new()
//     {
//         new("bar", MetricParameterType.Scalar)
//     },
//     "Double an input.",
//     (ctx ,obj, args) => (double)args[0]! * 2)
// {
//     SourceExpressions = null
// });

/* Here is an example of programmatically adding a regular metric to the catalog for Tracker.*/
// env.Context.Get<MetricCatalogs>().TryGet(typeof(Tracker), out var catalog);
// catalog.Add(new MetricDescriptor()
// {
//     Type = MetricDescriptor.EType.Property,
//     Name = "TrueZ2",
//     Help = "Example duplicate TrueZ",
//     ValueType = typeof(double),
//     Getter = (ctx, tracker) => ((Tracker)tracker).ZScore - Math.Abs(((Tracker)tracker).ZScoreHeads)
// });

/* Here is an example of programmatically adding an alia for and expression to the catalog for Tracker.*/
// env.Context.Get<MetricCatalogs>().TryGet(typeof(Tracker), out var catalog);
// catalog.Add(new MetricDescriptor()
// {
//     Type = MetricDescriptor.EType.Property,
//     Name = "Alias_TrueZ",
//     Help = "Example duplicate TrueZ",
//     ValueType = typeof(double),
//     Getter = (ctx, tracker) => ctx.Get("sub#ZScore,abs#ZScoreHeads"),
//     SourceExpressions = ["sub#ZScore,abs#ZScoreHeads"]
// });


env.ServeTypes = new Type[] { typeof(FarmCommand), typeof(InfoCommand), typeof(HelpCommand) };

List<String> cl = new List<String>(args);

/*
  
cl = new List<String>("csv segment window by_total 10B file /data/jwc/Documents/Trackers/crypto.tkr by_total 100B Index EndTotal BeginWallclock Begin.absoluteTotal End.absoluteTotal Z.AnticipatedPercentage .END. -info filename.meta.txt".Split(' '));
cl = new List<String>("csv tracker window by_total 10B file /data/jwc/Documents/Trackers/crypto3.tkr absoluteTotal heads tails anticipated ZScore .END. -info filename.meta.txt".Split(' '));

cl = new List<String>("-help".Split(' '));
cl = new List<String>("show metrics".Split(' '));

cl = new List<String>("csv tracker from absWallclock 01:00:00 to absWallclock 02:00:00 window by_total 10B file /data/jwc/Documents/Trackers/crypto3.tkr WallclockTime absTotal heads tails anticipated ZScore UtcEndTime .END. -info filename.meta.txt".Split(' '));

cl = new List<String>("csv segment window by_total 100B file /data/jwc/Documents/Trackers/crypto3.tkr by_total 100B Index EndTotal MeanTrueZ EndTrueZ BestTrueZ".Split(' '));

cl = new List<string>
{
    "csv",
    "segment",
    "window",
    "by_total",
    "100B",
    "file",
    "/data/jwc/Documents/Trackers/crypto3.tkr",
    "by_total",
    "100B",
    "Index",
    "End.AnticipatedPercentage",
    "End.SamePercentage",
    "PctAnticipatedSameSign"
};

cl = new List<String>("csv segment_agg window by_total 10B file /data/jwc/Documents/Trackers/crypto.tkr by_total 10B by_total 100B Index mean#median#AnticipatedPercentage".Split(' '));

*/
cl = new List<String>(args);

int cl_index = 0;

HelpCommand? helpToken = null;
InfoCommand? infoToken = null;
FarmCommand? reportCommand = null;

FluentMethodRegistry.RegistryParseResult reportResult = null;

while (cl_index < cl.Count)
{
    var res = env.ParseOne(cl, ref cl_index);
    if (env.WantExit)
        break;

    if (res == null)
    {
        if (env.Status == 0)
            Console.Error.WriteLine($"Unrecognized command or argument at index {cl_index}: '{cl[cl_index]}'");
        break;
    }

    if (res.Result != null)
    {
        Object Result = res.Result;
        Type T = Result.GetType();
        if (Result is InfoCommand info) env.Unique(ref infoToken, info, () => "Only one -info allowed");
        if (Result is FarmCommand report)
        {
            env.Unique(ref reportCommand, report, () => "Only one report command allowed");
            reportResult = res;
        }
        if (Result is HelpCommand help) env.Unique(ref helpToken, help, () => "Only one -help allowed");

        //Console.WriteLine(res.Registry.Info(env, res));
    }
}

if (cl_index < cl.Count && !env.WantExit)
{
    Console.WriteLine($"Unconsumed arguments remaining at index {cl_index}: {string.Join(" ", cl.GetRange(cl_index, cl.Count - cl_index))}");
}

// Only show help when there was no explicit error that already reported itself.
// When WantExit is set by an input-error handler (e.g. a bad metric expression),
// the diagnostic has already been written to stderr; dumping the full help on top
// of it is noisy and unhelpful.
if ((helpToken != null || reportCommand == null) && !env.WantExit)
{
    Console.Error.WriteLine("Usage: TruthInTheFlip_Farm [arguments]");
    Console.Error.WriteLine(@"
TruthInTheFlip Farm is under active development.

The command language and available reports may grow between releases.
For this build, the generated help and metric listings are the authoritative reference.
");
    Console.Error.WriteLine(env.Help());
    return 1;
}

if (env.WantExit || env.Status != 0)return env.Status == 0 ? 0 : 1;

if (infoToken != null)
{
    string info = reportResult.Registry.Info(env, reportResult);
    File.WriteAllText(infoToken.OutputPath, info);
    Console.Error.WriteLine($"Wrote info to {infoToken.OutputPath}");
}

if (reportResult.Result is FarmCommand cmd) cmd.Execute(new());

return 0;


