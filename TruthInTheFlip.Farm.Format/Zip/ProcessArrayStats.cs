using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Positional row stats item produced by <see cref="ZipProcess"/> containing one item from each child process.
/// Process-local dynamic metrics (item_0, item_1, etc.) are bound dynamically to access individual child items.
/// </summary>
public sealed class ProcessArrayStats : MetricFunctions
{
    /// <summary>
    /// Gets the ordered items corresponding to each child process in the zip operation.
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
}
