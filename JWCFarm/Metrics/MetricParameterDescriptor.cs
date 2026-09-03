using System.Reflection;

namespace JWCFarm.Metrics;

public enum MetricParameterType
{
    Scalar,
    Aggregate
}

public abstract record MetricArgument;

public sealed class MetricParameterDescriptor
{
    public MetricParameterDescriptor()
    {
        
    }

    public MetricParameterDescriptor(string name, MetricParameterType type, Type? reflectedType = null)
    {
        Name = name;
        Type = type;
        ReflectedType = reflectedType;
    }

    public string Name { get; init; }
    public MetricParameterType Type { get; init; }
    public Type? ReflectedType { get; init; }
}