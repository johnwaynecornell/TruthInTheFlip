using System.Globalization;
using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using JWCFarm.Metrics;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// An N-way positional process combinator that executes child processes sequentially,
/// materializes their emitted item populations, and aligns them into positional <see cref="ProcessArrayStats"/> rows.
/// 
/// Key operational characteristics:
/// - Positional alignment: items from child 0, child 1, ..., child N-1 are paired by ordinal index.
/// - Shortest wins: emits exactly min(length(child_0), ..., length(child_N-1)) complete rows.
/// - Sequential child execution: each child process runs to completion before the next begins.
/// - Materialization: child populations are buffered in memory before row emission begins.
/// </summary>
public sealed class ZipProcess : FarmProcess
{
    private readonly IReadOnlyList<FarmProcess> _children;
    private readonly MetricCatalog _dynamicMetricCatalog;

    /// <summary>
    /// Gets the ordered collection of child processes participating in the zip combinator.
    /// </summary>
    public IReadOnlyList<FarmProcess> Children => _children;

    public ZipProcess(IReadOnlyList<FarmProcess> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        if (children.Count == 0)
        {
            throw new ArgumentException("ZipProcess requires at least one child process.", nameof(children));
        }

        var list = new FarmProcess[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i] ?? throw new ArgumentNullException(nameof(children), $"Child process at index {i} cannot be null.");
            child.Projection ??= new MetricProjection();
            list[i] = child;
        }

        _children = list;
        _dynamicMetricCatalog = ProcessArrayStats.CreateDynamicMetricCatalog(list);
    }

    public ZipProcess(params FarmProcess[] children)
        : this((IReadOnlyList<FarmProcess>)children)
    {
    }

    [FluentMethod("zip")]
    [KV_FA(FluentAttribute.Help, "Align multiple child FarmProcesses positionally into combined rows. End the child process list with .END.")]
    public static FarmProcess Zip(
        [KV_FA(FluentAttribute.Help, "Child FarmProcesses to zip positionally. End with .END.")]
        FarmProcess[] processes)
    {
        if (processes == null || processes.Length == 0)
        {
            throw new ArgumentException("Zip requires at least one child process.", nameof(processes));
        }

        return new ZipProcess(processes);
    }

    public override Type StatType => typeof(ProcessArrayStats);
    public override Type InputType => typeof(object);
    public override FarmProcess? InputProcess => null;

    public override MetricCatalog? GetDynamicMetricCatalog(Type type)
    {
        if (type == typeof(ProcessArrayStats))
            return _dynamicMetricCatalog;

        return null;
    }

    protected override IEnumerable<object> EnumerateItems(FarmContext context)
    {
        if (_children.Count == 0)
            yield break;

        var populations = new List<List<object>>(_children.Count);

        foreach (var child in _children)
        {
            var items = new List<object>();
            ChildProcessObserver.Execute(child, context, (_, item) => items.Add(item));
            populations.Add(items);
        }

        int minCount = populations.Min(p => p.Count);

        for (int i = 0; i < minCount; i++)
        {
            var rowItems = new object[_children.Count];
            for (int c = 0; c < _children.Count; c++)
            {
                rowItems[c] = populations[c][i];
            }

            yield return new ProcessArrayStats(rowItems);
        }
    }
}
