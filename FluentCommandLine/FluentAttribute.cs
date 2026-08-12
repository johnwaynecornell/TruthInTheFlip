using JWCEssentials.Metadata;

namespace FluentCommandLine;

public enum FluentAttribute
{
    Help, 
    Def
}

public class KV_FA : KVAttribute<FluentAttribute>
{
    public KV_FA(FluentAttribute attribute, string value) : base(attribute, value)
    {
    }
}