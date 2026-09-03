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

    public Func<MetricEvaluationContext, object, object?>? Getter { get; set; } = null;
    public Func<MetricEvaluationContext, object, object?[], object?>? Invoke { get; init; }

    public IReadOnlyList<MetricParameterDescriptor>? Parameters { get; init; }

    // Canonical expression strings that this descriptor requires to be pre-bound during
    // MetricBinder.Bind. The bound MetricPaths are stored as hidden dependencies in the
    // MetricProjection and retrieved at evaluation time via MetricEvaluationContext.Get<T>.
    public IReadOnlyList<string>? SourceExpressions { get; init; }
    
    public MetricDescriptor()
    {

    }
    
    public MetricDescriptor(string name, Type returnType, string help, Func<MetricEvaluationContext, object, object?> getter, IReadOnlyList<string>? sourceExpressions = null)
    {
        Type = EType.Property;
        Name = name;
        ValueType = returnType;
        Help = help;
        Getter = getter;
        SourceExpressions = sourceExpressions;
    }
    
    public MetricDescriptor(string name, Type returnType, List<MetricParameterDescriptor> parameters, string help, Func<MetricEvaluationContext, object, object?[], object?>? invoke, IReadOnlyList<string>? sourceExpressions = null )
    {
        Type = EType.Method;
        
        Name = name;
        ValueType = returnType;
        Parameters = parameters;
        Help = help;
        Invoke = invoke;
        SourceExpressions = sourceExpressions;
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
    
    public static Instance CreateInstance(object? value)
    {
        return new Instance
        {
            IsValue = true,
            Value = value
        };
    }
}