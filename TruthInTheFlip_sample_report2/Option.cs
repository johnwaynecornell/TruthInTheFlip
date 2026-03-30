using System.Collections;
using System.Text;

namespace TruthInTheFlip_sample_report2;

public abstract class Option
{
    public string? Name { get; set;  }
    public bool Enabled { get; set; } = false;

    public Option(string? name)
    {
        //  I strongly considered requiring the first character to be a - here, but if you want to use acronyms, that's up to you.
        this.Name = name;
    }

    public bool ManualConfigurate(Action<string>? errorWriteLine = null, params string[] args)
    {
        if (errorWriteLine == null) errorWriteLine = (s) => Console.Error.WriteLine(s);
        List<String> command_args = new List<String>(args);

        int status = 0;
        bool rc = TryParse(command_args, 0, ref status, errorWriteLine);
        return rc && (status == 0) && (command_args.Count == 0);
    }
    
    public virtual bool TryParse(List<String> command_args, int index, ref int status, Action<String> errorMessage)
    {
        // If Name is null, as is the case of the Options class, it will need special processing through method override.
        if (Name == null) return false;
        
        if (index >= command_args.Count) return false;
        if (command_args[index] != Name) return false;

        command_args.RemoveAt(index);
        
        if (Enabled)
        {
            errorMessage($"Option '{Name}' is duplicated on command line");
            status = -1;
            return false;
        }

        Enabled = true;
        return true;
    }

    public abstract string GetHelp();
    public abstract string Info();

    public virtual string NameString()
    {
        return Util.PadRight($"  {Name}");
    }
    
    // The default behavior when an option is disabled. 
    // Subclasses can override this to provide specific context (e.g., "Disabled (Using Lifetime)")
    public virtual string DisabledInfo()
    {
        return $"{NameString()} Disabled\n";
    }
}

// It is better to use this as a global for the command line  But it's a free world, so if you want to use it as a base class for an extension, ...
public class InfoOption : Option
{
    public InfoOption() : base("-info") { }
    
    public virtual string HelpMessage { get; set; } = "Show state\n";
    
    // It just inherits TryParse. If it finds "-info", it sets Enabled = true.
    public override string GetHelp()
    {
        return NameString() + HelpMessage;
    }

    public virtual string InfoMessage { get; set; } = ""; // Leave blank unless you want info to tell you about itself.

    public override string Info()
    {
        return InfoMessage;
    }
}

public class Options : Option , IEnumerable<Option>
{
    public List<Option> contents = new List<Option>();

    public Options() : base(null)
    {
        Enabled = true;
    }

    public override bool TryParse(List<String> command_args, int index, ref int status, Action<String> errorMessage)
    {
        bool rc;
        if (status != 0) return false;
        foreach (Option o in contents)
            if ((rc = o.TryParse(command_args, index, ref status, errorMessage)) || status != 0)
                return rc;
        return false;
    }

    public override string GetHelp()
    {
        // Help handles its own new lines. All we have to do is concatenate.
        StringBuilder stringBuilder = new StringBuilder();
        foreach (Option o in contents) stringBuilder.Append(o.GetHelp());
        return stringBuilder.ToString();
    }

    public override string Info()
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (Option o in contents)
        {
            if (o.Enabled)
            {
                stringBuilder.Append(o.Info());
            }
            else
            {
                stringBuilder.Append(o.DisabledInfo());
            }
        }
        return stringBuilder.ToString();
    }
    
    public virtual Option Add(Option o)
    {
         contents.Add(o);
         return o;
    }

    public IEnumerable<string> Keys => (from v in contents select v.Name); 
    public Option this[string name] => (from v in contents where v.Name == name select v).First();

    public IEnumerator<Option> GetEnumerator() => contents.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => contents.GetEnumerator();
}