namespace TruthInTheFlip_sample_report2;

public class StringHelpAttribute : Attribute
{
    public string Description { get; }
    
    public StringHelpAttribute(string description)
    {
        Description = description;
    }
}

public class StringDefaultAttribute : Attribute
{
    public string Value { get; }
    
    public StringDefaultAttribute(string value)
    {
        Value = value;
    }
}
