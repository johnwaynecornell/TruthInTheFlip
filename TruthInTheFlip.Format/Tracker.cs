using System.Diagnostics;

namespace TruthInTheFlip.Format;

public interface ITracker
{
    ITrackerStore Store { get; }
    void Reset();
    
    /// <summary>
    /// Core anticipation logic that evaluates whether the next flip matches the predicted pattern.
    /// This method serves as the fallback implementation used by TrackerRunner when no custom
    /// anticipation strategy is provided. It tracks consecutive flip relationships (same vs. different)
    /// and maintains statistics on prediction accuracy, bet distribution, and cumulative performance.
    /// </summary>
    /// <param name="currentFlip">The current flip result (true = heads, false = tails)</param>
    /// <returns>True if the anticipation was correct, false otherwise</returns>
    bool Anticipate(bool currentFlip);
    void Merge(ITracker otherTracker); //Merge a tracker from the same store.
    long total { get; set; }
    
    void WallclockBegin();
    void WallclockEnd();
    void BatchMemberBegin();
    void BatchMemberEnd();
}

public class Tracker : ITracker
{
    //fields begin
    [IsRecord("TruthInTheFlip.v1.0")] public long total { get; set; } = 0;
    [IsRecord("TruthInTheFlip.v1.0")] public long heads { get; set; } = 0;
    [IsRecord("TruthInTheFlip.v1.0")] public long tails { get; set; } = 0;

    [IsRecord("TruthInTheFlip.v1.0")] public long anticipated { get; set; } = 0;
    [IsRecord("TruthInTheFlip.v1.0")] public long baseAnticipated { get; set; } = 0;
    [IsRecord("TruthInTheFlip.v1.0")] public long anticipatedHeads { get; set; } = 0;
    [IsRecord("TruthInTheFlip.v1.0")] public long anticipatedTails { get; set; } = 0;

    //Although included in the file, in a batch scenario it is always false in the file
    //  And with serial use it's effect is microscopic
    public bool priorFlip = false;

    //Although included in the file, in a batch scenario it is always false in the file
    //  And with serial use it's effect is microscopic
    public bool guessAnticipateChange = false;

    public Tracker? trackerInner = null;
    [IsRecord("TruthInTheFlip.v1.0")] public long cumulativeTicks { get; set; } = 0;

    //v1.1.0 additions
    [IsRecord("TruthInTheFlip.v1.1.0")] public long batchTotal { get; set; } = 0;
    [IsRecord("TruthInTheFlip.v1.1.0")] public long wallclockTimeNs { get; set; } = 0;

    [IsRecord("TruthInTheFlip.v1.1.0")] public long batchWallclockTimeNs { get; set; } = 0;
    [IsRecord("TruthInTheFlip.v1.1.0")] public long utcBeginTimeMs { get; set; } = 0;
    [IsRecord("TruthInTheFlip.v1.1.0")] public long utcEndTimeMs { get; set; } = 0;

    [IsRecord("TruthInTheFlip.v1.1.0")] public long betHeads { get; set; } = 0;

    [IsRecord("TruthInTheFlip.v1.1.0")] public long betSame { get; set; } = 0;

    [IsRecord("TruthInTheFlip.v1.1.0")] public long anticipatedSame { get; set; } = 0;

    //fields end

    public const double ExpectedWinRate = 0.5;

    protected long totalBegin;
    protected long batchMemberStartTimestamp;
    protected long wallclockStartTimestamp;


    public virtual void WallclockBegin()
    {
        // Absolute time for external reference (CSV, charting, Python)
        utcBeginTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // High-resolution start for hyper-accurate duration math
        wallclockStartTimestamp = Stopwatch.GetTimestamp();

        totalBegin = total;
    }

    public virtual void WallclockEnd()
    {
        utcEndTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        batchTotal = total - totalBegin;

        // Calculate highly accurate nanoseconds using Stopwatch
        var elapsed = Stopwatch.GetElapsedTime(wallclockStartTimestamp);
        batchWallclockTimeNs = elapsed.Ticks * 100; // 1 Tick = 100ns

        wallclockTimeNs += batchWallclockTimeNs;
    }

    public virtual void BatchMemberBegin()
    {
        batchMemberStartTimestamp = Stopwatch.GetTimestamp();
    }

    public virtual void BatchMemberEnd()
    {
        var elapsed = Stopwatch.GetElapsedTime(batchMemberStartTimestamp);
        cumulativeTicks += elapsed.Ticks;
    }

    public ITrackerStore Store { get; }

    public Tracker(TrackerStore store)
    {
        this.Store = store;
    }

