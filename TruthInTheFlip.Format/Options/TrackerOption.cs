namespace TruthInTheFlip.Format.Options;

public abstract class TrackerOption : Option
{
    protected TrackerOption(string Name) : base(Name)
    {
    }
    
    public VersioningAttribute? RequiredVersion 
    {
        get => _requiredVersion;
        set => _requiredVersion = value;
    }
    
    private VersioningAttribute? _requiredVersion = default;
    
    public override bool ValidateVersion(string target_version, SOut? errorMessage)
    {
        if (Enabled && RequiredVersion != null)
        {
            int[]? ver = TrackerStore.ReadVersion("TruthInTheFlip.v", target_version);
            if (ver == null)
            {
                errorMessage?.Invoke($"Error: The chosen {Name} parameters require a TrackerRecord file but the loaded file is not compatible {target_version}.");
                return false;
                
            }
            
            int[]? verLow =  TrackerStore.ReadVersion("TruthInTheFlip.v", RequiredVersion.Version);
            if (verLow == null)
            {
                errorMessage?.Invoke($"Error: {target_version} not compatible with TrackerOption.");
                return false;
                
            }
            
            if (TrackerStore.VersionCompare(ver, verLow) < 0)
            {
                errorMessage?.Invoke($"Error: The chosen {Name} parameters require a TrackerRecord file of at least version {RequiredVersion.Version}, but the loaded file is version {target_version}.");
                return false;
            }

            if (RequiredVersion.VersionHigh_exclusive != null)
            {
                int[]? verHigh =  TrackerStore.ReadVersion("TruthInTheFlip.v", RequiredVersion.VersionHigh_exclusive);
                if (verHigh == null)
                {
                    errorMessage?.Invoke($"Error: {target_version} not compatible with TrackerOption.");
                    return false;
                }
                
                if (TrackerStore.VersionCompare(ver, verHigh) >= 0)
                {
                    errorMessage?.Invoke(
                        $"Error: The chosen {Name} parameters require a TrackerRecord file below version {RequiredVersion.VersionHigh_exclusive}, but the loaded file is version {target_version}.");
                    return false;
                }
            }
        }
        
        return true;
    }
    
    
}