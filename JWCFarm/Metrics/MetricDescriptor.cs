using System.Reflection;

namespace JWCFarm.Metrics;

public class MetricDescriptor
{
    public enum EType
    {
        Property,
        Method
    }
    
    public EType Type { get; init; }
    
    public string Name { get; init; }
    public Type ValueType { get; init; }
    public string Help { get; init; }

    public Func<object, object?>? Getter { get; set; } = null;
    public Func<object, object?[], object?>? Invoke { get; init; }
    
    public IReadOnlyList<MetricParameterDescriptor>? Parameters { get; init; }
    
    public MetricDescriptor()
    {

    }
    
    public MetricDescriptor(string name, Type returnType, List<MetricParameterDescriptor> parameters, string help, Func<object, object?[], object?>? invoke )
    {
        Type = EType.Method;
        
        Name = name;
        ValueType = returnType;
        Parameters = parameters;
        Help = help;
        Invoke = invoke;
    }
    
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