    public virtual void Reset()
    {
        heads = 0;
        tails = 0;

        anticipated = 0;
        baseAnticipated = 0;
        anticipatedHeads = 0;
        anticipatedTails = 0;
        total = 0;

        // Timing & Batch Fields
        cumulativeTicks = 0;
        batchTotal = 0;
        wallclockTimeNs = 0;
        batchWallclockTimeNs = 0;
        utcBeginTimeMs = 0;
        utcEndTimeMs = 0;
        totalBegin = 0;

        // Betting Fields
        betHeads = 0;
        betSame = 0;
        anticipatedSame = 0;

        trackerInner?.Reset();
    }

    public double CalculateZScore(long measure, long count, double expectedRate)
    {
        if (count == 0) return 0;

        double actualWinRate = (double)measure / count;

        // Standard error formula: sqrt((p * (1 - p)) / n)
        double standardError = Math.Sqrt((expectedRate * (1.0 - expectedRate)) / count);

        if (standardError == 0) return 0;

        return (actualWinRate - expectedRate) / standardError;
    }

    public double GetCurrentZScore()
    {
        return CalculateZScore(anticipated, total, ExpectedWinRate);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")] public double ZScore => GetCurrentZScore();

    [IsMetric("TruthInTheFlip.v1.1.0")] public double ZScoreHeads => CalculateZScore(heads, total, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")] public double ZScoreTails => CalculateZScore(tails, total, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public double ZScoreBaseAnticipated => CalculateZScore(baseAnticipated, total, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public double ZScoreAnticipatedHeads => CalculateZScore(anticipatedHeads, heads, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public double ZScoreAnticipatedTails => CalculateZScore(anticipatedTails, tails, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public double ZScoreBetHeads => CalculateZScore(betHeads, total, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public double ZScoreBetTails => CalculateZScore(total - betHeads, total, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")] public double ZScoreBetSame => CalculateZScore(betSame, total, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public double ZScoreBetDiff => CalculateZScore(total - betSame, total, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public double ZScoreAnticipatedSame => CalculateZScore(anticipatedSame, betSame, ExpectedWinRate);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public double ZScoreAnticipatedDiff =>
        CalculateZScore(anticipated - anticipatedSame, total - betSame, ExpectedWinRate);



    public long EstimateTotalFlipsForZScore(double targetZScore)
    {
        if (total == 0) return 0;

        double actualWinRate = (double)anticipated / total;

        // If the win rate drops to exactly 0.5 or lower, a positive Z-score 
        // is mathematically unreachable with the current trend.
        if (actualWinRate <= ExpectedWinRate) return -1;

        double zSquared = targetZScore * targetZScore;

        // ExpectedWinRate * (1 - ExpectedWinRate) is always 0.25 for a 50/50 baseline
        double variance = ExpectedWinRate * (1.0 - ExpectedWinRate);

        double edge = actualWinRate - ExpectedWinRate;
        double edgeSquared = edge * edge;

        // n = (Z^2 * variance) / edge^2
        double estimatedTotal = (zSquared * variance) / edgeSquared;

        return (long)Math.Ceiling(estimatedTotal);
    }

    public long EstimateRemainingFlipsForZScore(double targetZScore)
    {
        long estimatedTotal = EstimateTotalFlipsForZScore(targetZScore);

        // Return -1 if the target is currently unreachable
        if (estimatedTotal <= 0) return -1;

        long remaining = estimatedTotal - total;

        // If the target is already reached, remaining is 0
        return remaining > 0 ? remaining : 0;
    }

    /// <summary>
    /// Mathematically estimates the total number of attempts (n) required to achieve a specific target Z-Score, 
    /// assuming the current observed win rate remains constant.
    /// </summary>
    /// <param name="measure">The number of successful outcomes (e.g., total correct guesses).</param>
    /// <param name="count">The total number of attempts or events processed so far (the sample size).</param>
    /// <param name="expectedRate">The theoretical win rate if the system were perfectly random (typically 0.5).</param>
    /// <param name="targetZScore">The statistical Z-Score threshold to reach (e.g., 1.96 for 95% confidence, 3.0 for 99.7%).</param>
    /// <returns>
    /// The estimated total number of events required to reach the target Z-Score. 
    /// Returns -1 if the current win rate is less than or equal to the expected rate, 
    /// making a positive Z-Score mathematically unreachable with the current trend.
    /// </returns>
    public static long EstimateTotalFlipsForZScore(long measure, long count, double expectedRate, double targetZScore)
    {
        if (count == 0) return 0;

        double actualWinRate = (double)measure / count;

        // If the win rate drops to exactly the baseline or lower, a positive Z-score 
        // is mathematically unreachable with the current trend.
        if (actualWinRate <= expectedRate) return -1;

        double zSquared = targetZScore * targetZScore;

        double variance = expectedRate * (1.0 - expectedRate);

        double edge = actualWinRate - expectedRate;
        double edgeSquared = edge * edge;

        // n = (Z^2 * variance) / edge^2
        double estimatedTotal = (zSquared * variance) / edgeSquared;

        return (long)Math.Ceiling(estimatedTotal);
    }

    /// <summary>
    /// Calculates how many *additional* attempts are required to reach a specific target Z-Score, 
    /// based on the current win rate trajectory.
    /// </summary>
    /// <param name="measure">The number of successful outcomes observed so far.</param>
    /// <param name="count">The total number of attempts processed so far.</param>
    /// <param name="expectedRate">The theoretical win rate if the system were perfectly random (typically 0.5).</param>
    /// <param name="targetZScore">The statistical Z-Score threshold to reach.</param>
    /// <returns>
    /// The estimated number of remaining events needed to hit the target. 
    /// Returns 0 if the target is already reached. 
    /// Returns -1 if the current trajectory makes the target unreachable.
    /// </returns>
    public static long EstimateRemainingFlipsForZScore(long measure, long count, double expectedRate, double targetZScore)
    {
        long estimatedTotal = EstimateTotalFlipsForZScore(measure, count, expectedRate, targetZScore);

        // Return -1 if the target is currently unreachable
        if (estimatedTotal <= 0) return -1;

        long remaining = estimatedTotal - count;

        // If the target is already reached, remaining is 0
        return remaining > 0 ? remaining : 0;
    }

    /// <summary>
    /// Core anticipation logic that evaluates whether the next flip matches the predicted pattern.
    /// This method serves as the fallback implementation used by TrackerRunner when no custom
    /// anticipation strategy is provided. It tracks consecutive flip relationships (same vs. different)
    /// and maintains statistics on prediction accuracy, bet distribution, and cumulative performance.
    /// </summary>
    /// <param name="currentFlip">The current flip result (true = heads, false = tails)</param>
    /// <returns>True if the anticipation was correct, false otherwise</returns>
    public virtual bool Anticipate(bool currentFlip)
    {
        // If anticipating change, expect !priorFlip. If anticipating same, expect priorFlip.

        bool guess = guessAnticipateChange ? (!priorFlip) : priorFlip;
        bool guessOutcome = guess == currentFlip;

        if (guess) betHeads++;

        if (guessOutcome) baseAnticipated++;

        //if (trackerInner != null) result = trackerInner.Anticipate(result) ? !result : result;
        if (trackerInner != null) guessOutcome = trackerInner.Anticipate(guessOutcome) ? guessOutcome : !guessOutcome;

        if (!guessAnticipateChange) betSame++;

        if (guessOutcome)
        {
            if (!guessAnticipateChange) anticipatedSame++;
            if (currentFlip) anticipatedHeads++;
            else anticipatedTails++;

            anticipated++;
        }

        total++;

        // Anticipate the relation, not the value.
        guessAnticipateChange = currentFlip == priorFlip;

        if (currentFlip)
            heads++;
        else
            tails++
                ;

        priorFlip = currentFlip;

        return guessOutcome;
    }

    public static string FormatOffset(double percentage, string format = "F6")
    {
        double offset = percentage - 50.0;
        string sign = offset >= 0 ? "+" : ""; // Negative numbers already include the '-' sign
        return $"50{sign}{offset.ToString(format)}%";
    }

    public static string FormatWithPlus(double v, string format)
    {
        string s = v.ToString(format);
        return (v >= 0 ? "+" : "") + s;
    }

    public override string ToString()
    {
        return $"{total} flips → " +
               $"positive: {HeadsPercentage:F4}% | " +
               $"negative: {TailsPercentage:F4}% | " +
               $"anticipatedPositive: {AnticipatedHeadsPercentage:F6}% | " +
               $"anticipatedNegative: {AnticipatedTailsPercentage:F6}% | " +
               $"anticipated: {AnticipatedPercentage:F6}% | " +
               $"base: {BaseAnticipatedPercentage:F6}% | " +
               $"Z: {ZScore:F6}" +
               (trackerInner != null ? $" | INNER | {trackerInner}" : "");
    }

    public virtual void Merge(ITracker otherTracker)
    {
        // Timing falls under external management, so no timing fields are aggregated here with the exception of 
        //  cumulative ticks which are emerged by nature.
        Tracker other = (Tracker)otherTracker;
        this.heads += other.heads;
        this.tails += other.tails;
        this.anticipated += other.anticipated;
        this.baseAnticipated += other.baseAnticipated;

        this.total += other.total;
        this.anticipatedHeads += other.anticipatedHeads;
        this.anticipatedTails += other.anticipatedTails;
        this.cumulativeTicks += other.cumulativeTicks;

        this.betHeads += other.betHeads;
        this.betSame += other.betSame;
        this.anticipatedSame += other.anticipatedSame;

        if (other.trackerInner != null)
        {
            if (this.trackerInner == null) this.trackerInner = (Tracker)Store.NewTracker();
            this.trackerInner.Merge(other.trackerInner);
        }
    }

    /*
         Guessed ⊂ total
         Positive ⊂ Anticipated ∩ (next == last)
         Negative ⊂ Anticipated ∩ (next != last)
     */
    public static double Percentage(long parts, long whole) => (double)parts / whole * 100;

    [IsMetric("TruthInTheFlip.v1.0")]
    [MetricType("Percentage")]
    public double HeadsPercentage => Percentage(heads, total);

    [IsMetric("TruthInTheFlip.v1.0")]
    [MetricType("Percentage")]
    public double TailsPercentage => Percentage(tails, total);

    [IsMetric("TruthInTheFlip.v1.0")]
    [MetricType("Percentage")]
    public double AnticipatedPercentage => Percentage(anticipated, total);

    [IsMetric("TruthInTheFlip.v1.0")]
    [MetricType("Percentage")]
    public double BaseAnticipatedPercentage => Percentage(baseAnticipated, total);

    [IsMetric("TruthInTheFlip.v1.0")]
    [MetricType("Percentage")]
    public double AnticipatedHeadsPercentage => Percentage(anticipatedHeads, heads);

    [IsMetric("TruthInTheFlip.v1.0")]
    [MetricType("Percentage")]
    public double AnticipatedTailsPercentage => Percentage(anticipatedTails, tails);

    [IsMetric("TruthInTheFlip.v1.0")]
    [MetricType("Percentage")]
    public double BiasDelta => AnticipatedPercentage - BaseAnticipatedPercentage;

    [IsMetric("TruthInTheFlip.v1.0")]
    [MetricType("PercentageDelta")]
    public double InversionGain => AnticipatedPercentage - 50.0;

    // 1. Bet Distribution (The existing ones - Proves our guesses are 50/50 balanced)
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double BetHeadsPercentage => Percentage(betHeads, total);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double BetTailsPercentage => Percentage(total - betHeads, total);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double BetSamePercentage => Percentage(betSame, total);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double BetDiffPercentage => Percentage(total - betSame, total);


    // 2. Bet Win Rates (Precision - "When we bet X, how often did X win?")
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double BetHeadsWinRate => Percentage(anticipatedHeads, betHeads);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double BetTailsWinRate => Percentage(anticipatedTails, total - betHeads);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double BetSameWinRate => Percentage(anticipatedSame, betSame);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double BetDiffWinRate => Percentage(anticipated - anticipatedSame, total - betSame);

    // 3. Win Distribution ("Of all our wins, what percentage were X?")
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double WinDistributionHeads => Percentage(anticipatedHeads, anticipated);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double WinDistributionTails => Percentage(anticipatedTails, anticipated);

    [MetricType("Percentage")]
    [IsMetric("TruthInTheFlip.v1.1.0")]
    public double WinDistributionSame => Percentage(anticipatedSame, anticipated);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double WinDistributionDiff => Percentage(anticipated - anticipatedSame, anticipated);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double AnticipatedSamePercentage => Percentage(anticipatedSame, betSame);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [MetricType("Percentage")]
    public double AnticipatedDiffPercentage => Percentage(anticipated - anticipatedSame, total - betSame);

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public DateTime UtcBeginTime => DateTimeOffset.FromUnixTimeMilliseconds(utcBeginTimeMs).UtcDateTime;

    [IsMetric("TruthInTheFlip.v1.1.0")]
    public DateTime UtcEndTime => DateTimeOffset.FromUnixTimeMilliseconds(utcEndTimeMs).UtcDateTime;

    [IsMetric("TruthInTheFlip.v1.1.0")] public TimeSpan WallclockTime => new TimeSpan(wallclockTimeNs / 100);

    [IsMetric("TruthInTheFlip.v1.1.0")] public TimeSpan BatchWallclockTime => new TimeSpan(batchWallclockTimeNs / 100);

}