using System;

namespace Pantheon.Game.Audio;

/// <summary>
/// Makes the game's sounds from code (spec §60, ADR-021): no recordings ship with the game, so
/// every sound is a short recipe of tones, noise and envelopes. Pure and deterministic; the client
/// turns the samples into audio streams.
/// </summary>
public static class SoundBank
{
    /// <summary>The sample rate the sounds are made at, in samples per second.</summary>
    public const int SampleRate = 22050;

    /// <summary>Whether a sound is meant to loop (the wind and the rain).</summary>
    /// <param name="kind">The sound.</param>
    /// <returns><c>true</c> for loops.</returns>
    public static bool Loops(SoundKind kind) => kind is SoundKind.Wind or SoundKind.Rain;

    /// <summary>Makes a sound.</summary>
    /// <param name="kind">The sound.</param>
    /// <returns>Mono samples from -1 to 1 at <see cref="SampleRate"/>.</returns>
    public static float[] Render(SoundKind kind) => kind switch
    {
        SoundKind.Chime => Bells(2.4, (0.0, 880.0), (0.12, 1318.5)),
        SoundKind.Shimmer => Shimmer(),
        SoundKind.Bolt => Bolt(),
        SoundKind.Thunder => Thunder(),
        SoundKind.Rumble => Rumble(),
        SoundKind.Crackle => Crackle(),
        SoundKind.Roar => Roar(),
        SoundKind.Alert => Tones(0.45, 0.6f, (0.0, 659.3), (0.12, 987.8)),
        SoundKind.Warning => Tones(0.8, 0.9f, (0.0, 392.0), (0.22, 293.7)),
        SoundKind.Fanfare => Bells(2.6, (0.0, 523.3), (0.14, 659.3), (0.28, 784.0), (0.42, 1046.5)),
        SoundKind.Wind => Wind(),
        SoundKind.Rain => Rain(),
    };

    /// <summary>Struck bells: inharmonic partials that ring out, one strike per note.</summary>
    private static float[] Bells(double seconds, params (double Start, double Frequency)[] notes)
    {
        var samples = new float[(int)(seconds * SampleRate)];
        double[] partials = [1.0, 2.76, 5.4, 8.93];
        double[] weights = [1.0, 0.45, 0.22, 0.1];
        foreach (var (start, frequency) in notes)
        {
            for (var i = (int)(start * SampleRate); i < samples.Length; i++)
            {
                var t = (i / (double)SampleRate) - start;
                var value = 0f;
                for (var p = 0; p < partials.Length; p++)
                {
                    value += (float)weights[p] * Synth.Sine(frequency * partials[p] * t) * Synth.Decay(t, 0.9 / (1.0 + p));
                }

                samples[i] += value * Synth.Rise(t, 0.004);
            }
        }

        return Synth.Normalize(samples, 0.8f);
    }

    /// <summary>Soft sine tones one after another, each fading out.</summary>
    private static float[] Tones(double seconds, float peak, params (double Start, double Frequency)[] notes)
    {
        var samples = new float[(int)(seconds * SampleRate)];
        foreach (var (start, frequency) in notes)
        {
            for (var i = (int)(start * SampleRate); i < samples.Length; i++)
            {
                var t = (i / (double)SampleRate) - start;
                samples[i] += (Synth.Sine(frequency * t) + (0.2f * Synth.Sine(3.0 * frequency * t))) * Synth.Rise(t, 0.01) * Synth.Decay(t, 0.16);
            }
        }

        return Synth.Normalize(samples, peak);
    }

    /// <summary>A cluster of high partials gliding upwards and twinkling.</summary>
    private static float[] Shimmer()
    {
        var samples = new float[(int)(2.0 * SampleRate)];
        double[] partials = [1.0, 1.5, 2.0, 2.5, 3.0];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var glide = 600.0 * (1.0 + (0.25 * t));
            var value = 0f;
            for (var p = 0; p < partials.Length; p++)
            {
                value += Synth.Sine(glide * partials[p] * t) * (0.5f + (0.5f * Synth.Sine((3.0 + p) * t))) / (1f + p);
            }

            samples[i] = value * Synth.Rise(t, 0.4) * Synth.Decay(Math.Max(0.0, t - 0.6), 0.5);
        }

