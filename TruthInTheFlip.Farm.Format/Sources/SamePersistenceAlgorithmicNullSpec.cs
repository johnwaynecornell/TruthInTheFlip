using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Specification for an algorithmic-null simulation generating fresh fair-source histories
/// under active SamePersistence strategy feedback matching the historical experiment geometry.
/// </summary>
[KV_FA(FluentAttribute.Help, "Specification for an algorithmic-null simulation with active SamePersistence strategy feedback.")]
public class SamePersistenceAlgorithmicNullSpec : INullTrialSpec
{
    public TrackerSelector HistoricalSource { get; }
    public long StrategyWindowFlips { get; }
    public Func<ulong, IBinomialSampler>? SamplerFactory { get; init; }

    public SamePersistenceAlgorithmicNullSpec(TrackerSelector historicalSource, long strategyWindowFlips = 10_000_000_000L)
    {
        HistoricalSource = historicalSource ?? throw new ArgumentNullException(nameof(historicalSource));

        if (!HistoricalSource.IsAccumulated)
        {
            throw new FarmInputException(
                "same_persistence_algorithmic requires an accumulated historical tracker source. " +
                "Condition the algorithmic null simulation on the accumulated source before applying window; " +
                "an already-windowed or interval-relative source cannot be used as the historical source.");
        }

        if (strategyWindowFlips <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strategyWindowFlips), "Strategy window flips must be greater than zero.");
        }

        StrategyWindowFlips = strategyWindowFlips;
    }

    [FluentMethod("same_persistence_algorithmic")]
    [KV_FA(FluentAttribute.Help, "Execute SamePersistence algorithmic null simulation with active strategy feedback matching historical experiment geometry.")]
    public static INullTrialSpec SamePersistenceAlgorithmic(
        [KV_FA(FluentAttribute.Help, "Historical tracker source defining experiment geometry and batch cadence.")]
        TrackerSelector source,
        [KV_FA(FluentAttribute.Def, "10B")]
        [KV_FA(FluentAttribute.Help, "Strategy decision window size (default 10B).")]
        Count strategyWindow)
    {
        return new SamePersistenceAlgorithmicNullSpec(source, strategyWindow.Value);
    }

    private SamePersistenceAlgorithmicNullSchedule? _cachedSchedule;
    private readonly object _lock = new();

    /// <summary>
    /// Materializes and caches the historical experiment geometry schedule in memory for zero-disk-IO trial replays.
    /// </summary>
    public INullSchedule MaterializeSchedule()
    {
        if (_cachedSchedule != null)
            return _cachedSchedule;

        lock (_lock)
        {
            if (_cachedSchedule != null)
                return _cachedSchedule;

            _cachedSchedule = SamePersistenceAlgorithmicNullSchedule.Materialize(HistoricalSource, StrategyWindowFlips);
            return _cachedSchedule;
        }
    }

    /// <summary>
    /// Creates a synthetic TrackerSelector for a single deterministic algorithmic null trial under the given seed.
    /// </summary>
    public TrackerSelector CreateSyntheticSelector(ulong seed)
    {
        return MaterializeSchedule().CreateSelector(seed, SamplerFactory);
    }

    /// <summary>
    /// Creates a single synthetic TrackerStream for a deterministic algorithmic null trial under the given seed.
    /// </summary>
    public TrackerStream CreateSyntheticStream(ulong seed)
    {
        return MaterializeSchedule().CreateStream(seed, SamplerFactory);
    }
}
