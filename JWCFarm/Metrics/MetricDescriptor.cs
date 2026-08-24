using System.Reflection;

namespace JWCFarm.Metrics;

public class MetricDescriptor
{
    public enum EType
    {
        Property,
        Method
    }
    
    public required EType Type { get; init; }
    
    public required string Name { get; init; }
    public required Type ValueType { get; init; }
    public required string Help { get; init; }

    public Func<object, object?>? Getter { get; set; } = null;
    public MethodInfo? Method { get; set; } = null;
    
    public IReadOnlyList<MetricParameterDescriptor> Parameters { get; init; }
    
    public class Instance
    {
        public bool IsValue { get; set; }
        public object? Value { get; set; } = null;
        public MetricDescriptor InstanceDescriptor { get; set; } = null!;
        public List<MetricPath> ArgumentPaths { get; set; } = new();
    }
    
    public Instance CreateInstance(List<MetricPath>? parameters)
    {
        return new Instance
        {
            InstanceDescriptor = this,
            ArgumentPaths = parameters
        };
    }
    
    public static Instance CreateInstance(double value)
    {
        return new Instance
        {
            IsValue = true,
            Value = value
        };
    }
}