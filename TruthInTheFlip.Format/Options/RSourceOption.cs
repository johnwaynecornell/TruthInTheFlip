using System.Text;

namespace TruthInTheFlip.Format.Options;

public class RSourceOption : Option
{
    public RSourceOption() : base("-rsource")
    {
    }
    
    public virtual string? Source { get; set; }

    public Dictionary<string, Func<Action<byte[]>>> Sources { get; set; } = new Dictionary<string, Func<Action<byte[]>>>();
    public Dictionary<string, string> Descriptions { get; set; } = new Dictionary<string, string>();
    
    public virtual RSourceOption AddDefaults()
    {
        AddSource("NET1", "System.Random", BitFactory.initRandom_Net);
        AddSource("NET2", "System.Security.Cryptography.RandomNumberGenerator",() => (arr) => System.Security.Cryptography.RandomNumberGenerator.Fill(arr));
        return this;
    }

    public virtual Func<Action<byte[]>>? SeedFunc => Source == null ? null : (Sources.ContainsKey(Source) ? Sources[Source] : null);
    
    public virtual void AddSource(String name, String description, Func<Action<byte[]>> func)
    {
        if (Sources.ContainsKey(name)) throw new ArgumentException($"collision on {name}");
        Sources[name] = func;
        Descriptions[name] = description;
    }

    public override string GetHelp()
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("  -rsource list       List random sources");
        stringBuilder.AppendLine("  -rsource <string>   Random source string (default: NET1)");
        return stringBuilder.ToString();
    }

    public virtual string List()
    {
        StringBuilder b = new StringBuilder();

        b.AppendLine("Random sources:");
        foreach (string key in Sources.Keys)
        {
            b.AppendLine(UtilT.PadRight($"  {key}") + Descriptions[key]);
        }
        
        return b.ToString();

    }
    
    public override string Info()
    {
        StringBuilder b = new StringBuilder();

        b.AppendLine("Random source:");
        
        b.AppendLine(UtilT.PadRight($"  Source = {Source}") + (Source == null ? "error" : Descriptions[Source]));
        return b.ToString();
        
    }
    public override bool TryParse(List<string> command_args, int index, ref int status, SOut message, SOut errorMessage)
    {
        if (!base.TryParse(command_args, index, ref status, message, errorMessage) || status != 0)
        {
            return false;
        }
        
        if (index >= command_args.Count)
        {
            errorMessage($"Option \'{Name}\' missing parameters");
            status = -1;
            return false;
        }

        Source = command_args[index];
        command_args.RemoveAt(index);

        if (Source == "list")
        {
            message(List(), false);
            
            Enabled = false;
            WantExit = true;
        }

        return true;
    }
}