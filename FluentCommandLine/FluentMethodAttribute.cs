namespace FluentCommandLine;

public class FluentMethodAttribute : Attribute
{
    public String? Name { get; }
    public bool Default { get; }
    
    public FluentMethodAttribute(string? name = null, bool def = false)
    {
        Name = name;
        Default = def;
    }
}