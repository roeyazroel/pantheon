using System;

namespace Pantheon.Game.Audio.Orchestra;

/// <summary>
/// A state-variable filter (the topology-preserving form), giving low-, band- and high-pass at once.
/// Its cutoff may move as a note plays; the coefficients are worked out again only when asked.
/// </summary>
internal sealed class Svf
{
    private float _a1;
    private float _a2;
    private float _a3;
    private float _k;
    private float _ic1;
    private float _ic2;

    /// <summary>The band-pass output of the last sample.</summary>
    public float Band { get; private set; }

    /// <summary>The high-pass output of the last sample.</summary>
    public float High { get; private set; }

    /// <summary>Sets the cutoff and resonance.</summary>
    /// <param name="cutoff">Cutoff in hertz.</param>
    /// <param name="q">Resonance: 0.7 is flat, higher rings.</param>
    /// <param name="rate">Samples a second.</param>
    public void Tune(float cutoff, float q, int rate)
    {
        var g = MathF.Tan(MathF.PI * Math.Clamp(cutoff, 10f, rate * 0.45f) / rate);
        _k = 1f / Math.Max(0.1f, q);
        _a1 = 1f / (1f + (g * (g + _k)));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    /// <summary>Filters a sample.</summary>
    /// <param name="input">The sample.</param>
    /// <returns>The low-pass output.</returns>
    public float Low(float input)
    {
        var v3 = input - _ic2;
        var v1 = (_a1 * _ic1) + (_a2 * v3);
        var v2 = _ic2 + (_a2 * _ic1) + (_a3 * v3);
        _ic1 = (2f * v1) - _ic1;
        _ic2 = (2f * v2) - _ic2;
        Band = v1;
        High = input - (_k * v1) - v2;
        return v2;
    }
}

/// <summary>Band-limited waveforms: a sawtooth with its step smoothed (PolyBLEP), so high notes do not alias.</summary>
internal static class Blep
{
    /// <summary>A sawtooth from -1 to 1 at a phase (0..1), for a phase step of <paramref name="step"/> a sample.</summary>
    /// <param name="phase">The phase.</param>
    /// <param name="step">The phase's step a sample.</param>
    /// <returns>The sample.</returns>
    public static float Saw(double phase, double step) => (float)((2.0 * phase) - 1.0 - Correction(phase, step));

    /// <summary>The PolyBLEP correction near the wave's step.</summary>
    private static double Correction(double t, double dt)
    {
        if (t < dt)
        {
            t /= dt;
            return t + t - (t * t) - 1.0;
        }

        if (t > 1.0 - dt)
        {
            t = (t - 1.0) / dt;
            return (t * t) + t + t + 1.0;
        }

        return 0.0;
    }
}

/// <summary>
/// The hall (after Jezar's Freeverb): eight damped combs and four all-passes a side, the right side's
/// delays a little longer for width, after a pre-delay. It runs over the loop twice and keeps the
/// second, so the tail of the end rings on into the start.
/// </summary>
internal static class Hall
{
    private static readonly int[] Combs = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617];
    private static readonly int[] AllPasses = [556, 441, 341, 225];
    private const int Spread = 23;

    /// <summary>The hall's answer to what is sent into it, both sides.</summary>
    /// <param name="left">The left send.</param>
    /// <param name="right">The right send.</param>
    /// <param name="rules">The hall's size, damping and pre-delay.</param>
    /// <param name="rate">Samples a second.</param>
    /// <returns>The reverberation, both sides, as long as the sends.</returns>
    public static (float[] Left, float[] Right) Reverberate(float[] left, float[] right, HallRules rules, int rate)
    {
        var scale = rate / 44100.0;
        var feedback = (rules.Room * 0.28f) + 0.7f;
        var damp = rules.Damping * 0.4f;
        var delay = (int)(rules.PreDelayMs * rate / 1000f);
        return (Side(left, 0, scale, feedback, damp, delay), Side(right, Spread, scale, feedback, damp, delay));
    }

