using System.Diagnostics;

namespace TruthInTheFlip;

public class Tracker
{
    public long positive = 0;
    public long negative = 0;
    
    public long anticipated = 0;
    public long baseAnticipated = 0;
    public long anticipatedPositive = 0;
    public long anticipatedNegative = 0;
    public long total = 0;

    public bool priorFlip = false;
    public bool guessAnticipateChange = false;

    public Tracker trackerInner;
    public long cumulativeTicks = 0;

    public Random rand = null;
    
    private const double ExpectedWinRate = 0.5;

    public void Reset()
    {
        positive = 0;
        negative = 0;

        anticipated = 0;
        baseAnticipated = 0;
        anticipatedPositive = 0;
        anticipatedNegative = 0;
        total = 0;

        trackerInner?.Reset();

    }


    public double GetCurrentZScore()
    {
        if (total == 0) return 0;

        double actualWinRate = (double)anticipated / total;
        
        // Standard error formula: sqrt((p * (1 - p)) / n)
        double standardError = Math.Sqrt((ExpectedWinRate * (1.0 - ExpectedWinRate)) / total);
        
        if (standardError == 0) return 0;

        return (actualWinRate - ExpectedWinRate) / standardError;
    }
    
    // Add these right below your GetCurrentZScore() method

    public long EstimateTotalFlipsForZScore(double targetZScore)
    {
        if (total == 0) return 0;

        double actualWinRate = (double)anticipated / total;
        
        // If the win rate drops to exactly 0.5 or lower, a positive Z-score 
        // is mathematically unreachable with the current trend.
        if (actualWinRate <= ExpectedWinRate) return -1;

        double zSquared = targetZScore * targetZScore;
        
        // ExpectedWinRate * (1 - ExpectedWinRate) is always 0.25 for a 50/50 baseline
        double variance = ExpectedWinRate * (1.0 - ExpectedWinRate); 
        
        double edge = actualWinRate - ExpectedWinRate;
        double edgeSquared = edge * edge;

        // n = (Z^2 * variance) / edge^2
        double estimatedTotal = (zSquared * variance) / edgeSquared;

        return (long)Math.Ceiling(estimatedTotal);
    }

    public long EstimateRemainingFlipsForZScore(double targetZScore)
    {
        long estimatedTotal = EstimateTotalFlipsForZScore(targetZScore);
        
        // Return -1 if the target is currently unreachable
        if (estimatedTotal <= 0) return -1; 
        
        long remaining = estimatedTotal - total;
        
        // If the target is already reached, remaining is 0
        return remaining > 0 ? remaining : 0; 
    }
    
    public bool Anticipate(bool currentFlip)
    {
        // If anticipating change, expect !priorFlip. If anticipating same, expect priorFlip.
        
        bool guessOutcome = (guessAnticipateChange ? (!priorFlip) : priorFlip) == currentFlip;
        
        if (guessOutcome) baseAnticipated++;
        
        //if (trackerInner != null) result = trackerInner.Anticipate(result) ? !result : result;
        if (trackerInner != null) guessOutcome = trackerInner.Anticipate(guessOutcome) ? guessOutcome : !guessOutcome;

        if (guessOutcome)
        {
            if (currentFlip) anticipatedPositive++;
            else anticipatedNegative++;

            anticipated++;
        }

        total++;
        
        // Anticipate the relation, not the value.
        guessAnticipateChange = currentFlip == priorFlip;
        
        if (currentFlip)
            positive++;
        else
            negative++;
            
        priorFlip = currentFlip;

        return guessOutcome;
    }
    
    public override string ToString()
    {
        return $"{total} flips → " +
               $"positive: {PositivePercentage:F4}% | " +
               $"negative: {NegativePercentage:F4}% | " +
               $"anticipatedPositive: {AnticipatedPositivePercentage:F6}% | " +
               $"anticipatedNegative: {AnticipatedNegativePercentage:F6}% | " +
               $"anticipated: {AnticipatedPercentage:F6}% | " +
               $"base: {BaseAnticipatedPercentage:F6}% | " +
               $"Z: {GetCurrentZScore():F6}" +
               (trackerInner != null ? $" | INNER | {trackerInner}" : "");
    }

    public void Add(Tracker other)
    {
        this.positive += other.positive;
        this.negative += other.negative;
        this.anticipated += other.anticipated;
        this.baseAnticipated += other.baseAnticipated;

        this.total += other.total;
        this.anticipatedPositive += other.anticipatedPositive;
        this.anticipatedNegative += other.anticipatedNegative;
        this.cumulativeTicks += other.cumulativeTicks;
        
        if (other.trackerInner != null)
        {
            if (this.trackerInner == null) this.trackerInner = new Tracker();
            this.trackerInner.Add(other.trackerInner);
        }
    }

