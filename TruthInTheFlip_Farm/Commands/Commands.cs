using System.Globalization;
using System.Reflection;
using System.Text;
using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;

namespace TruthInTheFlip_CSV_Farm;

public class Commands
{
    
    [KV_FA(FluentAttribute.Help, "Selectable information topic")]
    public record ShowArgument(Action<FarmContext> Action)
    {
        
    }
    
    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Display the available metric fields.")]
    public static ShowArgument metrics()
    {
        var catalogs = FluentEnvironment.Current.Context.Get<MetricCatalogs>();
        
        return new ShowArgument(ctx =>
        {
            ctx.Output.WriteLine("Metrics:");
            List<string>[] columns = new List<string>[3];
            int [] len= new int[3];
            
            Action<int, string> set = (i, s) =>
            {
                columns[i].Add(s);
                len[i] = Math.Max(len[i], s.Length);
            };

            Action<string,int, StringBuilder> pad = (s, l, sb) =>
            {
                sb.Append(s);
                for (int j=s.Length; j<l; j++)
                {
                    sb.Append(' ');
                }
            };
            
            Func<int, string> get = (i) =>
            {
                StringBuilder sb = new StringBuilder();
                
                for (int column=0; column< columns.Length; column++)
                    pad(columns[column][i], len[column] + 1, sb);
                
                return sb.ToString();
            };
            
            foreach (var kvp in catalogs.Catalogs)
            {
                ctx.Output.WriteLine();
                ctx.Output.WriteLine("    " + kvp.Key);
                for (int i=0; i<3; i++)
                {
                    columns[i] = (new List<string>());
                    len[i] = 0;
                }

                foreach (var m in kvp.Value.Metrics)
                {
                    set(0, m.Value.Name);
                    set(1, $"<{m.Value.ValueType.Name}>");
                    set(2, m.Value.Help);
                    
                    //ctx.Output.WriteLine("        " + FluentEnvironment.PadRight(m.Value.Name) + m.Value.Help);
                }
                
                for (int i=0; i<columns[0].Count; i++) ctx.Output.WriteLine("        " + get(i));
            }
        });
    }
    
    [FluentMethod]
    [KV_FA(FluentAttribute.Help, "Display the current time in Farm-compatible format.")]
    public static ShowArgument now()
    {
        return new ShowArgument(ctx =>
        {
            ctx.Output.WriteLine(
                DateTimeOffset.Now.ToString(
                    "yyyy-MM-ddTHH:mm:sszzz",
                    CultureInfo.InvariantCulture));
        });
    }

    [FluentMethod("show")]
    [KV_FA(FluentAttribute.Help, "Show information about the selected topic.")]
    public static FarmCommand Show(
        [KV_FA(FluentAttribute.Help, "Information topic to show.")]
        ShowArgument argument)
    {
        var catalogs = FluentEnvironment.Current.Context.Get<MetricCatalogs>();
        
        catalogs.TryGet(typeof(Tracker), out var trackerMetrics);
        catalogs.TryGet(typeof(SegmentStats), out var segmentMetrics);
        
        return new FarmDelegateCommand((ctx) =>
        {
            argument.Action(ctx);
        });
    }
}
