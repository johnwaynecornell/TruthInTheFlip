using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Common abstraction for null trial specifications (e.g. conditional null, algorithmic null).
/// </summary>
public interface INullTrialSpec
{
    /// <summary>
    /// Historical tracker source defining experiment geometry (and predictor decisions for conditional null).
    /// </summary>
    TrackerSelector HistoricalSource { get; }

    /// <summary>
    /// Optional factory for creating custom pseudo-random binomial / Gaussian samplers per seed.
    /// </summary>
    Func<ulong, IBinomialSampler>? SamplerFactory { get; }

    /// <summary>
    /// Materializes the in-memory schedule from the historical source for fast, zero-disk-IO trial replays.
    /// </summary>
    INullSchedule MaterializeSchedule();

    /// <summary>
    /// Creates a synthetic TrackerSelector for a single deterministic null trial under the given seed.
    /// </summary>
    TrackerSelector CreateSyntheticSelector(ulong seed);

    /// <summary>
    /// Creates a single synthetic TrackerStream for a deterministic null trial under the given seed.
    /// </summary>
    TrackerStream CreateSyntheticStream(ulong seed);
}

/// <summary>
/// Common abstraction for an in-memory materialized null replay schedule.
/// </summary>
public interface INullSchedule
{
    /// <summary>
    /// TrackerStore schema used to allocate synthetic tracker records.
    /// </summary>
    TrackerStore Store { get; }

    /// <summary>
    /// Original file path of the historical source tracker, if available.
    /// </summary>
    string? SourcePath { get; }

    /// <summary>
    /// Number of snapshot steps in the schedule.
    /// </summary>
    int StepCount { get; }

    /// <summary>
    /// Creates a synthetic TrackerSelector replaying this schedule under the given seed.
    /// </summary>
    TrackerSelector CreateSelector(ulong seed, Func<ulong, IBinomialSampler>? samplerFactory = null);

    /// <summary>
    /// Creates a single synthetic TrackerStream replaying this schedule under the given seed.
    /// </summary>
    TrackerStream CreateStream(ulong seed, Func<ulong, IBinomialSampler>? samplerFactory = null);

    /// <summary>
    /// Replays the in-memory schedule with a deterministic sampler, yielding synthetic accumulated Tracker records.
    /// </summary>
    IEnumerable<ITracker> Replay(IBinomialSampler sampler);
}
