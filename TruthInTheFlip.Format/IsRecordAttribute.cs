namespace TruthInTheFlip.Format;



public class VersioningAttribute : Attribute
{
    public string Version { get; set; }
    public string? VersionHigh_exclusive { get; set; }
    public bool Obsolete { get; set; }
    
    public VersioningAttribute(string version, string ?version_high_exclusive = null, bool obsolete = false)
    {
        this.Version = version;
        this.VersionHigh_exclusive = version_high_exclusive;
        this.Obsolete = obsolete;
    }
}

public class IsRecordAttribute : VersioningAttribute
{
    public IsRecordAttribute(string version, string? version_high_exclusive = null, bool obsolete = false) : base(
        version, version_high_exclusive, obsolete)
    {
        
    }
}

public class MetricTypeAttribute : Attribute
{
    public String Type;
    public MetricTypeAttribute(String type)
    {
        this.Type = type;
    }
}

public class IsMetricAttribute : VersioningAttribute
{
    
    public IsMetricAttribute(string version, string? version_high_exclusive = null, bool obsolete = false) : base(
        version, version_high_exclusive, obsolete)
    {
    }
    
}