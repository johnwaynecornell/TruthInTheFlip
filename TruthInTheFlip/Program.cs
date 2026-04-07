using TruthInTheFlip.Format;
using TruthInTheFlip.Format.Options;
using System.Diagnostics;
using TruthInTheFlip;

public class Program
{
    public static string? fileName = null;
    public static TrackerStore? store = null;
    public static ITracker? allTime = null;

    public static bool log = false;
    public static bool record;

    public static void LogWrite(string text)
    {
        Console.Write(text);

        if (log)
        {
            using (var fs = new FileStream(fileName + ".log", FileMode.Append))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(text);
            }
        }
    }

    public static void LogWriteLine(string text)
    {
        message(text);

        if (log)
        {
            using (var fs = new FileStream(fileName + ".log", FileMode.Append))
            using (var sw = new StreamWriter(fs))
            {
                sw.WriteLine(text);
            }
        }
    }

    public static string RecordText(Tracker tracker, string tail)
    {
        if (store == null || allTime == null) throw new NullReferenceException("Program is uninitialized");

        return (store.Print(tracker) + (tail == "" ? "" : $" | {tail}"));
    }

    public static SOut errorMessage = (s, n) => Console.Error.Write(s + (n ? "\n" : ""));
    public static SOut message = (s, n) => Console.Write(s + (n ? "\n" : ""));

    public static int[] supported_ver = TrackerStore.VersionArray(1, 1, 0);

    public static int Main(string[] command_line_args)
    {
        BitFactory bitFactory = new BitFactory();

        //fillArray = QuantisInterop.initQuantis_Linux();

        List<String> cl_args = new(command_line_args);

        Options O = new Options();

        InfoOption infoOption;
        RSourceOption rsourceOption;
        TrackerWindow.WindowOption windowOption;
        AnticipationStrategies.AnticipationOption anticipationOption;

        O.Add(infoOption = new InfoOption());
        O.Add(rsourceOption = new RSourceOption().AddDefaults());
        O.Add(windowOption = new TrackerWindow.WindowOption().AddDefaults());
        O.Add(anticipationOption = new AnticipationStrategies.AnticipationOption().AddDefaults());

        bool show = false;
        bool dump = false;
        bool createIfDoesntExsist = false;
        bool concise = false;

        bool showHelp = false;
        int rc = 0;

        Func<bool>? runCondition = null;

        int cur = 0;
        while (cur < cl_args.Count)
        {
            if (O.TryParse(cl_args, cur, ref rc, message, errorMessage)) continue;
            if (cur >= cl_args.Count) continue;


            if (cl_args[cur].StartsWith("-"))
            {
                if (cl_args[cur] == "-log")
                {
                    log = true;
                    cl_args.RemoveAt(cur);
                    continue;
                }
                
                if (cl_args[cur] == "-concise")
                {
                    concise = true;
                    cl_args.RemoveAt(cur);
                    continue;
                }

                if (cl_args[cur] == "-show")
                {
                    show = true;
                    cl_args.RemoveAt(cur);
                    continue;
                }

                if (cl_args[cur] == "-dump")
                {
                    dump = true;
                    cl_args.RemoveAt(cur);
                    continue;
                }
                
                if (cl_args[cur] == "-iter")
                {
                    cl_args.RemoveAt(cur);

                    if (cur >= cl_args.Count)
                    {
                        errorMessage($"Option \'-iter\' missing parameter");
                        rc = -1;
                        continue;
                    }

                    string val = cl_args[cur];
                    cl_args.RemoveAt(cur);

                    if (!long.TryParse(val, out long iter_val))
                    {
                        errorMessage($"-iter could not parse {val} as long integer");
                        rc = -1;
                        continue;
                    }

                    Func<bool> condition = () => --iter_val >= 0;
                    Func<bool>? prev = runCondition;

                    message($"-iter     Will run for at most {iter_val} iterations");

                    runCondition = prev == null
                        ? condition
                        : () =>
                        {
                            //  We must ensure both are evaluated so conditions don't stack.
                            bool A = condition();
                            bool B = prev();

                            return A && B;
                        };



                    continue;
                }

                if (cl_args[cur] == "-stopwatch")
                {
                    cl_args.RemoveAt(cur);

                    if (cur >= cl_args.Count)
                    {
                        errorMessage($"Option \'-stopwatch\' missing parameter");
                        rc = -1;
                        continue;
                    }

                    string val = cl_args[cur];
                    cl_args.RemoveAt(cur);

                    if (!TimeSpan.TryParse(val, out TimeSpan time_val))
                    {
                        errorMessage($"-stopwatch could not parse {val} as TimeSpan");
                        rc = -1;
                        continue;
                    }

                    DateTime start = DateTime.Now;
                    Func<bool> condition = () => DateTime.Now - start <= time_val;
                    Func<bool>? prev = runCondition;
                    
                    message($"-stopwatch Will run for at most {time_val}");

                    runCondition = prev == null
                        ? condition
                        : () =>
                        {
                            //  We must ensure both are evaluated so conditions don't stack.
                            bool A = condition();
                            bool B = prev();

                            return A && B;
                        };
                    
                    continue;
                }

                if (cl_args[cur] == "-create")
                {
                    createIfDoesntExsist = true;
                    cl_args.RemoveAt(cur);
                    continue;
                }

                if (cl_args[cur] == "-record")
                {
                    record = true;
                    cl_args.RemoveAt(cur);
                    continue;
                }

                if (cl_args[cur] == "-help" || cl_args[cur] == "-h")
                {
                    cl_args.RemoveAt(cur);
                    showHelp = true;
                    continue;
                }

                errorMessage($"Unknown argument: {cl_args[cur]}");
                cl_args.RemoveAt(cur);

                rc = -1;
                showHelp = true;
                continue;
            }
            else cur++;
        }

        if (cl_args.Count() != 1)
        {
            errorMessage("expected one file path");
            showHelp = true;
            rc = -1;
        }
        
        if (O.WantExit)
            return rc;

        if (showHelp || rc != 0)
        {
            message("Usage: TruthInTheFlip [options] <filepath>");
            message();
            message("Arguments:");
            message("  <filepath>          Path to the tracker state file (required)");
            message();
            message("Options:");
            message("  -log                Enable detailed logging output");
            message("  -show               show stats or log tail");
            message("  -create             Create the state file if it doesn't exist");
            message("  -dump               verbose output");
            message("  -record             Append records to the state file");
            message("  -concise            Prefer skinier output");
            message("  -iter <integer>     run x iterations and exit");
            message("  -stopwacth <time>   run for x amount of time(example 1:0:0 for 1hr) and exit ");
            message(O.GetHelp(), false);
            message("  -help, -h           Display this help message");
            message();
            message("Description:");
            message("  This program runs a high-performance multithreaded meta-guessing simulation");
            message("  to test negentropy hypotheses against massive random bit sequences.");
            message("  It continuously processes billions of flips and tracks statistical significance");
            message("  via Z-score calculations, saving state periodically to the specified file.");
            message("  The recomended extension is .tkr or .TrackerRecord.");
            message("  **This program runs perpetually by default see -iter or -stopwatch");
            message("Example: (bash)");
            message("  ./TruthInTheFlip /PathToYourTracker/YourTracker.tkr -create -record -rsource NET2 -window def");
                        
            return rc;
        }

        Func<Action<byte[]>> seedFunc = BitFactory.initRandom_Net;
        if (rsourceOption.Enabled)
            seedFunc = UtilT.ThrowIfNull(rsourceOption.SeedFunc, "rsource enabled but not providing SeedFunc");
        bitFactory.resetRandom = seedFunc;
        bitFactory.Reset();
        
        
        fileName = cl_args[0];

        if (!windowOption.Enabled)
            errorMessage("warning viewing without window expect drift. Enable a window with -window def");

        store = TrackerStore.Default(fileName);
        store.concise = concise;

        //v1.0.1 compat
        // store.print_delegate = (store, tracker) =>
        // {
        //     if (!(tracker is Tracker t)) throw new IOException();
        //     
        //     string threadTime = $"threadTime: {new TimeSpan(t.cumulativeTicks):G}";
        //     return ($"{t.Source.total} flips → " +
        //             $"positive: {Tracker.FormatOffset(t.HeadsPercentage, "0.0e+00")} | " +
        //             $"negative: {Tracker.FormatOffset(t.TailsPercentage, "0.0e+00")} | " +
        //             $"anticipatedPositive: {Tracker.FormatOffset(t.AnticipatedHeadsPercentage, "0.0e+00")} | " +
        //             $"anticipatedNegative: {Tracker.FormatOffset(t.AnticipatedTailsPercentage, "0.0e+00")} | " +
        //             $"anticipated: {Tracker.FormatOffset(t.AnticipatedPercentage, "0.0e+00")} | " +
        //             $"base: {Tracker.FormatOffset(t.BaseAnticipatedPercentage, "0.0e+00")} | " +
        //             $"Z: {t.GetCurrentZScore():F6} | {threadTime}");
        // };

        if (dump)
        {
            long cnt = 0;
            long good = 0;

            foreach (var t1 in store.Enumerate())
            {
                Tracker t = (Tracker)t1;
                message(store.Print(t));
                cnt++;
                if (t.anticipated << 1 >= t.total) good++;
            }

            message($"{(good / (double)cnt * 100)}% of the time above 50%");
            return 0;
        }

        if (!(show && log))
        {
            allTime = store.NewTracker();
            if (File.Exists(fileName))
            {
                allTime = store.LoadOrCreate(null);

                if (((TrackerStore)store).Record != record)
                {
                    errorMessage(
                        $"{fileName} : Record ={((TrackerStore)store).Record} but -record switch {(record ? "was" : "wasn't")} passed");
                    if (record && !store.Record)
                        errorMessage(
                            "create new file with -record and -create among the switches if you desire to record");
                    return 1;
                }


                if (show)
                {
                    message(store.Print(allTime));
                    return 0;
                }
            }
            else if (!createIfDoesntExsist)
            {
                errorMessage($"File does not exist: {fileName}");
                errorMessage("Use -create to create a new state file");
                return 1;
            }
            else
            {
                store.Record = record;
                store.Version = TrackerStore.latest;
            }
        }
        else
        {
            if (!File.Exists(fileName + ".log"))
            {
                errorMessage($"the log file \"{fileName + ".log"}\" does not exist.");
                return 1;
            }

            Queue<string> lines = new();
            using (var stream = new StreamReader(fileName + ".log"))
            {
                string? line;
                while ((line = stream.ReadLine()) != null)
                {
                    lines.Enqueue(line);
                    if (lines.Count > 20) lines.Dequeue();
                }

                foreach (var l in lines) message(l);
                return 0;
            }

        }

        int[] ver = UtilT.ThrowIfNull(TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version),
            "unsupported version");

        bool validate_error = false;
        validate_error = !O.ValidateVersion(UtilT.ThrowIfNull(store.Version,"Store.Version"), errorMessage) || validate_error;
        
        if (validate_error) return -1;

        if (!record) message($"warning {fileName} started without record, history not being saved");

        if (infoOption.Enabled)
        {
            message("=== Run Configuration Info ===");
            message(UtilT.PadRight("File:") + fileName);
            message(UtilT.PadRight("record:") + store.Record);

            message(UtilT.PadRight("Supports:") + "TruthInTheFlip.v" + TrackerStore.VersionPrint(supported_ver));

            // Quick peek at the file metadata (without running the full Enumerate)
            if (store.Version != null)
            {
                Tracker lastRecord = (Tracker)UtilT.ThrowIfNull(allTime, "allTime must be Tracker");
                Tracker firstRecord = File.Exists(store.Path) ? (Tracker)store.Enumerate().First() : lastRecord;

                message(UtilT.PadRight($"Tracker Version:") + TrackerStore.VersionPrint(ver));
                // You could load the tail here just to print the total lifetime flips/times
                message(UtilT.PadRight("Total Flips:") + $"{lastRecord.total:N0}");

                if (File.Exists(store.Path))
                {
                    if (TrackerStore.VersionCompare(ver, 1, 1, 0) >= 0)
                    {
                        message(UtilT.PadRight("Start Time:") + firstRecord.UtcBeginTime + " UTC");
                        message(UtilT.PadRight("End Time:") + lastRecord.UtcEndTime + " UTC");
                    }
                    else message("use newer file version for timing info");
                }
                else message("creating file");


                message();
            }

            message("[Options]");
            Console.Write(O.Info());
            message("==============================\n");
        }

        TrackerWindow? window = null;
        if (windowOption.Enabled) 
        {
            window = new TrackerWindow(store,
                UtilT.ThrowIfNull(windowOption.Strategy, "windowOption.Strategy"));
            //if (window != null) foreach (Tracker t in store.Enumerate()) window.Add(t);
            
            window.States.PopHead();   //we won't be using the default 'zero' entry
            bool filled = false;
            foreach (ITracker t in store.ReverseEnumerate()) //This design pattern can be used to compose code to prime multiple tracker windows with relative ease.
            {
                filled = window.ReverseAdd((Tracker)t);
                if (filled) break;
            }
            
            if (!filled) { window.States.AddHead(new UtilT.LinkNode<Tracker>((Tracker)store.NewTracker())); }
        }

        TrackerRunner runner = new TrackerRunner(store, bitFactory);
        //runner.anticipate_delegate = runner.MakeAnticipateDelegate(AnticipationStrategies.AlternatingMetaGuess());
        if (anticipationOption.Enabled)
        {
            runner.anticipate_delegate = runner.MakeAnticipateDelegate(UtilT.ThrowIfNull(anticipationOption.Strategy, "anticipationOption.Strategy"));
        }
        
        
        
        while (runCondition == null || runCondition())
        {
            runner.Run(allTime, 20, 10000000);
            store.Save(allTime, record);
            Tracker current = window == null ? (Tracker)allTime : window.Add((Tracker)allTime);

            LogWriteLine(Program.RecordText(current, ""));
        }

        return 0;
    }
}