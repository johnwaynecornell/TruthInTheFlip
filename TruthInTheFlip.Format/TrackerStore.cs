namespace TruthInTheFlip.Format;

public interface ITrackerStore
{
    string? Path { get;set;}
    
    string? Version { get;set;}

    bool Record {get;set;}
    
    ITracker NewTracker();
    string Print(ITracker tracker);

    ITracker LoadOrCreate(bool? record = false);
    /// <summary>
    /// Lazily enumerates through the historical tracker records in the store.
    /// </summary>
    /// <remarks>
    /// ⚠️ **WARNING:** This method holds an open read lock on the underlying file stream 
    /// while enumerating. If you do not consume the entire sequence (e.g., by breaking out 
    /// of a foreach loop early, or using LINQ methods like .First()), you MUST ensure the 
    /// resulting IEnumerator is properly disposed to release the file lock. 
    /// Standard <c>foreach</c> loops handle this automatically.
    /// </remarks>
    /// <returns>An enumerable stream of historical tracker states.</returns>
    IEnumerable<ITracker> Enumerate();
    void Save(ITracker tracker, bool record = false);
}


public class TrackerStore : ITrackerStore
{
    public delegate ITracker NewTrackerDelegate(ITrackerStore store);

    public delegate string PrintDelegate(ITrackerStore store, ITracker tracker);

    public delegate ITracker LoadOrCreateDelegate(ITrackerStore store, bool? record = false);

    public delegate IEnumerable<ITracker> EnumerateDelegate(ITrackerStore store);

    public delegate void SaveDelegate(ITrackerStore store, ITracker tracker, bool record = false);

    public delegate ITracker ReadRecordDelegate(ITrackerStore store, string version, BinaryReader reader);

    public delegate void WriteRecordDelegate(ITrackerStore store, ITracker tracker, string version,
        BinaryWriter writer);

    public NewTrackerDelegate? newTracker_delegate;
    public PrintDelegate? print_delegate;
    public LoadOrCreateDelegate? loadOrCreate_delegate;
    public EnumerateDelegate? enumerate_delegate;
    public SaveDelegate? save_delegate;

    public ReadRecordDelegate? readRecord_delegate;
    public WriteRecordDelegate? writeRecord_delegate;

    public EnumerateDelegate? reverse_enumerate_delegate;
    
    public bool Record { get; set; }

    public string? Path { get; set; }

    public string? Version { get; set; }

    public bool concise { get; set; }

    public virtual ITracker NewTracker()
    {
        if (newTracker_delegate == null) throw new Exception("TrackerStore 'newTracker_delegate' not initialized");
        return newTracker_delegate(this);
    }

    public virtual string Print(ITracker tracker)
    {
        if (print_delegate == null) throw new Exception("TrackerStore 'print_delegate' not initialized");
        return print_delegate(this, tracker);
    }

    public virtual ITracker LoadOrCreate(bool? record = false)
    {
        if (loadOrCreate_delegate == null) throw new Exception("TrackerStore 'loadOrCreate_delegate' not initialized");
        return loadOrCreate_delegate(this, record);
    }

