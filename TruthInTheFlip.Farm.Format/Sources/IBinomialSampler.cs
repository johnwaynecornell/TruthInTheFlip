namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Abstraction for generating binomial random variates under a given probability.
/// Serves as the replacement seam for alternative exact or specialized large-n sampling algorithms.
/// </summary>
public interface IBinomialSampler
{
    /// <summary>
    /// Samples a single binomial variate ~ Binomial(n, p).
    /// </summary>
    /// <param name="n">Number of independent Bernoulli trials (must be non-negative).</param>
    /// <param name="p">Probability of success in each trial (default: 0.5 for fair null).</param>
    /// <returns>Number of successes k in [0, n].</returns>
    long Sample(long n, double p = 0.5);

    /// <summary>
    /// Generates a standard normal random variate ~ N(0, 1).
    /// </summary>
    double NextGaussian();
}

/// <summary>
/// Fast, deterministic pseudo-random binomial sampler using Xoshiro256** and hybrid exact/Gaussian methods.
/// </summary>
/// <remarks>
/// <para>
/// <b>Random Number Generation:</b>
/// Initialized with a 64-bit seed, expanded via SplitMix64 into a 256-bit internal state for the Xoshiro256** generator.
/// </para>
/// <para>
/// <b>Sampling Regimes for p = 0.5 (Fair Coin Null):</b>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Small n (n &lt;= 64):</b> Exact sampling using hardware bit popcount (<see cref="System.Numerics.BitOperations.PopCount"/>)
/// over a single uniform 64-bit integer.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Medium n (65 &lt;= n &lt;= 256):</b> Exact sampling via chunked 64-bit popcount loops (up to 4 iterations).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Large n (n &gt; 256):</b> Gaussian diffusion approximation with Box-Muller transform:
/// <c>k = round(0.5 * n + 0.5 * sqrt(n) * Z)</c>, where <c>Z ~ N(0, 1)</c>, strictly clamped to <c>[0, n]</c>.
/// </description>
/// </item>
/// </list>
/// </para>
/// <para>
/// <b>Mature Tracker Scale:</b>
/// In real mature tracker files, snapshot increments are typically M = 200,000,000 flips per batch.
/// Under this regime (n &gt;&gt; 256), the Gaussian diffusion approximation is used, providing high performance
/// and asymptotic convergence under the Central Limit Theorem.
/// The <see cref="IBinomialSampler"/> interface remains available as the extension seam should an exact large-n
/// sampler (such as BTPE or rejection sampling) be required in future work.
/// </para>
/// </remarks>
public sealed class FastBinomialSampler : IBinomialSampler
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public FastBinomialSampler(ulong seed)
    {
        // Initialize 256-bit state using SplitMix64
        ulong smState = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        _s0 = NextSplitMix64(ref smState);
        _s1 = NextSplitMix64(ref smState);
        _s2 = NextSplitMix64(ref smState);
        _s3 = NextSplitMix64(ref smState);
    }

    private static ulong NextSplitMix64(ref ulong state)
    {
        ulong z = (state += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private ulong NextUInt64()
    {
        // Xoshiro256**
        ulong result = RotL(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;

        _s2 ^= t;
        _s3 = RotL(_s3, 45);

        return result;
    }

    private static ulong RotL(ulong x, int k) => (x << k) | (x >> (64 - k));

    private double NextDouble()
    {
        // Generate uniform double in (0, 1)
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53)) + (1.0 / (1UL << 54));
    }

    public double NextGaussian()
    {
        double u1 = NextDouble();
        double u2 = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    public long Sample(long n, double p = 0.5)
    {
        if (n <= 0) return 0;

        if (Math.Abs(p - 0.5) < 1e-9)
        {
            if (n <= 64)
            {
                ulong bits = NextUInt64();
                ulong mask = n == 64 ? ~0UL : ((1UL << (int)n) - 1UL);
                return System.Numerics.BitOperations.PopCount(bits & mask);
            }

            if (n <= 256)
            {
                long remaining = n;
                long total = 0;
                while (remaining > 0)
                {
                    int take = (int)Math.Min(64, remaining);
                    ulong bits = NextUInt64();
                    ulong mask = take == 64 ? ~0UL : ((1UL << take) - 1UL);
                    total += System.Numerics.BitOperations.PopCount(bits & mask);
                    remaining -= take;
                }
                return total;
            }

            // Normal approximation for large n with continuity correction
            double z = NextGaussian();

            double mean = n * 0.5;
            double stdDev = 0.5 * Math.Sqrt(n);

            long k = (long)Math.Round(mean + stdDev * z);
            return Math.Clamp(k, 0L, n);
        }

        // Generic fallback for arbitrary p
        double meanGeneric = n * p;
        double stdDevGeneric = Math.Sqrt(n * p * (1.0 - p));
        double zG = NextGaussian();
        long kG = (long)Math.Round(meanGeneric + stdDevGeneric * zG);
        return Math.Clamp(kG, 0L, n);
    }
}
