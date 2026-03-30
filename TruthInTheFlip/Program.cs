using TruthInTheFlip.Format;
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
        Console.WriteLine(text);

        if (log)
        {
            using (var fs = new FileStream(fileName + ".log", FileMode.Append))
            using (var sw = new StreamWriter(fs))
            {
                sw.WriteLine(text);
            }
        }
    }

    public static string RecordText(string tail)
    {
        if (store == null || allTime == null) throw new NullReferenceException("Program is uninitialized");
        
        return (store.Print(allTime) + (tail == "" ? "" : $" | {tail}"));
    }

    public static int Main(string[] command_line_args)
    {
        BitFactory bitFactory = new BitFactory();

        //fillArray = QuantisInterop.initQuantis_Linux();

        List<String> cl_args = new(command_line_args);

        Options O = new Options();
        
        Action<String> errorWriteLine = (s) => Console.Error.WriteLine(s);
        
        bool show = false;
        bool dump = false;
        bool createIfDoesntExsist = false;
        bool concise = false;

        bool showHelp = false;
        int rc = 0;
        
        string randomSource = "NET1";
        
        int cur = 0;
        while (cur < cl_args.Count)
        {
            if (O.TryParse(cl_args, cur, ref rc, errorWriteLine)) continue;
            if (cur >= cl_args.Count) continue;

            
            if (cl_args[cur].StartsWith("-"))
            {
                if (cl_args[cur] == "-log")
                {
                    log = true;
                    cl_args.RemoveAt(cur);
                    continue;
                }

                if (cl_args[cur] == "-rsource")
                {
                    cl_args.RemoveAt(cur);
                    if (cur >= cl_args.Count || cl_args[cur].StartsWith("-"))
                    {
                        errorWriteLine($"Expected random source string after -rsource");
                        showHelp = true;
                        rc = -1;
                        continue;
                    }

                    randomSource = cl_args[cur];
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

                errorWriteLine($"Unknown argument: {cl_args[cur]}");
                cl_args.RemoveAt(cur);
                
                rc = -1;
                showHelp = true;
                continue;
            }
            else cur++;
        }

        if (cl_args.Count() != 1)
        {
            errorWriteLine("expected one file path");
            showHelp = true;
            rc = -1;
        }

        switch (randomSource)
        {
            case "list":
                Console.WriteLine("Random sources:");
                Console.WriteLine("  NET1            System.Random");
                Console.WriteLine("  NET2            System.Security.Cryptography");
                return rc;
            case "NET1":
                bitFactory.resetRandom = BitFactory.initRandom_Net;
                bitFactory.Reset();
                break;
            case "NET2":
                bitFactory.resetRandom = () => (arr) => System.Security.Cryptography.RandomNumberGenerator.Fill(arr);
                bitFactory.Reset();
                break;
            default:
            {
                errorWriteLine($"random source \"{randomSource}\" UNKNOWN");
                return -1;
            }
        }
        
        if (showHelp || rc != 0)
        {
            Console.WriteLine("Usage: TruthInTheFlip [options] <filepath>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <filepath>          Path to the tracker state file (required)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -log                Enable detailed logging output");
            Console.WriteLine("  -show               show stats or log tail");
            Console.WriteLine("  -create             Create the state file if it doesn't exist");
            Console.WriteLine("  -dump               verbose output");
            Console.WriteLine("  -record             Append records to the state file");
            Console.WriteLine("  -concise            Prefer skinier output");
            Console.WriteLine("  -rsource <string>   Random source string (default: NET1)");
            Console.WriteLine("  -rsource list       List random sources");
            Console.WriteLine("  -help, -h           Display this help message");
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  This program runs a high-performance multithreaded meta-guessing simulation");
            Console.WriteLine("  to test negentropy hypotheses against massive random bit sequences.");
            Console.WriteLine("  It continuously processes billions of flips and tracks statistical significance");
            Console.WriteLine("  via Z-score calculations, saving state periodically to the specified file.");
            Console.WriteLine("  The recomended extension is .tkr or .TrackerRecord.");
            
            return rc;
        }
        
        fileName = cl_args[0];

        store = TrackerStore.Default(fileName);
        store.concise = concise;
        
        int[] ver = UtilT.ThrowIfNull(TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version), "unsupported version");
        
        bool validate_error = false;
        foreach (Option o in O)
        {
            if (o is TrackerOption tracker_o) validate_error = !tracker_o.ValidateVersion(ver, errorWriteLine) || validate_error;
        }

        if (validate_error) return -1;

        
        //v1.0.1 compat
        // store.print_delegate = (store, tracker) =>
        // {
        //     if (!(tracker is Tracker t)) throw new IOException();
        //     
        //     string threadTime = $"threadTime: {new TimeSpan(t.cumulativeTicks):G}";
        //     return ($"{t.total} flips → " +
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
                Console.WriteLine(store.Print(t));
                cnt++;
                if (t.anticipated << 1 >= t.total) good++;
            }

            Console.WriteLine($"{(good / (double)cnt * 100)}% of the time above 50%");
            return 0;
        }

        if (!(show && log))
        {
            allTime = store.NewTracker();
            if (File.Exists(fileName))
            {
                allTime = store.LoadOrCreate(show ? null : record);

                if (show)
                {
                    Console.WriteLine(store.Print(allTime));
                    return 0;
                }
            }
            else if (!createIfDoesntExsist)
            {
                errorWriteLine($"File does not exist: {fileName}");
                errorWriteLine("Use -create to create a new state file");
                return 1;
            }
        }
        else
        {
            if (!File.Exists(fileName + ".log"))
            {
                errorWriteLine($"the log file \"{fileName + ".log"}\" does not exist.");
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

                foreach (var l in lines) Console.WriteLine(l);
                return 0;
            }

        }

        TrackerRunner runner = new TrackerRunner(store, bitFactory);

        while (true)
        {
            runner.Run(allTime, 20, 10000000);
            store.Save(allTime, record);
            LogWriteLine(Program.RecordText(""));
        }
    }
}