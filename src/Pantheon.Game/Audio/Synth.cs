using System;

namespace Pantheon.Game.Audio;

/// <summary>
/// Small building blocks for synthesized sound (ADR-021): a sine table, seeded noise and envelopes.
/// Deterministic: the same call always gives the same samples.
/// </summary>
internal static class Synth
{
    private const int TableSize = 4096;
    private static readonly float[] Table = BuildTable();

    /// <summary>The sine of a phase given in turns (1 turn = 2π).</summary>
    public static float Sine(double turns)
    {
        var position = (turns - Math.Floor(turns)) * TableSize;
        var index = (int)position;
        var fraction = (float)(position - index);
        return Table[index] + ((Table[(index + 1) & (TableSize - 1)] - Table[index]) * fraction);
    }

    /// <summary>An exponential decay: 1 at the start, 1/e after <paramref name="tau"/> seconds.</summary>
    public static float Decay(double seconds, double tau) => (float)Math.Exp(-seconds / tau);

    /// <summary>A linear rise from 0 to 1 over <paramref name="attack"/> seconds.</summary>
    public static float Rise(double seconds, double attack) => attack <= 0 ? 1f : (float)Math.Min(1.0, seconds / attack);

    /// <summary>Scales samples so the loudest is <paramref name="peak"/>.</summary>
    public static float[] Normalize(float[] samples, float peak)
    {
        var loudest = 0f;
        foreach (var sample in samples)
        {
            loudest = Math.Max(loudest, Math.Abs(sample));
        }

        if (loudest > 0f)
        {
            var scale = peak / loudest;
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }

        return samples;
    }

    /// <summary>Blends a sound's last <paramref name="overlap"/> samples into its first, so it loops without a click; the result is that much shorter.</summary>
    public static float[] Loop(float[] samples, int overlap)
    {
        var length = samples.Length - overlap;
        var looped = new float[length];
        Array.Copy(samples, looped, length);
        for (var i = 0; i < overlap; i++)
        {
            var t = i / (float)overlap;
            looped[i] = (samples[length + i] * (1f - t)) + (samples[i] * t);
        }

        return looped;
    }

    /// <summary>A sine table of one turn.</summary>
    private static float[] BuildTable()
    {
        var table = new float[TableSize];
        for (var i = 0; i < TableSize; i++)
        {
            table[i] = (float)Math.Sin(2.0 * Math.PI * i / TableSize);
        }

        return table;
    }
}

/// <summary>Seeded white noise (xorshift), so every sound is the same each time it is made.</summary>
internal sealed class Noise
{
    private uint _state;

    /// <summary>Creates noise from a seed.</summary>
    public Noise(uint seed) => _state = seed == 0 ? 0x9E3779B9u : seed;

    /// <summary>The next value, from -1 to 1.</summary>
    public float Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (_state / (float)uint.MaxValue * 2f) - 1f;
    }
}

/// <summary>A one-pole low-pass filter.</summary>
internal sealed class LowPass
{
    private float _value;

    /// <summary>Filters one sample; <paramref name="amount"/> near 0 is darker, 1 lets everything through.</summary>
    public float Next(float input, float amount)
    {
        _value += (input - _value) * amount;
        return _value;
    }
}
