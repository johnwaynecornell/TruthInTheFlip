using System.Reflection;

namespace JWCFarm.Metrics;

public class MetricCatalogs
{
    public Dictionary<Type, MetricCatalog?> Catalogs { get; set; } = new Dictionary<Type, MetricCatalog>();
    public Func<Type, MetricCatalog?> Reflect { get; set; } = (T) => null;

    public bool TryGet(Type type, out MetricCatalog? catalog)
    {
        if (Catalogs.TryGetValue(type, out catalog)) return catalog != null;
        catalog = Catalogs[type] = Reflect(type);
        return catalog != null;
        
    }
    
}