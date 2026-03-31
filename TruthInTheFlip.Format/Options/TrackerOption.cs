namespace TruthInTheFlip.Format.Options;

public abstract class TrackerOption : Option
{
    protected TrackerOption(string Name) : base(Name)
    {
    }
    
    public virtual int[]? RequiredVersion {get; set; }

    public abstract bool ValidateVersion(int[] target_version, SOut? errorMessage);
}