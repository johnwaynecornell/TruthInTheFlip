using System.Text;

namespace TruthInTheFlip.Format.Options;

public class RSourceOption : Option
{
    public DelegateMethodRegistry Registry { get; set; }
    public DelegateMethodRegistry.RegistryParseResult? RegistryParseResult { get; set; }

    public RSourceOption() : base("-rsource")
    {
        Registry = new DelegateMethodRegistry(typeof(BitFactory),"random source");
    }
    
    public static Dictionary<string, BitFactory> FactoryStorage = new Dictionary<string, BitFactory>();
    
    public virtual RSourceOption AddDefaults()
    {

        Registry.AddFromHostType(GetType());
        Registry.Strategies["NET1"].IsDefault = true;
        
        return this;
    }

    [StringHelp("System.Random")]
    public static BitFactory NET1()
    {
        lock(FactoryStorage)
        {
            if (!FactoryStorage.ContainsKey("NET1"))
            {
                FactoryStorage["NET1"] = new BitFactory();
                FactoryStorage["NET1"].resetRandom = Format.BitFactory.initRandom_Net;
                FactoryStorage["NET1"].Reset();
            }

            return FactoryStorage["NET1"];
        }
    }

    [StringHelp("System.Security.Cryptography.RandomNumberGenerator")]
    public static BitFactory NET2()
    {
        lock(FactoryStorage)
        {
            if (!FactoryStorage.ContainsKey("NET2"))
            {
                FactoryStorage["NET2"] = new BitFactory();
                FactoryStorage["NET2"].resetRandom =
                    (() => (arr) => System.Security.Cryptography.RandomNumberGenerator.Fill(arr));
                FactoryStorage["NET2"].Reset();
            }

            return FactoryStorage["NET2"];
        }
    }

    
    public virtual BitFactory? BitFactory =>  RegistryParseResult?.Strategy as BitFactory;

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