    public void WriteRecord(BinaryWriter writer)
    {
        long loc = writer.Seek(0, SeekOrigin.Current);
        long loc2;
                
        writer.Write((int)0);

        writer.Write("TrackerRecord");
        
        for (Tracker current = this; current != null; current = current.trackerInner)
        {
            writer.Write(current.positive);
            writer.Write(current.negative);
            writer.Write(current.anticipated);
            writer.Write(current.baseAnticipated);
            writer.Write(current.total);
            writer.Write(current.anticipatedPositive);
            writer.Write(current.anticipatedNegative);

            writer.Write(current.priorFlip);
            writer.Write(current.guessAnticipateChange);
            writer.Write(current.cumulativeTicks);
            writer.Write(current.trackerInner != null);
        }
                
        loc2 = writer.Seek(0, SeekOrigin.Current);
        int size = (int)(loc2 - loc);
        writer.Seek(-size, SeekOrigin.Current);
        writer.Write((int)(size));
        writer.Seek(size - 4, SeekOrigin.Current);
        writer.Write((int)(size));
    }

    public void Save(string path, bool record = false)
    {
        if (record == false || !File.Exists(path))
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                writer.Write("TruthInTheFlip.v1.0.1");
                writer.Write(record);
                WriteRecord(writer);
            }

