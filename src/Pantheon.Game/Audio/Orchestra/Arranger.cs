using System;
using System.Collections.Generic;
using System.Linq;

namespace Pantheon.Game.Audio.Orchestra;

/// <summary>One note the orchestra plays.</summary>
/// <param name="Instrument">Who plays it.</param>
/// <param name="Start">When it starts, in seconds from the loop's start.</param>
/// <param name="Length">How long it is held, in seconds (its release rings on after).</param>
/// <param name="Midi">Its pitch (69 is the A at 440 Hz); unused by the unpitched drums.</param>
/// <param name="Velocity">How hard it is played (an accent above 1).</param>
/// <param name="Pan">Where it sits, from -1 (left) to 1 (right).</param>
/// <param name="Shape">A roll or a swell, or plain.</param>
public sealed record NoteEvent(Instrument Instrument, double Start, double Length, float Midi, float Velocity, float Pan, NoteShape Shape = NoteShape.Plain);

/// <summary>How a note is shaped beyond its envelope.</summary>
public enum NoteShape
{
    /// <summary>Struck or played as written.</summary>
    Plain,

    /// <summary>A drum rolled through the note.</summary>
    Roll,

    /// <summary>Swelling into its end: a cymbal's or a drum roll's crescendo into a downbeat.</summary>
    Swell,
}

/// <summary>
/// Turns a mood's score into the notes the orchestra plays (ADR-071): chords voiced near their octave,
/// each voice moving as little as it can from the last; patterns of chord tones; melodies; drum
/// patterns; and swells into downbeats. Pure.
/// </summary>
public static class Arranger
{
    /// <summary>The notes of a piece, in its parts' order.</summary>
    /// <param name="score">The piece.</param>
    /// <returns>Its notes.</returns>
    public static List<NoteEvent> Arrange(MoodScore score)
    {
        ArgumentNullException.ThrowIfNull(score);
        var beat = 60.0 / score.Tempo;
        var timeline = Timeline(score);
        var notes = new List<NoteEvent>();
        foreach (var part in score.Parts)
        {
            var pan = part.Pan ?? DefaultPan(part.Instrument);
            foreach (var (first, last) in Notation.BarRanges(part.Bars, score.Bars))
            {
                var (from, to) = ((first - 1) * score.BeatsPerBar, last * score.BeatsPerBar);
                var arranged = new List<(double Beat, double Beats, float Midi, float Velocity, NoteShape Shape)>();
                Play(part, score, timeline, from, to, arranged);
                foreach (var (at, beats, midi, velocity, shape) in arranged)
                {
                    var dynamic = part.Dynamics.Count == 2 ? part.Dynamics[0] + ((part.Dynamics[1] - part.Dynamics[0]) * (float)((at - from) / Math.Max(1e-6, to - from))) : 1f;
                    notes.Add(new NoteEvent(part.Instrument, at * beat, beats * beat, midi, velocity * part.Volume * dynamic, pan, shape));
                }
            }
        }

        return notes;
    }

    /// <summary>The chord sounding at a beat.</summary>
    /// <param name="timeline">The piece's chords, each with its first beat.</param>
    /// <param name="beat">The beat.</param>
    /// <returns>The chord.</returns>
    public static Chord ChordAt(IReadOnlyList<(double Beat, Chord Chord)> timeline, double beat)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var chord = timeline[0].Chord;
        foreach (var (start, each) in timeline)
        {
            if (start > beat + 1e-6)
            {
                break;
            }

            chord = each;
        }

