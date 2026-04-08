using System.Text;

namespace TruthInTheFlip.Format.Options;

public class RSourceOption : Option
{
    public DelegateMethodRegistry Registry { get; set; }
    public DelegateMethodRegistry.RegistryParseResult? RegistryParseResult { get; set; }

    public RSourceOption() : base("-rsource")
    {
        Registry = new DelegateMethodRegistry(typeof(Func<Action<byte[]>>),"random source");
    }
    
    public virtual RSourceOption AddDefaults()
    {
        Registry.AddSource(() => BitFactory.initRandom_Net, "NET1", "System.Random", new string[] { }, new string[] { }).IsDefault = true;
        Registry.AddSource(() => (Func<Action<byte[]>>)(() => (arr) => System.Security.Cryptography.RandomNumberGenerator.Fill(arr)), "NET2", "System.Security.Cryptography.RandomNumberGenerator", new string[]{}, new string[]{});

        return this;
    }

    public virtual Func<Action<byte[]>>? SeedFunc =>  RegistryParseResult?.Strategy as Func<Action<byte[]>>;

    public override bool ValidateVersion(string Version, SOut errorMessage)
    {
        return true;
    }

    public override bool TryParse(List<string> command_args, int index, ref int status, SOut message, SOut errorMessage)
    {
        if (!base.TryParse(command_args, index, ref status, message, errorMessage) || status != 0)
        {
            return false;
        }

        if (!Registry.TryParse(this, command_args, index, ref status, message, errorMessage, out var res)) return false;
        RegistryParseResult = res;
            
        return true;
    }

    public override string Info()
    {
        var res = UtilT.ThrowIfNull(RegistryParseResult, "RegistryParseResult");
        return Registry.Info(this, res);
    }
        
    public virtual string List()
    {
        return Registry.List(this);
    }
        
    public override string GetHelp()
    {
        return Registry.GetHelp(this);
    }
        
    public override string DisabledInfo()
    {
        return $"{NameString()}Disabled (Using default rsource)\n";
    }
}