    /// <summary>
    /// Executes a file operation with a transient retry mechanism. 
    /// Perfect for overcoming brief file locks caused by antivirus or concurrent readers.
    /// </summary>
    protected virtual T RetryIO<T>(Func<T> operation, int maxAttempts = 12, int delayMs = 500)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                return operation();
            }
            catch (IOException) // IOException specifically catches "File In Use" errors
            {
                if (i == maxAttempts - 1) 
                    throw; // If we are on the last attempt, rethrow to preserve the stack trace
                
                System.Threading.Thread.Sleep(delayMs);
            }
        }
        
        return default!; // The compiler requires this, but we will always return or throw above.
    }

    /// <summary>
    /// Safely opens a file stream using a retry mechanism and allows concurrent readers.
    /// </summary>
    public virtual Stream NewFileStream(string path, FileMode mode, FileAccess access = FileAccess.ReadWrite,
        FileShare share = FileShare.Read)
    {
        return RetryIO(() => new FileStream(path, mode, access, share));
    }

    /// <summary>
    /// Lazily enumerates through the historical tracker records in the store.
    /// </summary>
    /// <remarks>
    /// ⚠️ **WARNING:** This method holds an open read lock on the underlying file stream 
    /// while enumerating. If you do not consume the entire sequence (e.g., by breaking out 
    /// of a foreach loop early, or using LINQ methods like .First()), you MUST ensure the 
    /// resulting IEnumerator is properly disposed to release the file lock. 
    /// Standard <c>foreach</c> loops handle this automatically.
    /// </remarks>
    /// <returns>An enumerable stream of historical tracker states.</returns>
    public virtual IEnumerable<ITracker> Enumerate()
    {
        if (enumerate_delegate == null) throw new Exception("TrackerStore 'enumerate_delegate' not initialized");
        return enumerate_delegate(this);
    }

    public virtual IEnumerable<ITracker> ReverseEnumerate()
    {
        if (reverse_enumerate_delegate == null) throw new Exception("TrackerStore 'reverse_enumerate_delegate' not initialized");
        return reverse_enumerate_delegate(this); 
    }


    public virtual void Save(ITracker tracker, bool record = false)
    {
        if (save_delegate == null) throw new Exception("TrackerStore 'save_delegate' not initialized");
        save_delegate(this, tracker, record);
    }

    public virtual ITracker ReadRecord(string version, BinaryReader reader)
    {
        if (readRecord_delegate == null) throw new Exception("TrackerStore 'readRecord_delegate' not initialized");
        return readRecord_delegate(this, version, reader);
    }

    public virtual void WriteRecord(ITracker tracker, string version, BinaryWriter writer)
    {
        if (writeRecord_delegate == null) throw new Exception("TrackerStore 'writeRecord_delegate' not initialized");
        writeRecord_delegate(this, tracker, version, writer);
    }


    public static int[]? ReadVersion(string title, string? version)
    {
        if (version == null || !version.StartsWith(title)) return null;

        string[] parts = version.Substring(title.Length).Split('.');
        if (parts.Length < 2) return null;

        int[] version0 = new int[3];

        if (!int.TryParse(parts[0], out version0[2])) return null;
        if (!int.TryParse(parts[1], out version0[1])) return null;
        if (parts.Length > 2)
        {
            if (!int.TryParse(parts[2], out version0[0])) return null;
        }

        return version0;
    }

    public static int VersionCompare(int[] versionA, int major, int minor, int patch)
    {
        if (versionA[2] < major) return -1;
        if (versionA[2] > major) return 1;

        if (versionA[1] < minor) return -1;
        if (versionA[1] > minor) return 1;

        if (versionA[0] < patch) return -1;
        if (versionA[0] > patch) return 1;

        return 0;
    }

    public static int VersionCompare(int[] versionA, int[] versionB)
    {
        for (int i = 2; i >= 0; i--)
        {
            if (versionA[i] < versionB[i]) return -1;
            if (versionA[i] > versionB[i]) return 1;
        }
        
        return 0;
    }

    public static int[] VersionArray(int major, int minor, int patch)
    {
        return new int[] {patch, minor, major};
    }

    public static string VersionPrint(int[] version)
    {
        return $"{version[2]}.{version[1]}.{version[0]}";
    }


    public static bool VersionAtLeast(string title, string version, int major, int minor, int patch)
    {
        if (string.IsNullOrEmpty(version)) return false;
        int[]? ver = ReadVersion(title, version);
        if (ver == null) return false;

        return VersionCompare(ver, major, minor, patch) >= 0;
    }

    #region Stock

    public static TrackerStore Default(string fileName)
    {
        TrackerStore store = new TrackerStore();
        store.newTracker_delegate = StockNewTracker;
        store.print_delegate = StockPrint;
        store.loadOrCreate_delegate = StockLoadOrCreate;
        store.save_delegate = StockSave;
        store.enumerate_delegate = StockEnumerate;
        store.reverse_enumerate_delegate = StockReverseEnumerate;
        
        store.readRecord_delegate = StockReadRecord;
        store.writeRecord_delegate = StockWriteRecord;

        store.Path = fileName;

        if (File.Exists(store.Path))
            using (var fs = ((TrackerStore)store).NewFileStream(store.Path, FileMode.Open))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                string version;
                version = reader.ReadString();

                int[]? ver = ReadVersion("TruthInTheFlip.v", version);
                if (ver == null || VersionCompare(ver, 1, 0, 0) < 0)
                    throw new IOException($"version \"{version}\" not handled");
                if (VersionCompare(ver, 1, 1, 0) > 0)
                    throw new Exception($"\"{store.Path}\" \"{version}\" is a newer than {latest}");


                if (version == "TruthInTheFlip.v1.0")
                {
                    store.Record = false;
                    store.Version = "TruthInTheFlip.v1.0.1";

                    return store;
                }

                if (VersionCompare(ver, 1, 1, 0) > 0)
                    throw new Exception($"\"{store.Path}\" \"{version}\" is a newer than {latest}");


                store.Version = version;
            }

        return store;
    }
    
    public virtual Tracker Clone(Tracker other)
    {
        Tracker tracker = (Tracker) NewTracker();
        tracker.total = other.total;        
        tracker.heads = other.heads;
        tracker.tails = other.tails;
        tracker.anticipated = other.anticipated;

        tracker.baseAnticipated = other.baseAnticipated;
        tracker.anticipatedHeads = other.anticipatedHeads;
        tracker.anticipatedTails = other.anticipatedTails;
        tracker.cumulativeTicks = other.cumulativeTicks;
        //v1.1.0 additions
        tracker.batchTotal = other.batchTotal;
        tracker.wallclockTimeNs = other.wallclockTimeNs;
        tracker.batchWallclockTimeNs = other.batchWallclockTimeNs;
        tracker.utcBeginTimeMs = other.utcBeginTimeMs;
        tracker.utcEndTimeMs = other.utcEndTimeMs;
        tracker.betHeads = other.betHeads;
        tracker.betSame = other.betSame;
        tracker.anticipatedSame = other.anticipatedSame;
        
        return tracker;
    }

    public static string StockPrint(ITrackerStore store, ITracker tracker)
    {
        if (tracker == null) throw new NullReferenceException("You cannot print nothing.");
        Tracker? t = tracker as Tracker;
        if (t == null) return tracker.ToString() ?? "";

        if (store.Version == null) throw new Exception("TrackerStore 'store.Version' not initialized");

        int[]? ver = ReadVersion("TruthInTheFlip.v", store.Version);
        if (ver == null || VersionCompare(ver, 1, 0, 0) < 0)
            throw new IOException($"version \"{store.Version}\" not handled");

        string projectText = "";

        TrackerStore s = (TrackerStore)store;

        if (t.batchTotal != 0)
        {
            var proof1 = t.EstimateRemainingFlipsForZScore(1.96);
            var proof2 = t.EstimateRemainingFlipsForZScore(3.00);
            double durationSeconds = Math.Max(0.001, (t.Source.utcEndTimeMs - t.Source.utcBeginTimeMs) / 1000.0);
            double fps = t.batchTotal / durationSeconds;

            TimeSpan ts1 = new TimeSpan((long)((proof1 / fps) * TimeSpan.TicksPerSecond));
            TimeSpan ts2 = new TimeSpan((long)((proof2 / fps) * TimeSpan.TicksPerSecond));

            string project1 = proof1 == -1 ? "unreachable" : $"in {ts1:G}";
            string project2 = proof2 == -1 ? "unreachable" : $"in {ts2:G}";

            projectText = $" | flips/sec: {fps:F4} | Z(1.96) {project1} | Z(3.00) {project2}";
        }

        if (VersionCompare(ver, 1, 1, 0) >= 0)
        {
            TimeSpan wallclockSpan = new TimeSpan(t.Source.wallclockTimeNs / 100);

            string insert = s.concise
                ? ""
                : (
                    $"[RATE] H/T: {Tracker.FormatOffset(t.BetHeadsWinRate, "0.0e+00")}/ {Tracker.FormatOffset(t.BetTailsWinRate, "0.0e+00")} " +
                    $"S/D: {Tracker.FormatOffset(t.BetSameWinRate, "0.0e+00")}/ {Tracker.FormatOffset(t.BetDiffWinRate, "0.0e+00")} | ");

            return $"{t.Source.total} flips → " +
                   $"heads: {Tracker.FormatOffset(t.HeadsPercentage, "0.0e+00")} | " +
                   $"aHeads: {Tracker.FormatOffset(t.AnticipatedHeadsPercentage, "0.0e+00")} | " +
                   $"aTails: {Tracker.FormatOffset(t.AnticipatedTailsPercentage, "0.0e+00")} | " +
                   $"aSame: {Tracker.FormatOffset(t.AnticipatedSamePercentage, "0.0e+00")} | " +
                   $"aDiff: {Tracker.FormatOffset(t.AnticipatedDiffPercentage, "0.0e+00")} | " +
                   $"a: {Tracker.FormatOffset(t.AnticipatedPercentage, "0.0e+00")} | " +
                   $"base: {Tracker.FormatOffset(t.BaseAnticipatedPercentage, "0.0e+00")} | " +
                   $"Z: {t.ZScore:F6} | "
                   + insert +
                   //$"[BETS] H/T: {Tracker.FormatOffset(t.BetHeadsPercentage, "0.0e+00")}/{Tracker.FormatOffset(t.BetTailsPercentage, "0.0e+00")} " +
                   //$"S/D: {Tracker.FormatOffset(t.BetSamePercentage, "0.0e+00")}/{Tracker.FormatOffset(t.BetDiffPercentage, "0.0e+00")} | " +

                   $"threadTime: {new TimeSpan(t.Source.cumulativeTicks):G} | " +
                   $"wallclock: {wallclockSpan:G}" +
                   projectText;
        }

        return $"{t.Source.total} flips → " +
               $"heads: {Tracker.FormatOffset(t.HeadsPercentage, "0.0e+00")} | " +
               $"anticipatedHeads: {Tracker.FormatOffset(t.AnticipatedHeadsPercentage, "0.0e+00")} | " +
               $"anticipatedTails: {Tracker.FormatOffset(t.AnticipatedTailsPercentage, "0.0e+00")} | " +
               $"anticipated: {Tracker.FormatOffset(t.AnticipatedPercentage, "0.0e+00")} | " +
               $"base: {Tracker.FormatOffset(t.BaseAnticipatedPercentage, "0.0e+00")} | " +
               $"Z: {t.ZScore:F6} | " +
               $"threadTime: {new TimeSpan(t.Source.cumulativeTicks):G}" +
               projectText;

        // return $"{t.total} flips → " +
        //        $"positive: {t.HeadsPercentage:F4}% | " +
        //        $"negative: {t.TailsPercentage:F4}% | " +
        //        $"anticipatedPositive: {t.AnticipatedHeadsPercentage:F6}% | " +
        //        $"anticipatedNegative: {t.AnticipatedTailsPercentage:F6}% | " +
        //        $"anticipated: {t.AnticipatedPercentage:F6}% | " +
        //        $"base: {t.BaseAnticipatedPercentage:F6}% | " +
        //        $"Z: {t.GetCurrentZScore():F6}" +
        //        (t.trackerInner != null ? $" | INNER | {StockPrint(store, t.trackerInner)}" : "") + projectText;

    }

    public static ITracker StockNewTracker(ITrackerStore store)
    {
        return new Tracker((TrackerStore)store);
    }

    public static Tracker StockReadRecord(ITrackerStore store, string version, BinaryReader reader)
    {
        // Note this function presumes positioning within the underlying binary stream.

        int[]? ver = ReadVersion("TruthInTheFlip.v", version);
        if (ver == null || VersionCompare(ver, 1, 0, 0) < 0)
            throw new IOException($"version \"{version}\" not handled");

        Tracker tracker = (Tracker)store.NewTracker();
        Tracker? current;

        if (version == "TruthInTheFlip.v1.0")
        {
            current = tracker;

            while (current != null)
            {
                current.heads = reader.ReadInt64();
                current.tails = reader.ReadInt64();
                current.anticipated = reader.ReadInt64();
                current.baseAnticipated = reader.ReadInt64();

                current.total = reader.ReadInt64();
                current.anticipatedHeads = reader.ReadInt64();
                current.anticipatedTails = reader.ReadInt64();

                current.priorFlip = reader.ReadBoolean();
                current.guessAnticipateChange = reader.ReadBoolean();
                current.cumulativeTicks = reader.ReadInt64();

                if (current.heads + current.tails != current.total)
                    throw new IOException($"{store.Path} is damaged or incorrect");
                if (current.anticipatedHeads + current.anticipatedTails != current.baseAnticipated)
                    throw new IOException($"{store.Path} is damaged or incorrect");

                if (reader.ReadBoolean()) current = current.trackerInner = (Tracker)store.NewTracker();
                else current = null;

            }

            return tracker;
        }

        if (VersionCompare(ver, 1, 0, 1) < 0) throw new Exception($"reality check \"{version}\" not released");
        if (VersionCompare(ver, 1, 1, 0) > 0)
            throw new Exception($"\"{store.Path}\" \"{version}\" is a newer than {latest}");

        int sz = reader.ReadInt32();

        if (reader.ReadString() != "TrackerRecord")
            throw new IOException($"\"{store.Path}\" IOerror expected TrackerRecord");
        current = tracker;
        while (current != null)
        {
            current.heads = reader.ReadInt64();
            current.tails = reader.ReadInt64();
            current.anticipated = reader.ReadInt64();
            current.baseAnticipated = reader.ReadInt64();

            current.total = reader.ReadInt64();
            current.anticipatedHeads = reader.ReadInt64();
            current.anticipatedTails = reader.ReadInt64();

            current.priorFlip = reader.ReadBoolean();
            current.guessAnticipateChange = reader.ReadBoolean();
            current.cumulativeTicks = reader.ReadInt64();

            if (current.heads + current.tails != current.total)
                throw new IOException($"{store.Path} is damaged or incorrect");
            if (current.anticipatedHeads + current.anticipatedTails != current.baseAnticipated)
                throw new IOException($"{store.Path} is damaged or incorrect");

            if (VersionCompare(ver, 1, 1, 0) >= 0)
            {
                //new fields
                current.batchTotal = reader.ReadInt64();
                current.wallclockTimeNs = reader.ReadInt64();
                current.batchWallclockTimeNs = reader.ReadInt64();
                current.utcBeginTimeMs = reader.ReadInt64();
                current.utcEndTimeMs = reader.ReadInt64();

                current.betHeads = reader.ReadInt64();
                current.betSame = reader.ReadInt64();
                current.anticipatedSame = reader.ReadInt64();

            }


            if (reader.ReadBoolean()) current = current.trackerInner = (Tracker)store.NewTracker();
            else current = null;

        }

        int szEcho = reader.ReadInt32();
        if (szEcho != sz) throw new IOException($"\"{store.Path}\" error in validation");
        return tracker;
    }

    public static void StockWriteRecord(ITrackerStore store, ITracker tracker, string version, BinaryWriter writer)
    {
        int[]? ver = ReadVersion("TruthInTheFlip.v", version);
        if (ver == null || VersionCompare(ver, 1, 0, 0) < 0)
            throw new IOException($"version \"{version}\" not handled");

        bool use_new = TrackerStore.VersionCompare(ver, 1, 1, 0) >= 0;

        long loc = writer.Seek(0, SeekOrigin.Current);
        long loc2;

        writer.Write((int)0);

        writer.Write("TrackerRecord");

        for (Tracker? current = (Tracker)tracker; current != null; current = current.trackerInner)
        {
            writer.Write(current.heads);
            writer.Write(current.tails);
            writer.Write(current.anticipated);
            writer.Write(current.baseAnticipated);
            writer.Write(current.total);
            writer.Write(current.anticipatedHeads);
            writer.Write(current.anticipatedTails);

            writer.Write(current.priorFlip);
            writer.Write(current.guessAnticipateChange);
            writer.Write(current.cumulativeTicks);

            if (use_new)
            {
                writer.Write(current.batchTotal);
                writer.Write(current.wallclockTimeNs);
                writer.Write(current.batchWallclockTimeNs);
                writer.Write(current.utcBeginTimeMs);
                writer.Write(current.utcEndTimeMs);

                writer.Write(current.betHeads);
                writer.Write(current.betSame);
                writer.Write(current.anticipatedSame);
            }

            writer.Write(current.trackerInner != null);
        }

        loc2 = writer.Seek(0, SeekOrigin.Current);
        int size = (int)(loc2 - loc);
        writer.Seek(-size, SeekOrigin.Current);
        writer.Write((int)(size));
        writer.Seek(size - 4, SeekOrigin.Current);
        writer.Write((int)(size));
    }

    public const string latest = "TruthInTheFlip.v1.1.0";

    public static ITracker StockLoadOrCreate(ITrackerStore store, bool? record = false)
    {

        if (!File.Exists(store.Path)) return store.NewTracker();

        Tracker tracker;

        using (var fs = ((TrackerStore)store).NewFileStream(store.Path, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            string version;
            version = reader.ReadString();

            int[]? ver = ReadVersion("TruthInTheFlip.v", version);
            if (ver == null || VersionCompare(ver, 1, 0, 0) < 0)
                throw new IOException($"version \"{version}\" not handled");

            if (version == "TruthInTheFlip.v1.0")
            {
                if (record == true) throw new IOException($"\"{store.Path}\" record mode mismatch");

                tracker = (Tracker)((TrackerStore)store).ReadRecord(version, reader);
                store.Record = false;
                store.Version = "TruthInTheFlip.v1.0.1";
                return tracker;
            }

            store.Record = reader.ReadBoolean();
            if (record != null && store.Record != (bool)record)
                throw new IOException($"\"{store.Path}\" record mode mismatch");

            if (VersionCompare(ver, 1, 1, 0) > 0)
                throw new Exception($"\"{store.Path}\" \"{version}\" is a newer than {latest}");

            // We need to know if it's the latest version to not clobber recorded files.
            if (VersionCompare(ver, 1, 1, 0) < 0)
            {
                if (store.Record)
                {
                    store.Version = version;
                    Console.Error.WriteLine($"not latest file format using {store.Version} over {latest}");
                }
                else store.Version = latest;
            }
            else store.Version = latest;

            fs.Seek(-4, SeekOrigin.End);
            fs.Seek(-reader.ReadInt32() - 4, SeekOrigin.End);
            tracker = (Tracker)((TrackerStore)store).ReadRecord(version, reader);

        }

        return tracker;
    }

    public static IEnumerable<Tracker> StockEnumerate(ITrackerStore store)
    {
        if (!File.Exists(store.Path)) yield break;

        Tracker tracker;

        using (var fs = ((TrackerStore)store).NewFileStream(store.Path, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            string version;
            version = reader.ReadString();

            int[]? ver = ReadVersion("TruthInTheFlip.v", version);
            if (ver == null || VersionCompare(ver, 1, 0, 0) < 0)
                throw new IOException($"version \"{version}\" not handled");
            if (VersionCompare(ver, 1, 1, 0) > 0)
                throw new Exception($"\"{store.Path}\" \"{version}\" is a newer than {latest}");


            if (version == "TruthInTheFlip.v1.0")
            {
                tracker = (Tracker)((TrackerStore)store).ReadRecord(version, reader);
                store.Record = false;
                store.Version = "TruthInTheFlip.v1.0.1";

                yield return tracker;
                yield break;
            }

            if (VersionCompare(ver, 1, 1, 0) > 0)
                throw new Exception($"\"{store.Path}\" \"{version}\" is a newer than {latest}");


            store.Version = version;


            store.Record = reader.ReadBoolean();

            while (fs.Position != fs.Length)
            {
                tracker = (Tracker)((TrackerStore)store).ReadRecord(version, reader);
                yield return tracker;
            }
        }
    }

        public static IEnumerable<Tracker> StockReverseEnumerate(ITrackerStore store)
        {
            if (!File.Exists(store.Path)) yield break;
    
            Tracker tracker;
    
            using (var fs = ((TrackerStore)store).NewFileStream(store.Path, FileMode.Open))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                string version;
                version = reader.ReadString();
    
                int[]? ver = ReadVersion("TruthInTheFlip.v", version);
                if (ver == null || VersionCompare(ver, 1, 0, 0) < 0)
                    throw new IOException($"version \"{version}\" not handled");
                if (VersionCompare(ver, 1, 1, 0) > 0)
                    throw new Exception($"\"{store.Path}\" \"{version}\" is a newer than {latest}");
    
    
                if (version == "TruthInTheFlip.v1.0")
                {
                    tracker = (Tracker)((TrackerStore)store).ReadRecord(version, reader);
                    store.Record = false;
                    store.Version = "TruthInTheFlip.v1.0.1";
    
                    yield return tracker;
                    yield break;
                }
    
                if (VersionCompare(ver, 1, 1, 0) > 0)
                    throw new Exception($"\"{store.Path}\" \"{version}\" is a newer than {latest}");
    
    
                store.Version = version;
    
                long head = fs.Seek(0, SeekOrigin.Current);
    
                store.Record = reader.ReadBoolean();
    
                fs.Seek(-4, SeekOrigin.End);
                do
                {
                    int sz = reader.ReadInt32(); 
                    fs.Seek(-sz - 4, SeekOrigin.Current);
                    tracker = (Tracker)((TrackerStore)store).ReadRecord(version, reader);
                    fs.Seek(-sz - 8, SeekOrigin.Current);
                    
                    yield return tracker;
                } while (fs.Seek(0, SeekOrigin.Current) > head);
            }
        }

    
    
    public static void StockSave(ITrackerStore store, ITracker tracker, bool record = false)
    {
        if (store.Path == null) throw new Exception("TrackerStore 'store.Path' not initialized");

        Tracker t = (Tracker)tracker;

        store.Record = record;

        if (!File.Exists(store.Path))
        {
            using (var fs = ((TrackerStore)store).NewFileStream(store.Path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                writer.Write("TruthInTheFlip.v1.1.0");
                store.Version = "TruthInTheFlip.v1.1.0";
                writer.Write(record);
                ((TrackerStore)store).WriteRecord(tracker, store.Version, writer);
            }

            return;
        }

        using (var fs = ((TrackerStore)store).NewFileStream(store.Path, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            string version = reader.ReadString();

            switch (version)
            {
                case "TruthInTheFlip.v1.0":
                    if (record) throw new IOException("File version v1.0 does not support record");
                    break;
                case "TruthInTheFlip.v1.0.1":
                case "TruthInTheFlip.v1.1.0":
                    if (reader.ReadBoolean() != record)
                        throw new IOException("record mode mismatch between args and file");
                    break;
                default:
                    throw new IOException($"\"{version}\" unknown format");
            }
        }

        if (store.Version == null) throw new Exception("TrackerStore 'store.Path' not initialized");


        if (!record)
        {
            using (var fs = ((TrackerStore)store).NewFileStream(store.Path, FileMode.Truncate, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                writer.Write(store.Version);
                writer.Write(record);
                ((TrackerStore)store).WriteRecord(tracker, store.Version, writer);
            }

        }
        else
        {
            using (var fs = ((TrackerStore)store).NewFileStream(store.Path, FileMode.Append, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                ((TrackerStore)store).WriteRecord(tracker, store.Version, writer);
            }
        }

    }

    #endregion Stock
}