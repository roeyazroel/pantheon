using System;

namespace Pantheon.Game.Audio.Orchestra;

/// <summary>
/// The orchestra (ADR-071): it plays a mood's piece, every note into the dry mix and its share into
/// the hall, adds the hall's reverberation, takes out the rumble, and sets the piece's loudness by its
/// level, rounding off its peaks. The piece loops: notes and the hall's tail that run past its end ring
/// on into its start. Pure and deterministic: the same score always gives the same samples.
/// </summary>
public static class Orchestra
{
    /// <summary>Plays a piece.</summary>
    /// <param name="score">The piece.</param>
    /// <param name="music">The music's sound: its sample rate and hall.</param>
    /// <returns>Interleaved stereo samples (left, right) from -1 to 1, a whole loop.</returns>
    public static float[] Render(MoodScore score, MusicContent music)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(music);
        var rate = music.SampleRate;
        var buses = new Buses(Math.Max(1, (int)Math.Round(score.Seconds * rate)), rate);
        var notes = Arranger.Arrange(score);
        for (var i = 0; i < notes.Count; i++)
        {
            Voices.Play(notes[i], buses, Seed(score.Mood, i));
        }

        var (wetLeft, wetRight) = Hall.Reverberate(buses.SendLeft, buses.SendRight, music.Hall, rate);
        var (left, right) = (buses.DryLeft, buses.DryRight);
        var wet = music.Hall.Wet * 3f;
        for (var i = 0; i < left.Length; i++)
        {
            left[i] += wetLeft[i] * wet;
            right[i] += wetRight[i] * wet;
        }

        Mastering.HighPass(left, rate);
        Mastering.HighPass(right, rate);
        Mastering.Finish(left, right, 0.1f + (0.1f * score.Level));
        var stereo = new float[left.Length * 2];
        for (var i = 0; i < left.Length; i++)
        {
            stereo[2 * i] = left[i];
            stereo[(2 * i) + 1] = right[i];
        }

        return stereo;
    }

    /// <summary>A note's seed, for its voices' detuning and its noise: the same for the same note of the same piece.</summary>
    private static uint Seed(MusicMood mood, int note) => (uint)(((int)mood + 1) * 7919) ^ (uint)((note + 1) * 2654435761u);
}
