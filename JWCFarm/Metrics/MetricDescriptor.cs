using System.Reflection;

namespace JWCFarm.Metrics;

public class MetricDescriptor
{
    public enum EType
    {
        Property,
        // with the current design the following consume one extra argument from the metric path
        Scalar,
        Aggregate
    }
    
    public required EType Type { get; init; }
    
    public required string Name { get; init; }
    public required Type ValueType { get; init; }
    public required string Help { get; init; }

    public Func<object, object?>? Getter { get; set; } = null;
    public MethodInfo? Method { get; set; } = null;
    
    public class Instance
    {
        public MetricDescriptor InstanceDescriptor { get; set; } = null!;
        public MetricPath? ArgumentPath { get; set; } = null;
    }
    
    public Instance CreateInstance(MetricPath parameters)
    {
        return new Instance
        {
            InstanceDescriptor = this,
            ArgumentPath = parameters
        };
    }
}