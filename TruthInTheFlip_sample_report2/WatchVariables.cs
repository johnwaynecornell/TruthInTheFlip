using TruthInTheFlip.Format;

namespace TruthInTheFlip_sample_report2;

public class Watch
{
    public Tracker? minHolder = null;
    public Tracker? maxHolder = null;
    
    public double min = double.PositiveInfinity;
    public double max = double.NegativeInfinity;
    public double cnt = 0;
    public double sum = 0;

    public double average => sum / cnt;
    
    public Func<Tracker, double> property;  // Note the word property here is deliberately used and is meant in an intellectual sense.
    
    public Watch(Func<Tracker, double> property) => this.property = property;
    
    public void Lookat(Tracker t)
    {
        double v = property(t);
        
        if (v < min) { min = v; minHolder = t; }
        if (v > max) { max = v; maxHolder = t; }

        cnt++;
        sum += v;
    }
}

public class WatchVariables
{
    public long cnt = 0;
    public long good = 0;

    public Dictionary<String, Watch> watchers = new();
    
    public void Inspect(Tracker tracker)
    {
    
        foreach (var w in watchers.Values) w.Lookat(tracker);
        cnt++;
        if (tracker.anticipated << 1 >= tracker.total) good++;
    }
    
    public Watch this[string name]
    {
        get { return watchers[name]; }
        set { watchers[name] = value; }
    }
}