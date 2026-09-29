using JWCFarm;
using JWCFarm.Metrics;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Positional or joined row stats item produced by higher-order processes (<see cref="ZipProcess"/>, <see cref="JoinProcess"/>)
/// containing one item from each child process.
/// Process-local dynamic metrics (item_0, item_1, etc.) are bound dynamically to access individual child items.
/// </summary>
public sealed class ProcessArrayStats : MetricFunctions
{
    /// <summary>
    /// Gets the ordered items corresponding to each child process in the combinator operation.
    /// </summary>
    public IReadOnlyList<object> Items { get; }

    public ProcessArrayStats(IReadOnlyList<object> items)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public ProcessArrayStats(params object[] items)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    /// <summary>
    /// Constructs a dynamic <see cref="MetricCatalog"/> defining item_0, item_1, ..., item_{N-1}
    /// pointing to the respective child process item and StatType.
    /// </summary>
    public static MetricCatalog CreateDynamicMetricCatalog(IReadOnlyList<FarmProcess> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        var catalog = new MetricCatalog();
        for (int i = 0; i < children.Count; i++)
        {
            int capturedIndex = i;
            var child = children[capturedIndex];
            catalog.Add(new MetricDescriptor
            {
                Type = MetricDescriptor.EType.Property,
                Name = $"item_{capturedIndex}",
                ValueType = child.StatType,
                Help = $"Child process item at index {capturedIndex} ({child.StatType.Name})",
                Getter = (ctx, row) =>
                {
                    var stats = (ProcessArrayStats)row;
                    return stats.Items[capturedIndex];
                }
            });
        }
        return catalog;
    }
}
