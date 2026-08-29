using FluentCommandLine;

namespace TruthInTheFlip_CSV_Farm;

[KV_FA(FluentAttribute.Help, "request help")]
public class HelpCommand
{
    [FluentMethod("-help")]
    [KV_FA(FluentAttribute.Help, "Request this help")]
    public static HelpCommand HelpSwitch()
    {
        return new HelpCommand();
    }
}
