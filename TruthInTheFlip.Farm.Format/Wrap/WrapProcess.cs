using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using JWCFarm.Metrics;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// A higher-order FarmProcess adapter that wraps any child FarmProcess,
/// consumes all items produced by the child as a population into its metric evaluation session,
/// and yields a single outer <see cref="WrapStats"/> item against which aggregate metric expressions operate.
/// </summary>
public sealed class WrapProcess : FarmProcess
{
    private readonly FarmProcess _child;

    public FarmProcess Child => _child;

    public WrapProcess(FarmProcess child)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _child.Projection ??= new MetricProjection();
    }

    [FluentMethod("wrap")]
    [KV_FA(FluentAttribute.Help, "Wrap a child FarmProcess to expose its items as a population for outer metric aggregation.")]
    public static FarmProcess Wrap(
        [KV_FA(FluentAttribute.Help, "Child FarmProcess to wrap.")]
        FarmProcess process)
    {
        return new WrapProcess(process);
    }

    public override Type StatType => typeof(WrapStats);
    public override Type InputType => _child.StatType;
    public override FarmProcess? InputProcess => _child;

    protected override IEnumerable<object> EnumerateItems(FarmContext context)
    {
        var stats = new WrapStats();
        var session = Session ?? (Projection != null ? (Session = new MetricEvaluationSession(Projection)) : null);
        var originalActions = _child.Actions;

        try
        {
            _child.Actions = new ProcessActions(
                begin: originalActions?.Begin,
                process: (ctx, item) =>
                {
                    if (session != null)
                    {
                        session.Inspect(this, stats, item);
                    }
                    originalActions?.Process?.Invoke(ctx, item);
                },
                end: originalActions?.End,
                abort: originalActions?.Abort
            );

            _child.Execute(context);
        }
        finally
        {
            _child.Actions = originalActions;
        }

        yield return stats;
    }
}
