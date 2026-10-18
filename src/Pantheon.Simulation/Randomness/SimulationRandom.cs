using System;
using System.Numerics;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Randomness;

/// <summary>
/// Deterministic xoshiro256** pseudo-random generator. Core systems must use this type (never
/// <see cref="System.Random"/>), and its state is persisted through <see cref="RandomState"/>.
/// Instances are not thread-safe.
/// </summary>
public sealed class SimulationRandom
{
    private const double DoubleUnit = 1.0 / (1UL << 53);
    private const float FloatUnit = 1.0f / (1 << 24);

    /// <summary>Above this mean, Poisson sampling switches from Knuth's method to a normal approximation.</summary>
    private const double PoissonKnuthLimit = 30.0;

    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <summary>Creates a generator whose state is expanded from <paramref name="seed"/> with SplitMix64.</summary>
    /// <param name="seed">Any 64-bit seed, including zero.</param>
    public SimulationRandom(ulong seed)
    {
        var splitMix = seed;
        _s0 = NextSplitMix64(ref splitMix);
        _s1 = NextSplitMix64(ref splitMix);
        _s2 = NextSplitMix64(ref splitMix);
        _s3 = NextSplitMix64(ref splitMix);
    }

    /// <summary>Creates a generator positioned exactly at a previously captured state.</summary>
    private SimulationRandom(RandomState state)
    {
        _s0 = state.S0;
        _s1 = state.S1;
        _s2 = state.S2;
        _s3 = state.S3;
    }

    /// <summary>Restores a generator from a persisted state.</summary>
    /// <param name="state">State captured with <see cref="GetState"/>.</param>
    /// <returns>A generator that continues the original sequence.</returns>
    /// <exception cref="ArgumentException">The state is all zeros, which xoshiro cannot leave.</exception>
    public static SimulationRandom FromState(RandomState state)
    {
        if ((state.S0 | state.S1 | state.S2 | state.S3) == 0)
        {
            throw new ArgumentException("An all-zero xoshiro256** state is invalid.", nameof(state));
        }

        return new SimulationRandom(state);
    }

    /// <summary>Captures the current state so the sequence can be resumed later.</summary>
    /// <returns>The serializable generator state.</returns>
    public RandomState GetState() => new(_s0, _s1, _s2, _s3);

    /// <summary>Returns the next 64 uniformly distributed random bits.</summary>
    /// <returns>A uniformly distributed 64-bit value.</returns>
    public ulong NextUInt64()
    {
        var result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        var shifted = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= shifted;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        return result;
    }

    /// <summary>Returns an unbiased integer in <c>[0, maxExclusive)</c> (Lemire's multiply-shift method).</summary>
    /// <param name="maxExclusive">Exclusive upper bound; must be positive.</param>
    /// <returns>A uniformly distributed integer.</returns>
    public int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

        var bound = (ulong)maxExclusive;
        // 2^64 mod bound: low products below this threshold would over-represent some results.
        var threshold = (0UL - bound) % bound;
        while (true)
        {
            var high = Math.BigMul(NextUInt64(), bound, out var low);
            if (low >= threshold)
            {
                return (int)high;
            }
        }
    }

    /// <summary>Returns an unbiased integer in <c>[minInclusive, maxExclusive)</c>.</summary>
    /// <param name="minInclusive">Inclusive lower bound.</param>
    /// <param name="maxExclusive">Exclusive upper bound; must be greater than <paramref name="minInclusive"/>.</param>
    /// <returns>A uniformly distributed integer.</returns>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Upper bound must exceed the lower bound.");
        }

        return minInclusive + NextInt(maxExclusive - minInclusive);
    }

    /// <summary>Returns a double in <c>[0, 1)</c> with 53 bits of precision.</summary>
    /// <returns>A uniformly distributed double.</returns>
    public double NextDouble() => (NextUInt64() >> 11) * DoubleUnit;

    /// <summary>Returns a float in <c>[0, 1)</c> with 24 bits of precision.</summary>
    /// <returns>A uniformly distributed float.</returns>
    public float NextFloat() => (NextUInt64() >> 40) * FloatUnit;

    /// <summary>Samples a Poisson-distributed count, used for statistical births and deaths.</summary>
    /// <param name="mean">Expected number of events; values of zero or less yield zero.</param>
    /// <returns>A non-negative event count.</returns>
    public int NextPoisson(double mean)
    {
        if (double.IsNaN(mean) || double.IsInfinity(mean))
        {
            throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be finite.");
        }

        if (mean <= 0.0)
        {
            return 0;
        }

        return mean <= PoissonKnuthLimit ? SampleKnuthPoisson(mean) : SampleNormalPoisson(mean);
    }

    /// <summary>Knuth's multiplication method: count uniforms until their product drops below e^-mean.</summary>
    private int SampleKnuthPoisson(double mean)
    {
        var limit = Math.Exp(-mean);
        var count = 0;
        var product = NextDouble();
        while (product > limit)
        {
            count++;
            product *= NextDouble();
        }

        return count;
    }

    /// <summary>Normal approximation N(mean, mean), using Box-Muller, for large means.</summary>
    private int SampleNormalPoisson(double mean)
    {
        // 1 - NextDouble() lies in (0, 1], so the logarithm is always finite.
        var radius = Math.Sqrt(-2.0 * Math.Log(1.0 - NextDouble()));
        var angle = 2.0 * Math.PI * NextDouble();
        var sample = mean + (Math.Sqrt(mean) * radius * Math.Cos(angle));
        return Math.Max(0, (int)Math.Round(sample, MidpointRounding.AwayFromZero));
    }

    /// <summary>SplitMix64 step, used to expand a single seed into a full xoshiro state.</summary>
    private static ulong NextSplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
