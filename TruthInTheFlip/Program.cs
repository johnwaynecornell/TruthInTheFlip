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

        List<String> args = new(command_line_args);

        bool show = false;
        bool dump = false;
        bool createIfDoesntExsist = false;
        bool concise = false;

        bool showHelp = false;

        int cur = 0;
        while (cur < args.Count)
        {
            if (args[cur].StartsWith("-"))
            {
                if (args[cur] == "-log")
                {
                    log = true;
                    args.RemoveAt(cur);
                    continue;
                }
                
                if (args[cur] == "-concise")
                {
                    concise = true;
                    args.RemoveAt(cur);
                    continue;
                }

                if (args[cur] == "-show")
                {
                    show = true;
                    args.RemoveAt(cur);
                    continue;
                }

                if (args[cur] == "-dump")
                {
                    dump = true;
                    args.RemoveAt(cur);
                    continue;
                }


                if (args[cur] == "-create")
                {
                    createIfDoesntExsist = true;
                    args.RemoveAt(cur);
                    continue;
                }

                if (args[cur] == "-record")
                {
                    record = true;
                    args.RemoveAt(cur);
                    continue;
                }

                if (args[cur] == "-help" || args[cur] == "-h" || args[cur] == "/?" || args[cur] == "/h" ||
                    args[cur] == "/help")
                {
                    args.RemoveAt(cur);
                    showHelp = true;
                    continue;
                }

                Console.Error.WriteLine($"Unknown argument: {args[cur]}");
                args.RemoveAt(cur);
                showHelp = true;
                continue;
            }
            else cur++;
        }

        if (args.Count() != 1)
        {
            Console.Error.WriteLine("expected one file path");
            showHelp = true;
        }

        if (showHelp)
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
            Console.WriteLine("  -help, -h, /?, /h, /help");
            Console.WriteLine("                      Display this help message");
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  This program runs a high-performance multithreaded meta-guessing simulation");
            Console.WriteLine("  to test negentropy hypotheses against massive random bit sequences.");
            Console.WriteLine("  It continuously processes billions of flips and tracks statistical significance");
            Console.WriteLine("  via Z-score calculations, saving state periodically to the specified file.");
            Console.WriteLine("  The recomended extension is .tkr or .TrackerRecord.");

            return 0;
        }

        fileName = args[0];

        store = TrackerStore.Default(fileName);
        store.concise = concise;
        
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
                Console.Error.WriteLine($"File does not exist: {fileName}");
                Console.Error.WriteLine("Use -create to create a new state file");
                return 1;
            }
        }
        else
        {
            if (!File.Exists(fileName + ".log"))
            {
                Console.Error.WriteLine($"the log file \"{fileName + ".log"}\" does not exist.");
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