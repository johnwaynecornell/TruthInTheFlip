using System.Runtime.CompilerServices;
using FluentCommandLine;
using JWCFarm.Metrics;
using TruthInTheFlip.Farm.Format;

namespace TruthInTheFlip_CSV_Farm;

/// <summary>
/// Contains experimental and stateful metric extensions.
/// </summary>
/// <remarks>
/// Unlike standard <see cref="SegmentStats"/> metrics—which are stateless, pure functions evaluated 
/// independently on each record/segment—metrics registered here (e.g., <c>BetSameGapTrend</c>) are stateful.
/// <para>
/// Key characteristics:
/// <list type="bullet">
/// <item>
/// <description><b>Statefulness &amp; Sequential Dependence:</b> Evaluation depends on the history of prior evaluations across records within a projection, meaning evaluation order matters.</description>
/// </item>
/// <item>
/// <description><b>Memory &amp; Lifecycle Management:</b> Uses <see cref="ConditionalWeakTable{TKey, TValue}"/> keyed by <see cref="MetricProjection"/> to maintain isolated state per projection while allowing projection instances to be garbage collected naturally without memory leaks.</description>
/// </item>
/// <item>
/// <description><b>Result Caching:</b> Per-object results are memoized to ensure idempotency and prevent duplicate evaluations from corrupting the rolling history queue.</description>
/// </item>
/// </list>
/// </para>
/// </remarks>
public class Experimental
{
    static ConditionalWeakTable<MetricProjection, Dictionary<int, RollingState>> _betSameGapTrendStates = new();
    
    public static void AddToEnv(FluentEnvironment env)
    {
        Func<MetricProjection, int, RollingState> GetState = (MetricProjection projection, int window) =>
        {
            var projectionStates = _betSameGapTrendStates.GetOrCreateValue(projection);
            if (!projectionStates.TryGetValue(window, out RollingState? state))
            {
                state = new RollingState();
                projectionStates[window] = state;
            }

            return state;
        };

        env.Context.Get<MetricCatalogs>().TryGet(typeof(SegmentStats), out var catalog);
        catalog.Add(new MetricDescriptor(
            "BetSameGapTrend",
            typeof(double),
            new()
            {
                new("window", MetricParameterType.Scalar)
            },
            "Gap versus the prior rolling mean over the requested history window.",
            (ctx, obj, args) =>
            {
                int window = (int)(double)args[0]!;

                // resolve state for this projection + metric/window
                // calculate using prior history only
                // append current gap afterward
                // cache result for obj
        
                var state = GetState(ctx.Projection, window);
        
                if (state.Results.TryGetValue(obj, out double priorResult))
                    return priorResult;
        
                double betSame = ctx.Get<double>("mean#BetSameWinRate");
                double same = ctx.Get<double>("mean#SamePercentage");

                double gap = betSame - same;

                double result = double.NaN;

                if (state.History.Count >= window)
                {
                    double priorMean = state.History.Sum() / state.History.Count;
                    result = gap - priorMean;
                }

                state.History.Enqueue(gap);

                while (state.History.Count > window)
                    state.History.Dequeue();

                state.Results[obj] = result;
                return result;
            })
        {
            SourceExpressions =
            [
                "mean#BetSameWinRate",
                "mean#SamePercentage"
            ]
        });
    }
    
    sealed class RollingState
    {
        // History is the sample values
        public Queue<double> History { get; } = new();

        // Results is a cache helping to avoid recomputing the same value
        public Dictionary<object, double> Results { get; } =
            new(ReferenceEqualityComparer.Instance);
    }
    
}