    /// <summary>One side's reverberation over two passes of the loop.</summary>
    private static float[] Side(float[] input, int spread, double scale, float feedback, float damp, int delay)
    {
        var frames = input.Length;
        var combs = Array.ConvertAll(Combs, c => new float[Math.Max(1, (int)((c + spread) * scale))]);
        var stores = new float[Combs.Length];
        var combAt = new int[Combs.Length];
        var passes = Array.ConvertAll(AllPasses, a => new float[Math.Max(1, (int)((a + spread) * scale))]);
        var passAt = new int[AllPasses.Length];
        var output = new float[frames];
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < frames; i++)
            {
                var source = input[(((i - delay) % frames) + frames) % frames] * 0.015f;
                var sum = 0f;
                for (var c = 0; c < combs.Length; c++)
                {
                    var buffer = combs[c];
                    var at = combAt[c];
                    var held = buffer[at];
                    stores[c] = (held * (1f - damp)) + (stores[c] * damp);
                    buffer[at] = source + (stores[c] * feedback);
                    combAt[c] = at + 1 == buffer.Length ? 0 : at + 1;
                    sum += held;
                }

                for (var p = 0; p < passes.Length; p++)
                {
                    var buffer = passes[p];
                    var at = passAt[p];
                    var held = buffer[at];
                    buffer[at] = sum + (held * 0.5f);
                    sum = held - sum;
                    passAt[p] = at + 1 == buffer.Length ? 0 : at + 1;
                }

                if (pass == 1)
                {
                    output[i] = sum;
                }
            }
        }

        return output;
    }
}

/// <summary>The master's last touches: no rumble, a loudness for the mood, and peaks rounded off gently.</summary>
internal static class Mastering
{
    /// <summary>
    /// Takes the rumble and any offset out of a side, thins the low end a little (a low shelf about
    /// 150 Hz) so drums and basses weigh without muddying the rest, and lifts the presence about 3 kHz
    /// and the air above 6 kHz, as a hall's microphones hear the bows and the brass; over two passes of
    /// the loop.
    /// </summary>
    /// <param name="side">The samples, changed in place.</param>
    /// <param name="rate">Samples a second.</param>
    public static void HighPass(float[] side, int rate)
    {
        var a = MathF.Exp(-2f * MathF.PI * 32f / rate);
        var shelf = new Svf();
        shelf.Tune(150f, 0.6f, rate);
        var presence = new Svf();
        presence.Tune(3000f, 0.8f, rate);
        var air = new Svf();
        air.Tune(6000f, 0.7f, rate);
        var (x1, y1) = (0f, 0f);
        var output = new float[side.Length];
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < side.Length; i++)
            {
                var y = a * (y1 + side[i] - x1);
                (x1, y1) = (side[i], y);
                presence.Low(y);
                air.Low(y);
                output[i] = y - (0.4f * shelf.Low(y)) + (0.6f * presence.Band) + (0.5f * air.High);
            }
        }

        Array.Copy(output, side, side.Length);
    }

    /// <summary>Scales both sides to a loudness (root mean square), then rounds off the peaks above a knee so none reaches 1.</summary>
    /// <param name="left">The left side, changed in place.</param>
    /// <param name="right">The right side, changed in place.</param>
    /// <param name="loudness">The root mean square wanted.</param>
    public static void Finish(float[] left, float[] right, float loudness)
    {
        var sum = 0.0;
        for (var i = 0; i < left.Length; i++)
        {
            sum += (left[i] * left[i]) + (right[i] * right[i]);
        }

        var rms = Math.Sqrt(sum / Math.Max(1, 2 * left.Length));
        var gain = rms > 1e-6 ? (float)(loudness / rms) : 1f;
        for (var i = 0; i < left.Length; i++)
        {
            left[i] = Round(left[i] * gain);
            right[i] = Round(right[i] * gain);
        }
    }

    /// <summary>A soft knee: straight below 0.7, then bending towards 0.98.</summary>
    private static float Round(float x)
    {
        var size = MathF.Abs(x);
        return size <= 0.7f ? x : MathF.Sign(x) * (0.7f + (0.28f * MathF.Tanh((size - 0.7f) / 0.28f)));
    }
}