        return Synth.Normalize(samples, 0.6f);
    }

    /// <summary>A crack of bright noise, then a low rolling tail.</summary>
    private static float[] Bolt()
    {
        var samples = new float[(int)(2.4 * SampleRate)];
        var noise = new Noise(7);
        var (bright, low) = (new LowPass(), new LowPass());
        var previous = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var white = noise.Next();
            var crack = (white - previous) * Synth.Decay(t, 0.06);
            previous = white;
            var tail = low.Next(white, 0.02f) * Synth.Rise(t, 0.05) * Synth.Decay(t, 0.7) * 6f;
            samples[i] = bright.Next(crack, 0.6f) + tail;
        }

        return Synth.Normalize(samples, 0.95f);
    }

    /// <summary>Thunder far away: dark noise rolling in waves.</summary>
    private static float[] Thunder()
    {
        var samples = new float[(int)(4.2 * SampleRate)];
        var noise = new Noise(11);
        var (first, second) = (new LowPass(), new LowPass());
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var roll = 0.6f + (0.4f * Synth.Sine(1.7 * t)) * Synth.Sine(0.6 * t + 0.2);
            samples[i] = second.Next(first.Next(noise.Next(), 0.05f), 0.08f) * roll * Synth.Rise(t, 0.15) * Synth.Decay(t, 1.3);
        }

        return Synth.Normalize(samples, 0.9f);
    }

    /// <summary>The ground shaking: very low noise and a wobbling hum.</summary>
    private static float[] Rumble()
    {
        var samples = new float[(int)(4.5 * SampleRate)];
        var noise = new Noise(13);
        var (first, second) = (new LowPass(), new LowPass());
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var hum = Synth.Sine((38.0 + (6.0 * Synth.Sine(0.8 * t))) * t);
            var deep = second.Next(first.Next(noise.Next(), 0.03f), 0.04f) * 8f;
            samples[i] = (deep + (0.5f * hum)) * Synth.Rise(t, 0.6) * Synth.Decay(Math.Max(0.0, t - 1.2), 1.1);
        }

        return Synth.Normalize(samples, 0.95f);
    }

    /// <summary>Flames: a hiss with pops at seeded moments.</summary>
    private static float[] Crackle()
    {
        var samples = new float[(int)(2.6 * SampleRate)];
        var noise = new Noise(17);
        var hiss = new LowPass();
        var pop = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            if (noise.Next() > 0.9985f)
            {
                pop = 1f;
            }

            pop *= 0.992f;
            samples[i] = ((hiss.Next(noise.Next(), 0.35f) * 0.25f) + (noise.Next() * pop)) * Synth.Rise(t, 0.2) * Synth.Decay(Math.Max(0.0, t - 1.6), 0.4);
        }

        return Synth.Normalize(samples, 0.7f);
    }

    /// <summary>A Creature's roar: a buzzing voice sliding down, with breath.</summary>
    private static float[] Roar()
    {
        var samples = new float[(int)(1.5 * SampleRate)];
        var noise = new Noise(19);
        var (throat, breath) = (new LowPass(), new LowPass());
        var phase = 0.0;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var pitch = 120.0 - (40.0 * t) + (6.0 * Synth.Sine(7.0 * t));
            phase += pitch / SampleRate;
            var buzz = (float)((2.0 * (phase - Math.Floor(phase))) - 1.0);
            var voice = throat.Next(buzz, 0.12f) * 3f;
            var air = breath.Next(noise.Next(), 0.25f);
            samples[i] = (voice + (0.4f * air)) * Synth.Rise(t, 0.08) * Synth.Decay(Math.Max(0.0, t - 0.5), 0.35);
        }

        return Synth.Normalize(samples, 0.9f);
    }

    /// <summary>A looping wind: noise whose brightness swells and falls.</summary>
    private static float[] Wind()
    {
        var overlap = SampleRate;
        var samples = new float[(6 * SampleRate) + overlap];
        var noise = new Noise(23);
        var air = new LowPass();
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (double)SampleRate;
            var gust = 0.5f + (0.3f * Synth.Sine(0.17 * t)) + (0.2f * Synth.Sine(0.41 * t));
            samples[i] = air.Next(noise.Next(), 0.015f + (0.05f * gust)) * gust;
        }

        return Synth.Normalize(Synth.Loop(samples, overlap), 0.7f);
    }

    /// <summary>A looping rain: a bright hiss with drops.</summary>
    private static float[] Rain()
    {
        var overlap = SampleRate / 2;
        var samples = new float[(4 * SampleRate) + overlap];
        var noise = new Noise(29);
        var (hiss, previous) = (new LowPass(), 0f);
        var drop = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var white = noise.Next();
            if (noise.Next() > 0.996f)
            {
                drop = 0.8f;
            }

            drop *= 0.97f;
            samples[i] = (hiss.Next(white - previous, 0.5f) * 0.5f) + (white * drop * 0.3f);
            previous = white;
        }

        return Synth.Normalize(Synth.Loop(samples, overlap), 0.6f);
    }
}
