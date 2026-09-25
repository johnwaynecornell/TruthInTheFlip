using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Specification for a conditional-null simulation preserving historical predictor decisions
/// while replacing outcomes with an independent fair source.
/// </summary>
/// <remarks>
/// <para>
/// <b>Conditional Null Semantics:</b>
/// Given the exact historical predictor decisions (<c>betHeads</c> and <c>betTails = total - betHeads</c>)
/// recorded by the strategy at each snapshot step, this generator samples the joint distribution of source
/// outcomes (<c>heads</c>, <c>tails</c>) and anticipation correctness (<c>anticipated</c>, <c>anticipatedHeads</c>,
/// <c>anticipatedTails</c>) under the null hypothesis that the source bits are i.i.d. fair (<c>p = 0.5</c>).
/// </para>
/// <para>
/// <b>Sampling Regimes & Approximations:</b>
/// For each step increment, correct predictions are sampled from <c>Binomial(deltaBetHeads, 0.5)</c> and
/// <c>Binomial(deltaBetTails, 0.5)</c>. The conditional construction preserves the intended joint covariance structure.
/// In the default <see cref="FastBinomialSampler"/>, exact bit-count sampling is used for <c>n &lt;= 256</c>, while for
/// large batches (<c>n &gt; 256</c>, such as 200M-flip tracker increments), a Gaussian diffusion approximation is used.
/// </para>
/// <para>
/// <b>Same/Different Limitations:</b>
/// The synthetic tracker reproduces only the Heads/Tails and Anticipation contingency table.
/// Relational Same/Different state (<c>S_t = 1[F_t == F_{t-1}]</c>) is NOT synthesized under this batch model:
/// <c>betSame</c> is copied strictly as historical provenance / orientation record, while <c>anticipatedSame</c>
/// is fixed at 0. Consequently, Same/Different-derived metrics (<c>same</c>, <c>diff</c>, <c>anticipatedSame</c>,
/// <c>anticipatedDiff</c>, <c>BetSameWinRate</c>, <c>BetDiffWinRate</c>, <c>ZScoreSame</c>, <c>ZScoreDiff</c>)
/// on synthetic records are NOT statistically meaningful and must not be consumed for null inference.
/// Core <c>null_trial</c> and segment aggregate metrics do not consume these fields.
/// </para>
/// </remarks>
[KV_FA(FluentAttribute.Help, "Specification for a conditional-null simulation preserving historical predictor decisions.")]
public class ConditionalNullSpec
{
    public TrackerSelector HistoricalSource { get; }
    public Func<ulong, IBinomialSampler>? SamplerFactory { get; init; }

    public ConditionalNullSpec(TrackerSelector historicalSource)
    {
        HistoricalSource = historicalSource ?? throw new ArgumentNullException(nameof(historicalSource));

        if (!HistoricalSource.IsAccumulated)
        {
            throw new FarmInputException(
                "conditioned requires an accumulated historical tracker source. " +
                "Condition the null simulation on the accumulated source before applying window; " +
                "an already-windowed or interval-relative source cannot be used as the conditioning source.");
        }
    }

    [FluentMethod("conditioned")]
    [KV_FA(FluentAttribute.Help, "Condition null simulation on historical predictor decisions from an accumulated tracker source.")]
    public static ConditionalNullSpec Conditioned(
        [KV_FA(FluentAttribute.Help, "Historical accumulated tracker source.")]
        TrackerSelector source)
    {
        return new ConditionalNullSpec(source);
    }

    private ConditionalNullSchedule? _cachedSchedule;
    private readonly object _lock = new();

    /// <summary>
    /// Materializes and caches the historical predictor schedule in memory for zero-disk-IO trial replays.
    /// </summary>
    public ConditionalNullSchedule MaterializeSchedule()
    {
        if (_cachedSchedule != null)
            return _cachedSchedule;

        lock (_lock)
        {
            if (_cachedSchedule != null)
                return _cachedSchedule;

            _cachedSchedule = ConditionalNullSchedule.Materialize(HistoricalSource);
            return _cachedSchedule;
        }
    }

    /// <summary>
    /// Creates a synthetic TrackerSelector for a single deterministic null trial under the given seed.
    /// </summary>
    public TrackerSelector CreateSyntheticSelector(ulong seed)
    {
        return MaterializeSchedule().CreateSelector(seed, SamplerFactory);
    }

    /// <summary>
    /// Creates a single synthetic TrackerStream for a deterministic null trial under the given seed.
    /// </summary>
    public TrackerStream CreateSyntheticStream(ulong seed)
    {
        return MaterializeSchedule().CreateStream(seed, SamplerFactory);
    }
}
