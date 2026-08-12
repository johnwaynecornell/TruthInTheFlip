using System.Reflection;

namespace JWCFarm.Metrics;

public class MetricDescriptor
{
    public required string Name { get; init; }
    public required Type ValueType { get; init; }
    public required string Help { get; init; }
    public required Func<object, object?> Getter { get; init; }
}
