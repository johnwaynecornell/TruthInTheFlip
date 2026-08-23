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
    public required ParameterInfo Parameter { get; init; }
    public required MetricParameterType Type { get; init; }
}