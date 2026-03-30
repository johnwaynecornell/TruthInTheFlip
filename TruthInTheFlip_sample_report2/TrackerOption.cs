namespace TruthInTheFlip_sample_report2;

public abstract class TrackerOption : Option
{
    protected TrackerOption(string Name) : base(Name)
    {
    }
    
    public virtual int[]? RequiredVersion {get; set; }

    public abstract bool ValidateVersion(int[] target_version, Action<String>? errorMessag);
}