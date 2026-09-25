using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// FarmProcess generating a multi-trial population of deterministic conditional-null trial results.
/// </summary>
public sealed class NullTrialProcess : FarmProcess
{
    public int TrialCount { get; }
    public ulong BaseSeed { get; }
    public ConditionalNullSpec Condition { get; }
    public TrackerWindows.TrackerWindow Window { get; }
    public SegSelector Segmentation { get; }

    public NullTrialProcess(
        int trialCount,
        ulong baseSeed,
        ConditionalNullSpec condition,
        TrackerWindows.TrackerWindow window,
        SegSelector segmentation)
    {
        if (trialCount <= 0) throw new ArgumentOutOfRangeException(nameof(trialCount), "Trial count must be greater than zero.");
        TrialCount = trialCount;
        BaseSeed = baseSeed;
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Segmentation = segmentation ?? throw new ArgumentNullException(nameof(segmentation));
    }

    [FluentMethod("null_trials")]
    [KV_FA(FluentAttribute.Help, "Execute a population of deterministic conditional-null trials.")]
    public static FarmProcess NullTrials(
        [KV_FA(FluentAttribute.Help, "Number of trials to execute.")]
        int trialCount,
        [KV_FA(FluentAttribute.Help, "Base 64-bit random seed.")]
        ulong baseSeed,
        [KV_FA(FluentAttribute.Help, "Conditional null specification with historical tracker source.")]
        ConditionalNullSpec condition,
        [KV_FA(FluentAttribute.Help, "Rolling window bounds.")]
        TrackerWindows.TrackerWindow window,
        [KV_FA(FluentAttribute.Help, "Segmentation definition.")]
        SegSelector segmentation)
    {
        return new NullTrialProcess(trialCount, baseSeed, condition, window, segmentation);
    }

    public override Type StatType => typeof(NullTrialStats);
    public override Type InputType => typeof(NullTrialStats);

    protected override IEnumerable<object> EnumerateItems(FarmContext context)
    {
        var schedule = Condition.MaterializeSchedule();

        for (int i = 0; i < TrialCount; i++)
        {
            ulong trialSeed = DeriveTrialSeed(BaseSeed, i);
            var (agg, segmentCount, _) = NullTrialCommand.RunTrial(
                schedule,
                trialSeed,
                Window,
                Segmentation,
                context,
                Condition.SamplerFactory);

            NullTrialStats stats = NullTrialStats.FromAggregate(i, trialSeed, agg, segmentCount);
            yield return stats;
        }
    }

    /// <summary>
    /// Deterministically derives a 64-bit trial seed from a base seed and trial index using SplitMix64 mixing.
    /// Trial index 0 returns the base seed directly, preserving exact alignment with single-trial runs.
    /// </summary>
    public static ulong DeriveTrialSeed(ulong baseSeed, int trialIndex)
    {
        if (trialIndex == 0)
            return baseSeed;

        ulong indexHash = SplitMix64((ulong)trialIndex * 0x9E3779B97F4A7C15UL);
        return SplitMix64(baseSeed ^ indexHash);
    }

    private static ulong SplitMix64(ulong x)
    {
        ulong z = x + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
