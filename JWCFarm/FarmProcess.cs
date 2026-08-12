using System.ComponentModel;
using JWCFarm.Metrics;

namespace JWCFarm;

[Description("A process that produces items for Farm operations.")]
public abstract class FarmProcess : FarmCommand
{
    public MetricProjection? Projection { get;set;} = null;
    public MetricProjection projection_get() {
            return Projection ?? throw new InvalidOperationException(
                "Projection not set");
    }

    public virtual bool BindFields(MetricCatalogs catalogs, string[] fields)
    {
        if (!MetricBinder.Bind(catalogs, StatType, out MetricProjection projection, 
                fields)) throw new ArgumentException("Invalid fields");
        
        Projection = projection;
        return true;
    }
    
    public abstract Type StatType { get; }
    
    public ProcessActions Actions { get; set; } = new ();
    
    protected virtual void BeginProcess(FarmContext context)
        => Actions.Begin?.Invoke(context);

    protected virtual void ProcessItem(
        FarmContext context,
        object item)
        => Actions.Process?.Invoke(context, item);

    protected virtual void EndProcess(FarmContext context)
        => Actions.End?.Invoke(context);
    
    protected virtual void AbortProcess(
        FarmContext context,
        Exception exception)
        => Actions.Abort?.Invoke(context, exception);
    
    protected abstract IEnumerable<object> EnumerateItems(
        FarmContext context);
    
    public sealed override void Execute(FarmContext context)
    {
        try
        {
            BeginProcess(context);

            foreach (object item in EnumerateItems(context))
            {
                ProcessItem(context, item);
            }

            EndProcess(context);
        }
        catch (Exception e)
        {
            AbortProcess(context, e);
            throw;
        }
    }
    
}
