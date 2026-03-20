// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using TruthInTheFlip;

public class Program
{
    public class ForScope
    {
        public BitFactory bitFactory;
        public BitFactory.Consumer consume;
        public Tracker run;

        public ForScope(BitFactory bitFactory)
        {
            this.bitFactory = bitFactory;
            consume = new BitFactory.Consumer(bitFactory);

            run = new Tracker();
            //initialize the tracker to get the first valid guess to be fair statistically.
            run.Anticipate(consume.getBit());
            run.Anticipate(consume.getBit());
            run.Reset(); // This deliberately does not reset the prior flip memory or guess.

        }
    }

    public static string fileName = null;
    public static Tracker allTime;

    public static bool log = false;
    public static bool record = false;

    public static void LogWrite(string text)
    {
        Console.Write(text);
        
        if (log)
        {
            using (var fs = new FileStream(fileName+".log", FileMode.Append))
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
            using (var fs = new FileStream(fileName+".log", FileMode.Append))
            using (var sw = new StreamWriter(fs))
            {
                sw.WriteLine(text);
            }
        }
    }
    
    public static string RecordText(string tail)
    {
        string threadTime = $"threadTime: {new TimeSpan(allTime.cumulativeTicks):G}";
        return (allTime.ToString() + $" | {threadTime+tail}");
    }
    
    public static int Main(string[] command_line_args)
    {
        BitFactory bitFactory = new BitFactory();

        //fillArray = QuantisInterop.initQuantis_Linux();

        List<String> args = new(command_line_args);
        
        bool show = false;
        bool createIfDoesntExsist = false;

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

                if (args[cur] == "-show")
                {
                    show = true;
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
            Console.WriteLine("  -record             Append records to the state file");
            Console.WriteLine("  -help, -h, /?, /h, /help");
            Console.WriteLine("                      Display this help message");
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  This program runs a high-performance multithreaded meta-guessing simulation");
            Console.WriteLine("  to test negentropy hypotheses against massive random bit sequences.");
            Console.WriteLine("  It continuously processes billions of flips and tracks statistical significance");
            Console.WriteLine("  via Z-score calculations, saving state periodically to the specified file.");
            return 0;
        }
        fileName = args[0];

        if (!(show && log))
        {
            allTime = new Tracker();
            if (File.Exists(fileName))
            {
                // foreach (var t in Tracker.Enumerate(fileName))
                // {
                //     string threadTime = $"threadTime: {new TimeSpan(t.cumulativeTicks):G}";
                //     Console.WriteLine(t.ToString() + $" | {threadTime}");
                // }
                
                allTime = Tracker.SafeLoad(fileName, record);
            }
            else if (!createIfDoesntExsist)
            {
                Console.Error.WriteLine($"File does not exist: {fileName}");
                Console.Error.WriteLine("Use -create to create a new state file");
                return 1;
            }
            
            if (show)
            {
                Console.WriteLine(RecordText(""));
                return 0;
            }
        } else
        {
            if (!File.Exists(fileName+".log")) 
            {
                Console.Error.WriteLine($"the log file \"{fileName+".log"}\" does not exist.");
                return 1;
            }
            
            Queue<string> lines = new();
            using (var stream = new StreamReader(fileName+".log"))
            {
                string line;
                while ((line =stream.ReadLine()) != null)
                {
                    lines.Enqueue(line);
                    if (lines.Count > 20) lines.Dequeue();
                }
                
                foreach (var l in lines) Console.WriteLine(l);
                return 0;
            }
            
        }

        while (true)
        {
            DateTime begin = DateTime.Now;
            long begin_count = allTime.total;

            Parallel.For(
                0, 20,
                () => new ForScope(bitFactory), // 1. localInit: Runs once per thread to initialize the state
                (index, loopState, scope) =>
                {
                    // 2. body: Runs for each iteration, using the thread-local state

                    long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

                    try
                    {
                        for (int i = 0; i < 10000000; i++)
                        {
                            bool current = scope.consume.getBit();
                            scope.run.Anticipate(current);
                        }
                    }
                    finally
                    {
                        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp);
                        scope.run.cumulativeTicks += elapsed.Ticks;
                    }

                    return scope; // Pass the state to the next iteration on this thread
                },
                (scope) =>
                {
                    // 3. localFinally: Runs once per thread after all its iterations are done
                    lock (allTime) allTime.Add(scope.run);
                });

            DateTime end = DateTime.Now;
            long end_count = allTime.total;
            
            allTime.Save(fileName, record);
            
            var proof1 = allTime.EstimateRemainingFlipsForZScore(1.96);
            var proof2 = allTime.EstimateRemainingFlipsForZScore(3.00);

            double fps = (end_count - begin_count) / (end - begin).TotalSeconds;

            TimeSpan ts1 = new TimeSpan((long)((proof1 / fps) * TimeSpan.TicksPerSecond));
            TimeSpan ts2 = new TimeSpan((long)((proof2 / fps) * TimeSpan.TicksPerSecond));

            string project1 = proof1 == -1 ? "unreachable" : $"in {ts1:G}";
            string project2 = proof2 == -1 ? "unreachable" : $"in {ts2:G}";

            LogWriteLine(Program.RecordText($" | flips/sec: {fps:F4} | Z(1.96) {project1} | Z(3.00) {project2}"));
        }
    }
}