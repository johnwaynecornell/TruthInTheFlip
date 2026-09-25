namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Summary statistics comparing an observed metric against an empirical null distribution population.
/// </summary>
public sealed class EmpiricalMetricSummary
{
    public string MetricName { get; }
    public double Observed { get; }
    public double NullMean { get; }
    public double NullStdDev { get; }
    public double NullMedian { get; }
    public double NullP05 { get; }
    public double NullP95 { get; }
    public double Percentile { get; }
    public double LowerTail { get; }
    public double UpperTail { get; }
    public int SampleCount { get; }

    public EmpiricalMetricSummary(
        string metricName,
        double observed,
        double nullMean,
        double nullStdDev,
        double nullMedian,
        double nullP05,
        double nullP95,
        double percentile,
        double lowerTail,
        double upperTail,
        int sampleCount)
    {
        MetricName = metricName;
        Observed = observed;
        NullMean = nullMean;
        NullStdDev = nullStdDev;
        NullMedian = nullMedian;
        NullP05 = nullP05;
        NullP95 = nullP95;
        Percentile = percentile;
        LowerTail = lowerTail;
        UpperTail = upperTail;
        SampleCount = sampleCount;
    }

    /// <summary>
    /// Computes empirical distribution statistics and tail probabilities with +1 sample correction.
    /// </summary>
    public static EmpiricalMetricSummary Compute(string metricName, double observed, IReadOnlyList<double> nullPopulation)
    {
        if (nullPopulation == null || nullPopulation.Count == 0)
        {
            throw new ArgumentException("Null population must not be empty.", nameof(nullPopulation));
        }

        int n = nullPopulation.Count;
        double sum = 0.0;
        int countLeq = 0;
        int countGeq = 0;

        List<double> sorted = new List<double>(n);

        for (int i = 0; i < n; i++)
        {
            double v = nullPopulation[i];
            sorted.Add(v);
            sum += v;

            if (v <= observed)
                countLeq++;
            if (v >= observed)
                countGeq++;
        }

        sorted.Sort();

        double mean = sum / n;

        double varianceSum = 0.0;
        for (int i = 0; i < n; i++)
        {
            double diff = nullPopulation[i] - mean;
            varianceSum += diff * diff;
        }

        double stdDev = n > 1 ? Math.Sqrt(varianceSum / (n - 1)) : 0.0;
        double median = ComputePercentile(sorted, 0.50);
        double p05 = ComputePercentile(sorted, 0.05);
        double p95 = ComputePercentile(sorted, 0.95);

        double percentile = 100.0 * countLeq / n;
        double lowerTail = (countLeq + 1.0) / (n + 1.0);
        double upperTail = (countGeq + 1.0) / (n + 1.0);

        return new EmpiricalMetricSummary(
            metricName,
            observed,
            mean,
            stdDev,
            median,
            p05,
            p95,
            percentile,
            lowerTail,
            upperTail,
            n);
    }

    /// <summary>
    /// Computes a percentile using standard linear interpolation on a sorted list.
    /// </summary>
    public static double ComputePercentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return double.NaN;
        if (sorted.Count == 1) return sorted[0];

        p = Math.Clamp(p, 0.0, 1.0);
        double k = p * (sorted.Count - 1);
        int idx = (int)Math.Floor(k);
        double fraction = k - idx;

        if (idx >= sorted.Count - 1)
            return sorted[sorted.Count - 1];

        return sorted[idx] + fraction * (sorted[idx + 1] - sorted[idx]);
    }
}
