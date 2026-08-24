using System.Reflection;
namespace JWCFarm.Metrics;

public class MetricCatalog
{
    public Dictionary<string, MetricDescriptor> Metrics { get; set; }
    
    public void Add(MetricDescriptor metric)
    {
        Metrics.Add(metric.Name, metric);
    }
    
}