using System;

namespace Pantheon.Game.Audio;

/// <summary>
/// The game's music (spec §60's dynamic music, ADR-021): one seamless loop per mood, composed from
/// code: a soft pad playing four chords, a low drone, and a melody of bell notes from the chords'
/// scale, with a stereo echo. Calm is open and slow, Tension dark and pulsing, Triumph bright and
/// quick. Pure and deterministic.
/// </summary>
public static class MusicComposer
{
    /// <summary>Seconds each chord lasts.</summary>
    public const double ChordSeconds = 6.0;

    // Semitones above D of each chord's notes, for the four chords of each mood.
    private static readonly int[][] CalmChords = [[0, 7, 16], [-3, 4, 9], [-7, 0, 7], [-5, 2, 9]];
    private static readonly int[][] TensionChords = [[0, 7, 15], [-4, 3, 8], [-7, 0, 5], [-5, 2, 9]];
    private static readonly int[][] TriumphChords = [[0, 7, 16], [5, 9, 12], [7, 11, 14], [0, 4, 12]];

    // Each mood's melody notes, as semitones above D; -99 is a rest.
    private static readonly int[] CalmMelody = [12, -99, 16, 14, -99, 9, 12, -99, 7, -99, 9, 12, 16, -99, 14, 12];
    private static readonly int[] TensionMelody = [12, -99, -99, 10, -99, 7, -99, -99, 8, -99, 7, -99, 3, -99, 5, -99];
    private static readonly int[] TriumphMelody = [12, 16, 19, 24, 19, 16, 21, 17, 19, 23, 26, 23, 24, 19, 16, 12];

    private const double D3 = 146.83;

    /// <summary>Seconds a loop lasts: four chords.</summary>
    public static double LoopSeconds => 4 * ChordSeconds;

    /// <summary>Composes a mood's loop.</summary>
    /// <param name="mood">The mood.</param>
    /// <returns>Interleaved stereo samples (left, right) from -1 to 1 at <see cref="SoundBank.SampleRate"/>; the end runs on into the start.</returns>
    public static float[] Render(MusicMood mood)
    {
        var (chords, melody, noteSeconds, pulse) = mood switch
        {
            MusicMood.Calm => (CalmChords, CalmMelody, 1.5, 0.0),
            MusicMood.Tension => (TensionChords, TensionMelody, 1.5, 2.0),
            MusicMood.Triumph => (TriumphChords, TriumphMelody, 0.75, 0.0),
        };
        var rate = SoundBank.SampleRate;
        var frames = (int)(LoopSeconds * rate);
        var left = new float[frames];
        var right = new float[frames];
        AddPad(left, right, chords, pulse, mood == MusicMood.Tension ? 0.5f : 1f);
        AddMelody(left, right, melody, noteSeconds, mood == MusicMood.Triumph ? 0.55f : 0.4f);
        Echo(left, right, (int)(0.375 * rate), 0.35f);
        var stereo = new float[frames * 2];
        for (var i = 0; i < frames; i++)
        {
            stereo[2 * i] = left[i];
            stereo[(2 * i) + 1] = right[i];
        }

        return Synth.Normalize(stereo, 0.7f);
    }

    /// <summary>The pad: each chord's notes and a drone an octave below its root, swelling in and out, with an optional pulse.</summary>
    private static void AddPad(float[] left, float[] right, int[][] chords, double pulse, float brightness)
    {
        var rate = SoundBank.SampleRate;
        var chordFrames = (int)(ChordSeconds * rate);
        for (var c = 0; c < chords.Length; c++)
        {
            for (var n = 0; n < chords[c].Length; n++)
            {
                var frequency = D3 * Math.Pow(2.0, chords[c][n] / 12.0);
                var pan = (n - 1) * 0.35f;
                // Each note sounds a little past its chord, wrapping round, so chords overlap smoothly and the loop is seamless.
                for (var k = 0; k < chordFrames + (rate / 2); k++)
                {
                    var t = k / (double)rate;
                    var swell = (float)Math.Sin(Math.PI * Math.Min(1.0, t / (ChordSeconds + 0.5)));
                    var tremolo = pulse > 0 ? 0.65f + (0.35f * Synth.Sine(pulse * t)) : 1f;
                    var tone = Synth.Sine(frequency * t) + (0.3f * brightness * Synth.Sine(2.0 * frequency * t)) + (0.12f * brightness * Synth.Sine(3.0 * frequency * t * 1.002));
                    Put(left, right, (c * chordFrames) + k, tone * swell * tremolo * 0.18f, pan);
                }
            }

            var root = D3 * Math.Pow(2.0, (chords[c][0] - 12) / 12.0);
            for (var k = 0; k < chordFrames + (rate / 2); k++)
            {
                var t = k / (double)rate;
                var swell = (float)Math.Sin(Math.PI * Math.Min(1.0, t / (ChordSeconds + 0.5)));
                Put(left, right, (c * chordFrames) + k, Synth.Sine(root * t) * swell * 0.22f, 0f);
            }
        }
    }

    /// <summary>The melody: bell notes, one every <paramref name="noteSeconds"/>, panned from side to side.</summary>
    private static void AddMelody(float[] left, float[] right, int[] melody, double noteSeconds, float loudness)
    {
        var rate = SoundBank.SampleRate;
        var noteFrames = (int)(noteSeconds * rate);
        var count = (int)(LoopSeconds / noteSeconds);
        for (var n = 0; n < count; n++)
        {
            var step = melody[n % melody.Length];
            if (step == -99)
            {
                continue;
            }

            var frequency = D3 * 2.0 * Math.Pow(2.0, step / 12.0);
            var pan = (n % 2 == 0 ? -0.3f : 0.3f);
            for (var k = 0; k < 2 * rate; k++)
            {
                var t = k / (double)rate;
                var bell = Synth.Sine(frequency * t) + (0.35f * Synth.Sine(frequency * 2.76 * t) * Synth.Decay(t, 0.3));
                Put(left, right, (n * noteFrames) + k, bell * Synth.Rise(t, 0.005) * Synth.Decay(t, 0.7) * loudness * 0.3f, pan);
            }
        }
    }

    /// <summary>A stereo echo that crosses sides, wrapping round the loop.</summary>
    private static void Echo(float[] left, float[] right, int delay, float feedback)
    {
        var frames = left.Length;
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < frames; i++)
            {
                var from = (i - delay + frames) % frames;
                (left[i], right[i]) = (left[i] + (right[from] * feedback * 0.5f), right[i] + (left[from] * feedback * 0.5f));
            }
        }
    }

    /// <summary>Adds a sample to both sides at a frame, wrapping round the loop, panned from -1 (left) to 1 (right).</summary>
    private static void Put(float[] left, float[] right, int frame, float value, float pan)
    {
        var i = frame % left.Length;
        left[i] += value * (1f - Math.Max(0f, pan));
        right[i] += value * (1f + Math.Min(0f, pan));
    }
}