        return chord;
    }

    /// <summary>A chord's notes voiced near an octave, its voices moving as little as they can from the last voicing.</summary>
    /// <param name="chord">The chord.</param>
    /// <param name="octave">The octave (4 holds middle C).</param>
    /// <param name="last">The last voicing, if any.</param>
    /// <returns>The notes, low to high.</returns>
    public static int[] Voice(Chord chord, int octave, int[]? last)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var centre = ((octave + 1) * 12) + 7;
        var tones = chord.Intervals.Select(i => (chord.Root + i) % 12).Distinct().ToArray();
        int[]? best = null;
        var bestCost = double.MaxValue;
        for (var rotation = 0; rotation < tones.Length; rotation++)
        {
            var notes = new int[tones.Length];
            var low = centre - 8;
            notes[0] = low + ((((tones[rotation] - low) % 12) + 12) % 12);
            for (var k = 1; k < tones.Length; k++)
            {
                var previous = notes[k - 1];
                notes[k] = previous + 1 + ((((tones[(rotation + k) % tones.Length] - (previous + 1)) % 12) + 12) % 12);
            }

            var cost = Math.Abs(notes.Average() - centre) * 0.6;
            if (last is not null && last.Length == notes.Length)
            {
                cost += notes.Zip(last, (a, b) => (double)Math.Abs(a - b)).Sum();
            }

            if (cost < bestCost)
            {
                (best, bestCost) = (notes, cost);
            }
        }

        return best!;
    }

    /// <summary>The piece's chords, each with the beat it starts on.</summary>
    public static List<(double Beat, Chord Chord)> Timeline(MoodScore score)
    {
        ArgumentNullException.ThrowIfNull(score);
        var timeline = new List<(double, Chord)>();
        var at = 0.0;
        foreach (var span in Notation.Harmony(score.Chords, score.BeatsPerBar))
        {
            timeline.Add((at, span.Chord));
            at += span.Beats;
        }

        return timeline;
    }

    /// <summary>One run of a part's bars.</summary>
    private static void Play(ScorePart part, MoodScore score, List<(double Beat, Chord Chord)> timeline, double from, double to, List<(double, double, float, float, NoteShape)> notes)
    {
        switch (part.Play)
        {
            case Playing.Pad:
                Pad(part, timeline, from, to, notes);
                break;
            case Playing.Bass:
                foreach (var (start, end, chord) in Spans(timeline, from, to, score))
                {
                    notes.Add((start, end - start + 0.1, ((part.Octave + 1) * 12) + chord.Bass, 1f, NoteShape.Plain));
                }

                break;
            case Playing.Ostinato or Playing.Arpeggio:
                var degrees = Notation.Degrees(part.Pattern);
                var ring = part.Play == Playing.Arpeggio ? 3.0 : 0.6;
                var step = 0;
                for (var at = from; at < to - 1e-6; at += part.Step, step++)
                {
                    var chord = ChordAt(timeline, at);
                    var degree = degrees[step % degrees.Count];
                    notes.Add((at, part.Step * ring, Tone(chord, degree, part.Octave), step % 2 == 0 ? 1f : 0.82f, NoteShape.Plain));
                }

                break;
            case Playing.Melody:
                var melody = Notation.Melody(part.Notes);
                var beat = from;
                for (var n = 0; beat < to - 1e-6; n++)
                {
                    var (midi, beats) = melody[n % melody.Count];
                    if (midi is { } pitch)
                    {
                        notes.Add((beat, Math.Min(beats, to - beat) * 1.02, pitch, 1f, NoteShape.Plain));
                    }

                    beat += beats;
                }

                break;
            case Playing.Rhythm:
                Rhythm(part, timeline, from, to, notes);
                break;
            case Playing.Swell:
                for (var bar = from; bar < to - 1e-6; bar += score.BeatsPerBar)
                {
                    var end = bar + score.BeatsPerBar;
                    var length = Math.Max(1.0, part.Step);
                    notes.Add((end - length, length, Drum(part.Instrument, ChordAt(timeline, end)), 1f, NoteShape.Swell));
                }

                break;
        }
    }

    /// <summary>A pad: each chord voiced and held, overlapping the next a little so the strings never stop.</summary>
    private static void Pad(ScorePart part, List<(double Beat, Chord Chord)> timeline, double from, double to, List<(double, double, float, float, NoteShape)> notes)
    {
        int[]? last = null;
        for (var i = 0; i < timeline.Count; i++)
        {
            var start = Math.Max(from, timeline[i].Beat);
            var end = Math.Min(to, i + 1 < timeline.Count ? timeline[i + 1].Beat : double.MaxValue);
            if (end <= start)
            {
                continue;
            }

            last = Voice(timeline[i].Chord, part.Octave, last);
            foreach (var midi in last)
            {
                notes.Add((start, end - start + 0.15, midi, 1f, NoteShape.Plain));
            }
        }
    }

    /// <summary>A rhythm: drums struck on their steps, tuned to the chord; other instruments strike the chord short.</summary>
    private static void Rhythm(ScorePart part, List<(double Beat, Chord Chord)> timeline, double from, double to, List<(double, double, float, float, NoteShape)> notes)
    {
        var hits = Notation.Hits(part.Pattern);
        var step = 0;
        for (var at = from; at < to - 1e-6; at += part.Step, step++)
        {
            if (hits[step % hits.Count] is not { } hit)
            {
                continue;
            }

            var chord = ChordAt(timeline, at);
            var shape = hit.Roll ? NoteShape.Roll : NoteShape.Plain;
            if (part.Instrument is Instrument.Timpani or Instrument.Taiko or Instrument.Cymbal)
            {
                notes.Add((at, part.Step, Drum(part.Instrument, chord), hit.Velocity, shape));
                continue;
            }

            foreach (var midi in Voice(chord, part.Octave, null))
            {
                notes.Add((at, part.Step * 0.7, midi, hit.Velocity, shape));
            }
        }
    }

    /// <summary>A drum's pitch: a timpano tuned to the chord's bass in its low octave; the others need none.</summary>
    private static float Drum(Instrument instrument, Chord chord) =>
        instrument == Instrument.Timpani ? 38 + ((((chord.Bass - 2) % 12) + 12) % 12 is var lift && lift > 7 ? lift - 12 : lift) : 48f;

    /// <summary>A chord tone's pitch near an octave.</summary>
    private static float Tone(Chord chord, Degree degree, int octave)
    {
        var interval = degree.Tone < chord.Intervals.Count ? chord.Intervals[degree.Tone] : 12;
        return ((octave + 1) * 12) + chord.Root + interval + (12 * degree.Octaves);
    }

    /// <summary>The chords within a run of beats, each cut to the run.</summary>
    private static IEnumerable<(double Start, double End, Chord Chord)> Spans(List<(double Beat, Chord Chord)> timeline, double from, double to, MoodScore score)
    {
        for (var i = 0; i < timeline.Count; i++)
        {
            var start = Math.Max(from, timeline[i].Beat);
            var end = Math.Min(to, i + 1 < timeline.Count ? timeline[i + 1].Beat : score.Bars * score.BeatsPerBar);
            if (end > start)
            {
                yield return (start, end, timeline[i].Chord);
            }
        }
    }

    /// <summary>An instrument's own place on the stage, as an orchestra sits.</summary>
    private static float DefaultPan(Instrument instrument) => instrument switch
    {
        Instrument.Strings => -0.3f,
        Instrument.Tremolo => 0.25f,
        Instrument.LowStrings => 0.3f,
        Instrument.Spiccato => -0.15f,
        Instrument.Horns => -0.35f,
        Instrument.Brass => 0.3f,
        Instrument.Choir => 0f,
        Instrument.Flute => 0.2f,
        Instrument.Oboe => 0.15f,
        Instrument.Harp => -0.45f,
        Instrument.Celesta => 0.4f,
        Instrument.Timpani => 0.1f,
        Instrument.Taiko => 0f,
        Instrument.Cymbal => 0.3f,
    };
}
