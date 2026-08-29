using FluentCommandLine;

namespace TruthInTheFlip_CSV_Farm;

[KV_FA(FluentAttribute.Help, "request information")]
public class InfoCommand
{
    public string OutputPath { get; init; }
    
    public InfoCommand(string outputPath)
    {
        OutputPath = outputPath;
    }
    
    [FluentMethod("-info")]
    [KV_FA(FluentAttribute.Help, "Request configuration information for command line")]
    public static InfoCommand InfoSwitch(
        [KV_FA(FluentAttribute.Help, "Path to write information output to.")] 
        [KV_FA(FluentAttribute.Def, "#")] 
        string outputPath)
    {
        return new InfoCommand(outputPath);
    }
}