            return;
        }
        
        using (FileStream fs = new FileStream(path, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            string version = reader.ReadString();

            switch (version)
            {
                case "TruthInTheFlip.v1.0":
                    if (record) throw new IOException("File version v1.0 does not support record");
                    break;
                case "TruthInTheFlip.v1.0.1":
                    if (reader.ReadBoolean() !=record) throw new IOException("record mode mismatch between args and file");
                    break;
                default:
                    throw new IOException($"\"{version}\" unknown format");
            }
        }
        
        using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            WriteRecord(writer);
        }


    }


    public static Tracker SafeLoad(string path, bool record=false)
    {
        if (!File.Exists(path)) return new Tracker();

        Tracker tracker = new Tracker();
        Tracker outer = tracker;

        using (FileStream fs = new FileStream(path, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            string version;
            version = reader.ReadString();
            switch (version)
            {
                case "TruthInTheFlip.v1.0":
                    if (record) throw new IOException($"\"{path}\" record mode mismatch");
                    
                    while (tracker != null)
                    {
                        tracker.positive = reader.ReadInt64();
                        tracker.negative = reader.ReadInt64();
                        tracker.anticipated = reader.ReadInt64();
                        tracker.baseAnticipated = reader.ReadInt64();

                        tracker.total = reader.ReadInt64();
                        tracker.anticipatedPositive = reader.ReadInt64();
                        tracker.anticipatedNegative = reader.ReadInt64();

                        tracker.priorFlip = reader.ReadBoolean();
                        tracker.guessAnticipateChange = reader.ReadBoolean();
                        tracker.cumulativeTicks = reader.ReadInt64();

                        if (tracker.positive + tracker.negative != tracker.total)
                            throw new IOException($"{path} is damaged or incorrect");
                        if (tracker.anticipatedPositive + tracker.anticipatedNegative != tracker.baseAnticipated)
                            throw new IOException($"{path} is damaged or incorrect");

                        if (reader.ReadBoolean()) tracker = tracker.trackerInner = new Tracker();
                        else tracker = null;

                    }

                    break;
                case "TruthInTheFlip.v1.0.1":
                    if (reader.ReadBoolean() != record) throw new IOException($"\"{path}\" record mode mismatch");
                    
                    fs.Seek(-4, SeekOrigin.End);
                    fs.Seek(-reader.ReadInt32() - 4, SeekOrigin.End);
                    reader.ReadInt32();
                    if (reader.ReadString() != "TrackerRecord") throw new IOException("IOerror expected TrackerRecord");
                    while (tracker != null)
                    {
                        tracker.positive = reader.ReadInt64();
                        tracker.negative = reader.ReadInt64();
                        tracker.anticipated = reader.ReadInt64();
                        tracker.baseAnticipated = reader.ReadInt64();

                        tracker.total = reader.ReadInt64();
                        tracker.anticipatedPositive = reader.ReadInt64();
                        tracker.anticipatedNegative = reader.ReadInt64();

                        tracker.priorFlip = reader.ReadBoolean();
                        tracker.guessAnticipateChange = reader.ReadBoolean();
                        tracker.cumulativeTicks = reader.ReadInt64();

                        if (tracker.positive + tracker.negative != tracker.total)
                            throw new IOException($"{path} is damaged or incorrect");
                        if (tracker.anticipatedPositive + tracker.anticipatedNegative != tracker.baseAnticipated)
                            throw new IOException($"{path} is damaged or incorrect");

                        if (reader.ReadBoolean()) tracker = tracker.trackerInner = new Tracker();
                        else tracker = null;

                    }

                    break;
                default:
                    throw new IOException("version \"{version}\" not suppored");
            }


        }

        return outer;
    }

    public static IEnumerable<Tracker> Enumerate(string path)
    {
        if (!File.Exists(path)) yield break;

        using (FileStream fs = new FileStream(path, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            Tracker tracker;
            Tracker outer;


            string version;
            version = reader.ReadString();
            switch (version)
            {
                case "TruthInTheFlip.v1.0":
                    tracker = new Tracker();
                    outer = tracker;

                    while (tracker != null)
                    {
                        tracker.positive = reader.ReadInt64();
                        tracker.negative = reader.ReadInt64();
                        tracker.anticipated = reader.ReadInt64();
                        tracker.baseAnticipated = reader.ReadInt64();

                        tracker.total = reader.ReadInt64();
                        tracker.anticipatedPositive = reader.ReadInt64();
                        tracker.anticipatedNegative = reader.ReadInt64();

                        tracker.priorFlip = reader.ReadBoolean();
                        tracker.guessAnticipateChange = reader.ReadBoolean();
                        tracker.cumulativeTicks = reader.ReadInt64();

                        if (tracker.positive + tracker.negative != tracker.total)
                            throw new IOException($"{path} is damaged or incorrect");
                        if (tracker.anticipatedPositive + tracker.anticipatedNegative != tracker.baseAnticipated)
                            throw new IOException($"{path} is damaged or incorrect");

                        if (reader.ReadBoolean()) tracker = tracker.trackerInner = new Tracker();
                        else tracker = null;

                    }

                    yield return outer;
                    break;
                case "TruthInTheFlip.v1.0.1":
                    bool _record = reader.ReadBoolean();

                    while (fs.Position != fs.Length)
                    {
                        tracker = new Tracker();
                        outer = tracker;


                        int sz = reader.ReadInt32();
                        string recordType =reader.ReadString(); 
                        if (recordType != "TrackerRecord")
                            throw new IOException("IOerror expected TrackerRecord");
                        while (tracker != null)
                        {
                            tracker.positive = reader.ReadInt64();
                            tracker.negative = reader.ReadInt64();
                            tracker.anticipated = reader.ReadInt64();
                            tracker.baseAnticipated = reader.ReadInt64();

                            tracker.total = reader.ReadInt64();
                            tracker.anticipatedPositive = reader.ReadInt64();
                            tracker.anticipatedNegative = reader.ReadInt64();

                            tracker.priorFlip = reader.ReadBoolean();
                            tracker.guessAnticipateChange = reader.ReadBoolean();
                            tracker.cumulativeTicks = reader.ReadInt64();

                            if (tracker.positive + tracker.negative != tracker.total)
                                throw new IOException($"{path} is damaged or incorrect");
                            if (tracker.anticipatedPositive + tracker.anticipatedNegative != tracker.baseAnticipated)
                                throw new IOException($"{path} is damaged or incorrect");

                            if (reader.ReadBoolean()) tracker = tracker.trackerInner = new Tracker();
                            else tracker = null;

                        }
                        if (sz != reader.ReadInt32()) throw new IOException("tail size mismatch");
                        yield return outer;
                        
                    }

                    break;
                default:
                    throw new IOException("version \"{version}\" not suppored");
            }
        }
    }
    
    /*
        Guessed ⊂ total
        Positive ⊂ Anticipated ∩ (next == last)
        Negative ⊂ Anticipated ∩ (next != last)
    */
    public static double Percentage(long parts, long whole) => (double)parts / whole * 100;
    
    public double PositivePercentage => Percentage(positive, total);
    public double NegativePercentage => Percentage(negative, total);
    public double AnticipatedPercentage => Percentage(anticipated, total);
    public double BaseAnticipatedPercentage => Percentage(baseAnticipated, total); 
    public double AnticipatedPositivePercentage => Percentage(anticipatedPositive, positive);
    public double AnticipatedNegativePercentage => Percentage(anticipatedNegative, negative);
    public double BiasDelta => AnticipatedPercentage - BaseAnticipatedPercentage;
    public double InversionGain => AnticipatedPercentage - 50.0;

}
