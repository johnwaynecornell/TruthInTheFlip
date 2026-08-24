using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;
using FluentCommandLine;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

public class TruthInTheFlip_Fluent
{
    public static void FluentModuleInitialize(FluentEnvironment env)
    {
        env.AddModule<TrackerWindows>();
        env.AddModule<TrackerWindows.TrackerWindow>();
        env.AddModule<TrackerBoundarys>();
        env.AddModule<SegmentStatsReport>();

        if (!env.Context.TryGet<MetricCatalogs>(out var catalogs))
        {
            catalogs = new MetricCatalogs();
            env.Context.Set(catalogs);
        }

        env.TypeParseHandlers[typeof(TimeSpan)] =
            (type, commandArgs, ref cursor, ref status, message, errorMessage, out result) =>
            {
                if (!TimeSpan.TryParse(commandArgs[cursor], CultureInfo.InvariantCulture, out var parsed))
                {
                    status = -1;
                    result = null;
                    errorMessage($"Could not parse {commandArgs[cursor]} as a TimeSpan");
                    return false;
                }

                cursor++;
                result = parsed;
                return true;
            };

        env.TypeParseHandlers[typeof(DateTime)] =
            (type, commandArgs, ref cursor, ref status, message, errorMessage, out result) =>
            {
                if (!DateTime.TryParse(commandArgs[cursor], CultureInfo.InvariantCulture, out var parsed))
                {
                    status = -1;
                    result = null;
                    errorMessage($"Could not parse {commandArgs[cursor]} as a DateTime");
                    return false;
                }

                cursor++;
                result = parsed;
                return true;
            };

        env.TypeParseHandlers[typeof(DateTimeOffset)] =
            (type, commandArgs, ref cursor, ref status,
                message, errorMessage, out result) =>
            {
                if (!DateTimeOffset.TryParse(
                        commandArgs[cursor],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsed))
                {
                    status = -1;
                    result = null;
                    errorMessage(
                        $"Could not parse {commandArgs[cursor]} as a DateTimeOffset");
                    return false;
                }

                cursor++;
                result = parsed;
                return true;
            };

        env.Context.Get<MetricCatalogs>().Reflect = DefaultReflect;

        if (!env.Context.Get<MetricCatalogs>().TryGet(typeof(Tracker), out var catalog))
        {
            Console.Error.WriteLine($"Metric catalog not found for type Tracker");
            throw new Exception("Metric catalog not found for type Tracker");
        }

        catalog.Metrics["Source"] = new MetricDescriptor()
        {
            Type = MetricDescriptor.EType.Property,
            Name = "Source",
            Help = "Source of the tracker",
            ValueType = typeof(Tracker),
            Getter = (tracker) => ((Tracker)tracker).Source
        };
    }

    private static MetricCatalog? DefaultReflect(Type arg)
    {
        List<MetricDescriptor> l = new List<MetricDescriptor>();

        foreach (var member in arg.GetMembers())
        {
            if ((member.GetCustomAttributes(typeof(IsMetricAttribute), true).FirstOrDefault() is not null)
                || (member.GetCustomAttributes(typeof(IsRecordAttribute), true).FirstOrDefault() is not null))
            {
                if (member is FieldInfo fieldInfo)
                    l.Add(new MetricDescriptor
                    {
                        Type = MetricDescriptor.EType.Property,
                        Name = member.Name,
                        ValueType = fieldInfo.FieldType,
                        Help =
                            (string)(member.GetCustomAttributes(typeof(StringHelpAttribute), true).FirstOrDefault() as
                                StringHelpAttribute).Description,
                        Getter = obj => ((FieldInfo)member).GetValue(obj)
                    });
                else if (member is PropertyInfo propertyInfo)
                    l.Add(new MetricDescriptor
                    {
                        Type = MetricDescriptor.EType.Property,
                        Name = member.Name,
                        ValueType = propertyInfo.PropertyType,
                        Help =
                            (string)(member.GetCustomAttributes(typeof(StringHelpAttribute), true).FirstOrDefault() as
                                StringHelpAttribute).Description,
                        Getter = obj => ((PropertyInfo)member).GetValue(obj)
                    });
                else if (member is MethodInfo methodInfo)
                {
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    
                    List<MetricParameterDescriptor> _p = new List<MetricParameterDescriptor>();
                    
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        
                        if (parameters[i].ParameterType == typeof(List<double>))
                            _p.Add(new MetricParameterDescriptor()
                            {
                                Parameter = parameters[i],
                                Type = MetricParameterType.Aggregate
                            });
                        else
                            _p.Add(new MetricParameterDescriptor()
                            {
                                Parameter = parameters[i],
                                Type = MetricParameterType.Scalar
                            });
                    }
                    
                    l.Add(new MetricDescriptor
                    {
                        Type = MetricDescriptor.EType.Method,
                        Name = member.Name,
                        ValueType = methodInfo.ReturnType,
                        Help = (member.GetCustomAttributes(typeof(StringHelpAttribute), true)
                            .FirstOrDefault() as StringHelpAttribute).Description,
                        Method = (MethodInfo)member,
                        Parameters = _p
                    });

                }
            }
            
        }

