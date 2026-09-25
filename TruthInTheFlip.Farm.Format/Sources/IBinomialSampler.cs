namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Abstraction for generating binomial random variates under a given probability.
/// </summary>
public interface IBinomialSampler
{
    long Sample(long n, double p = 0.5);
}

/// <summary>
/// Fast, deterministic pseudo-random binomial sampler using Xoshiro256** and hybrid exact/Gaussian methods.
/// </summary>
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
            double u1 = NextDouble();
            double u2 = NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

            double mean = n * 0.5;
            double stdDev = 0.5 * Math.Sqrt(n);

            long k = (long)Math.Round(mean + stdDev * z);
            return Math.Clamp(k, 0L, n);
        }

        // Generic fallback for arbitrary p
        double meanGeneric = n * p;
        double stdDevGeneric = Math.Sqrt(n * p * (1.0 - p));
        double u1G = NextDouble();
        double u2G = NextDouble();
        double zG = Math.Sqrt(-2.0 * Math.Log(u1G)) * Math.Cos(2.0 * Math.PI * u2G);
        long kG = (long)Math.Round(meanGeneric + stdDevGeneric * zG);
        return Math.Clamp(kG, 0L, n);
    }
}
