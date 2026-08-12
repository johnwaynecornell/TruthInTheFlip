namespace JWCFarm;

public class FarmContext
{
    public TextWriter Output { get; set; } = Console.Out;
    public TextWriter ErrorOutput { get; set; } = Console.Error;
}