        if (l.Count == 0) return null;
        MetricCatalog R = new MetricCatalog();
        R.Metrics = l.ToDictionary(x => x.Name, x => x);
        return R;
    }


    [FluentMethod(def: true)]
    [KV_FA(FluentAttribute.Help, "Segment size in total flips")]
    public static SegSelector by_total(
        [KV_FA(FluentAttribute.Def, "100B")]
        [KV_FA(FluentAttribute.Help, "Maximum number of total flips in each segment.")]
        Count length)
    {
        return new SegSelector((stats, tracker) =>
            tracker.Source.total - stats.Begin.Source.total < length);
    }

    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Segment size in time elapsed")]
    public static SegSelector by_elapsed(
        [KV_FA(FluentAttribute.Def, "01:00:00")]
        [KV_FA(FluentAttribute.Help, "Maximum wall-clock duration of each segment.")]
        TimeSpan length)
    {
        return new SegSelector((stats, tracker) =>
            tracker.Source.WallclockTime - stats.Begin.Source.WallclockTime < length);
    }

    [FluentMethod("by_total", def: true)]
    [KV_FA(FluentAttribute.Help, "Segment size in total flips")]
    public static AggSelector Agg_by_total(
        [KV_FA(FluentAttribute.Def, "100B")]
        [KV_FA(FluentAttribute.Help, "Maximum number of total flips in each segment.")]
        Count length)
    {
        return new AggSelector((stats, tracker) =>
            tracker.EndTotal - stats.Begin.Begin.absTotal < length);
    }

    [FluentMethod("by_elapsed")]
    [KV_FA(FluentAttribute.Help, "Segment size in time elapsed")]
    public static AggSelector Agg_by_elapsed(
        [KV_FA(FluentAttribute.Def, "01:00:00")]
        [KV_FA(FluentAttribute.Help, "Maximum wall-clock duration of each segment.")]
        TimeSpan length)
    {
        return new AggSelector((stats, tracker) =>
            tracker.EndWallclock - stats.Begin.Begin.absWallclockTime < length);
    }

    [FluentMethod("file")]
    [KV_FA(FluentAttribute.Help, "Read tracker records from a tracker file.")]
    public static TrackerSelector Tracker(
        [KV_FA(FluentAttribute.Help, "Path to the tracker file.")]
        string trackerPath)
    {
        return new TrackerSelector(() => OpenTrackerStream(trackerPath));
    }

    [FluentMethod("full")]
    [KV_FA(FluentAttribute.Help, "Use only full trackers.")]
    public static TrackerSelector fullTracker(
        [KV_FA(FluentAttribute.Help, "The source tracker.")]
        TrackerSelector source)
    {
        return new TrackerSelector(source, t => ((Tracker)t).IsComplete);
    }

    [FluentMethod("full")]
    [KV_FA(FluentAttribute.Help, "Use only complete segments.")]
    public static SegSelector fullSegSelector(
        [KV_FA(FluentAttribute.Help, "The source selector.")]
        SegSelector source)
    {
        return new SegSelector(source, (stats) => stats.IsComplete);
    }

    [FluentMethod("full")]
    [KV_FA(FluentAttribute.Help, "Use only complete segments.")]
    public static AggSelector fullAggSelector(
        [KV_FA(FluentAttribute.Help, "The source selector.")]
        AggSelector source)
    {
        return new AggSelector(source, (stats) => stats.IsComplete);
    }

    public static TrackerStream OpenTrackerStream(string trackerPath)
    {
        if (!File.Exists(trackerPath))
        {
            throw new FarmInputException(
                $"Could not find \"{trackerPath}\".");
        }

        TrackerStore store = TrackerStore.Default(trackerPath);

        if (store.Version == null)
        {
            throw new FarmInputException(
                $"\"{trackerPath}\" is not a TrackerRecord file.");
        }

        int[] version = TrackerStore.ReadVersion(
                            "TruthInTheFlip.v",
                            store.Version)
                        ?? throw new FarmInputException(
                            $"Could not parse tracker version \"{store.Version}\".");

        if (TrackerStore.VersionCompare(version, 1, 1, 0) > 0)
        {
            throw new FarmInputException(
                $"{store.Path} version {store.Version} is newer than this program.");
        }

        if (TrackerStore.VersionCompare(version, 1, 1, 0) < 0)
        {
            throw new FarmInputException(
                $"{store.Path} version {store.Version} is below the required v1.1.0.");
        }

        List<ITracker> records =
            store.Enumerate().ToList();

        return new TrackerStream(store, records);
    }


    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Format a process as CSV using the selected metric fields.")]
    public static FarmCommand csv(
        [KV_FA(FluentAttribute.Help, "Process whose items will be written as CSV.")]
        FarmProcess process,
        [KV_FA(FluentAttribute.Help, "Metric paths to include as CSV columns.")]
        params string[] fields)
    {
        var catalogs = FluentEnvironment.Current.Context.Get<MetricCatalogs>();

        if (!process.BindFields(catalogs, fields, out MetricBindError? bindError))
        {
            Console.Error.WriteLine(bindError!.FormatDiagnostic());
            var env = FluentEnvironment.Current;
            env.Status = 1;
            env.WantExit = true;
            return new FarmDelegateCommand(_ => { }); // never executed; WantExit stops the loop
        }

        process.Actions = new ProcessActions(
            begin: context =>
                WriteHeader(process.projection_get(), context.Output),

            process: (context, stats) =>
                WriteRow(process.projection_get(), context.Output, stats),

            end: context =>
                context.Output.Flush(),

            abort: HandleAbort);

        return new FarmDelegateCommand((ctx) => { process.Execute(ctx); });
    }

    [FluentMethod("segment")]
    [KV_FA(FluentAttribute.Help, "Process tracker records as segments.")]
    public static FarmProcess Segment(
        [KV_FA(FluentAttribute.Help, "Tracker source to process.")]
        TrackerSelector tracker,
        [KV_FA(FluentAttribute.Help, "Method used to divide tracker records into segments.")]
        SegSelector segmentation)
    {

        var process = new SegmentStatsProcess(
            tracker,
            segmentation);

        return process;
    }

    [FluentMethod("segment_agg")]
    [KV_FA(FluentAttribute.Help, "Process segment stats as segments.")]
    public static FarmProcess SegmentAgg(
        [KV_FA(FluentAttribute.Help, "Tracker source to process.")]
        TrackerSelector tracker,
        [KV_FA(FluentAttribute.Help, "Method used to divide tracker records into segments.")]
        SegSelector segmentation,
        [KV_FA(FluentAttribute.Help, "Method used to aggregate segments.")]
        AggSelector aggregation)
    {

        var process = new SegmentAggregateProcess(
            tracker,
            segmentation,
            aggregation);

        return process;
    }

    public static void WriteHeader(MetricProjection projection, TextWriter writer)
    {
        bool f = true;
        foreach (var field in projection.Fields)
        {
            if (!f) writer.Write(",");
            else f = false;

            CSVOut(writer, field.ToString());
        }

        writer.WriteLine();
    }

    public static void WriteRow(MetricProjection projection, TextWriter writer, object stats)
    {
        bool f = true;
        foreach (var field in projection.Fields)
        {
            if (!f) writer.Write(",");
            else f = false;

            object? o = field.Get(projection, stats);
            CSVOut(writer, o);
        }

        writer.WriteLine();

    }

    public static void CSVOut(TextWriter writer, object? obj)
    {
        if (obj == null) return;

        string? s = obj switch
        {
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            TimeSpan ts => ts.ToString("c", CultureInfo.InvariantCulture),
            _ => obj.ToString()
        };

        if (string.IsNullOrEmpty(s)) return;

        if (s.Contains(",") || s.Contains("\"") || s.Contains("\r") || s.Contains("\n"))
        {
            writer.Write('"');
            writer.Write(s.Replace("\"", "\"\""));
            writer.Write('"');
        }
        else
        {
            writer.Write(s);
        }
    }

    [FluentMethod("tracker")]
    [KV_FA(FluentAttribute.Help, "Process tracker records individually.")]
    public static FarmProcess TrackerReport(
        [KV_FA(FluentAttribute.Help, "Tracker source to process.")]
        TrackerSelector tracker)
    {
        var process = new TrackerProcess(tracker);
        return process;
    }

    public static void HandleAbort(FarmContext ctx, Exception exception)
    {
        if (exception is FarmInputException e)
        {
            Console.Error.WriteLine(e.Message);
            Environment.Exit(1);
        }

        Console.Error.WriteLine(exception.Message);

        string errorLogPath = Path.Combine(
            Environment.CurrentDirectory,
            $"error_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        File.WriteAllText(errorLogPath,
            $"Exception occurred at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
            $"Message: {exception.Message}\n\n" +
            $"Stack Trace:\n{exception.StackTrace}\n\n" +
            $"Full Exception:\n{exception}\n");

        Console.Error.WriteLine($"Full error details written to: {errorLogPath}");

        Environment.Exit(1);
    }
}