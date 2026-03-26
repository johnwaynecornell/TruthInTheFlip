using TruthInTheFlip.Format;

namespace Report1_tst2;

public class WatchVariables
{
    public long cnt = 0;
    public long good = 0;

    public double minZ = double.PositiveInfinity;
    public double maxZ = double.NegativeInfinity;
    
    public double a = 0;
    public double Z = 0;
    
    public void Inspect(Tracker tracker)
    {
        var zScore = tracker.ZScore;
        if (zScore < minZ) minZ = zScore;
        if (zScore > maxZ) maxZ = zScore;

        this.Z += zScore;
        
        a += tracker.AnticipatedPercentage;
        
        cnt++;
        if (tracker.anticipated << 1 >= tracker.total) good++;
    }

}