using TruthInTheFlip.Format;

public class Program
{
    public static int Main(string[] command_line_args)
    {

        List<String> args = new(command_line_args);

        bool showHelp = false;
        if (args.Count() != 1 || args[0].StartsWith("-"))
        {
            Console.Error.WriteLine("expected one file path");
            showHelp = true;
        }

        if (showHelp)
        {
            Console.WriteLine("Usage: TruthInTheFlip_sample_csv");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <filepath>          Path to the tracker state file (required)");
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  This program outputs csv with a header for the file version.");
            
            return 0;
        }

        string fileName = args[0];

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
        // ver does not have to be verified here. That was done in Default.       
        if (ver == null) throw new NullReferenceException();
        
        Console.Write("total,heads,tails,anticipated,baseAnticipated,anticipatedHeads,anticipatedTails,cumulativeTicks");
        if (TrackerStore.VersionCompare(ver,1,1,0) >= 0)
            Console.Write(",batchTotal,wallclockTimeNs,batchWallclockTimeNs,utcBeginTimeMs,utcEndTimeMs,betHeads,betSame,anticipatedSame");
        Console.WriteLine();
        
        foreach (var t1 in store.Enumerate())
        {
            Tracker t = (Tracker)t1;
            Console.Write($"{t.total},{t.heads},{t.tails},{t.anticipated},{t.baseAnticipated},{t.anticipatedHeads},{t.anticipatedTails},{t.cumulativeTicks}");
    
            if (TrackerStore.VersionCompare(ver,1,1,0) >= 0)
                Console.Write($",{t.batchTotal},{t.wallclockTimeNs},{t.batchWallclockTimeNs},{t.utcBeginTimeMs},{t.utcEndTimeMs},{t.betHeads},{t.betSame},{t.anticipatedSame}");
            Console.WriteLine();
        }
        return 0;
    }
}
