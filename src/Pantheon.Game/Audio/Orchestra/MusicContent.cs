using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Persistence;

namespace Pantheon.Game.Audio.Orchestra;

/// <summary>The orchestra's instruments (ADR-071), all synthesized.</summary>
public enum Instrument
{
    /// <summary>Violins and violas, bowed together: a warm, wide section.</summary>
    Strings,

    /// <summary>The strings' tremolo: a shivering, urgent section.</summary>
    Tremolo,

    /// <summary>Cellos and basses, bowed: the orchestra's floor.</summary>
    LowStrings,

    /// <summary>Strings played short and sharp, for ostinatos.</summary>
    Spiccato,

    /// <summary>French horns: round and noble.</summary>
    Horns,

    /// <summary>Trumpets and trombones: bright and fierce.</summary>
    Brass,

    /// <summary>A choir singing "ah", or "oo" low.</summary>
    Choir,

    /// <summary>A flute.</summary>
    Flute,

    /// <summary>An oboe: reedy and plaintive.</summary>
    Oboe,

    /// <summary>A harp, plucked.</summary>
    Harp,

    /// <summary>A celesta's bells.</summary>
    Celesta,

    /// <summary>Timpani: tuned kettledrums, struck and rolled.</summary>
    Timpani,

    /// <summary>A great drum, felt more than heard.</summary>
    Taiko,

    /// <summary>A suspended cymbal: crashed, or swelled into a downbeat.</summary>
    Cymbal,
}

/// <summary>How a part plays its bars.</summary>
public enum Playing
{
    /// <summary>Holds each chord, voiced near its octave, the voices moving as little as they can.</summary>
    Pad,

    /// <summary>Holds each chord's root (or its slash bass) at its octave.</summary>
    Bass,

    /// <summary>Repeats a pattern of chord tones, each note short.</summary>
    Ostinato,

    /// <summary>Repeats a pattern of chord tones, each note left to ring.</summary>
    Arpeggio,

    /// <summary>Plays its notes, from the first of its bars, over again if they run out.</summary>
    Melody,

    /// <summary>Strikes a pattern of hits (a drum, or the chord short).</summary>
    Rhythm,

    /// <summary>Swells into the downbeat after each of its bars.</summary>
    Swell,
}

/// <summary>The orchestra's music (ADR-071), from <c>game/Data/music.json</c>: its sound and a score for every mood.</summary>
public sealed record MusicContent
{
    /// <summary>The file's name.</summary>
    public const string FileName = "music.json";

    /// <summary>Samples a second the music is made at.</summary>
    public required int SampleRate { get; init; }

    /// <summary>The hall the orchestra plays in.</summary>
    public required HallRules Hall { get; init; }

    /// <summary>A score for every mood.</summary>
    public required IReadOnlyList<MoodScore> Scores { get; init; }

    /// <summary>A mood's score.</summary>
    /// <param name="mood">The mood.</param>
    /// <returns>Its score.</returns>
    public MoodScore Score(MusicMood mood) => Scores.First(s => s.Mood == mood);

    /// <summary>Parses and validates the file.</summary>
    /// <param name="json">Its text.</param>
    /// <returns>The validated content.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or a score does not read.</exception>
    public static MusicContent Parse(string json)
    {
        var content = ContentJson.Parse<MusicContent>(json, FileName);
        content.Validate();
        return content;
    }

    /// <summary>Checks the sound's numbers, and that every mood has a score that reads.</summary>
    private void Validate()
    {
        if (SampleRate is < 16000 or > 48000)
        {
            throw new ContentFormatException($"{FileName}: SampleRate must lie from 16000 to 48000.");
        }

        Hall.Validate();
        foreach (var mood in Enum.GetValues<MusicMood>())
        {
            if (Scores.Count(s => s.Mood == mood) != 1)
            {
                throw new ContentFormatException($"{FileName}: there must be one score for {mood}.");
            }
        }

        foreach (var score in Scores)
        {
            score.Validate();
        }
    }
}

/// <summary>The hall's reverberation: how large it is, how its walls soften the high notes, and how much of it is heard.</summary>
public sealed record HallRules
{
    /// <summary>The room's size (0..1): longer tails as it grows.</summary>
    public required float Room { get; init; }

    /// <summary>How much the walls soften the high notes (0..1).</summary>
    public required float Damping { get; init; }

    /// <summary>How much of the hall is heard (0..1), against the instruments' own sound.</summary>
    public required float Wet { get; init; }

    /// <summary>Milliseconds before the hall answers.</summary>
    public required float PreDelayMs { get; init; }

    /// <summary>Checks the numbers.</summary>
    internal void Validate()
    {
        if (!(Room is >= 0f and < 1f) || !(Damping is >= 0f and <= 1f) || !(Wet is >= 0f and <= 1f) || !(PreDelayMs is >= 0f and <= 200f))
        {
            throw new ContentFormatException($"{MusicContent.FileName}: the hall needs a room from 0 to below 1, damping and wet from 0 to 1, and a pre-delay of 0 to 200 ms.");
        }
    }
}

/// <summary>One mood's piece: its tempo, harmony and parts. It loops, its last bar leading into its first.</summary>
public sealed record MoodScore
{
    /// <summary>The mood it plays for.</summary>
    public required MusicMood Mood { get; init; }

    /// <summary>The piece's name.</summary>
    public required string Name { get; init; }

    /// <summary>Beats a minute.</summary>
    public required float Tempo { get; init; }

    /// <summary>Beats in a bar.</summary>
    public int BeatsPerBar { get; init; } = 4;

    /// <summary>How many bars the piece runs before it loops.</summary>
    public required int Bars { get; init; }

    /// <summary>How loud the piece is against the others (0..1).</summary>
    public required float Level { get; init; }

    /// <summary>The harmony: a chord symbol a bar, or <c>Dm:2 C:2</c> for chords of so many beats; <c>|</c> only reads well.</summary>
    public required string Chords { get; init; }

    /// <summary>The parts.</summary>
    public required IReadOnlyList<ScorePart> Parts { get; init; }

    /// <summary>The piece's length in seconds.</summary>
    public double Seconds => Bars * BeatsPerBar * 60.0 / Tempo;

    /// <summary>Checks the piece reads: its harmony fills its bars, and every part's bars and notes.</summary>
    internal void Validate()
    {
        var where = $"{MusicContent.FileName}: {Mood}";
        if (Tempo is < 30f or > 220f || BeatsPerBar is < 2 or > 8 || Bars is < 2 or > 96 || !(Level is > 0f and <= 1f) || Parts.Count == 0)
        {
            throw new ContentFormatException($"{where} needs a tempo from 30 to 220, 2 to 8 beats a bar, 2 to 96 bars, a level above 0 up to 1, and parts.");
        }

        try
        {
            var harmony = Notation.Harmony(Chords, BeatsPerBar);
            if (Math.Abs(harmony.Sum(c => c.Beats) - (Bars * BeatsPerBar)) > 1e-3)
            {
                throw new FormatException($"the chords last {harmony.Sum(c => c.Beats)} beats, not {Bars * BeatsPerBar}");
            }

            foreach (var part in Parts)
            {
                part.Validate(this);
            }
        }
        catch (FormatException error)
        {
            throw new ContentFormatException($"{where}: {error.Message}.");
        }
    }
}

/// <summary>One part of a piece: an instrument playing some of the bars in its way.</summary>
public sealed record ScorePart
{
    /// <summary>The instrument.</summary>
    public required Instrument Instrument { get; init; }

    /// <summary>How it plays.</summary>
    public required Playing Play { get; init; }

    /// <summary>The bars it plays, from 1: <c>1-8,13-16</c>; all of them when empty.</summary>
    public string Bars { get; init; } = string.Empty;

    /// <summary>The octave its chords and patterns sit in (4 holds middle C).</summary>
    public int Octave { get; init; } = 4;

    /// <summary>How loud it plays (0..1.5).</summary>
    public float Volume { get; init; } = 0.7f;

    /// <summary>Where it sits, from -1 (left) to 1 (right); the instrument's own place when <c>null</c>.</summary>
    public float? Pan { get; init; }

    /// <summary>Beats each step of its pattern lasts.</summary>
    public float Step { get; init; } = 0.5f;

    /// <summary>
    /// Its pattern. For an ostinato or arpeggio, chord tones: 1 the root, 3 the third, 5 the fifth, 7 the
    /// seventh, 8, 10, 12 and 15 the same an octave or two up. For a rhythm, a step each: <c>x</c> a hit,
    /// <c>X</c> an accent, <c>o</c> a soft hit, <c>r</c> a roll, <c>.</c> a rest.
    /// </summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>A melody's notes: <c>D5:1.5</c> a note and its beats, <c>-:1</c> a rest.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Its loudness over each run of bars, from the first to the last (a crescendo or a fade); level when empty.</summary>
    public IReadOnlyList<float> Dynamics { get; init; } = [];

    /// <summary>Checks the part reads.</summary>
    internal void Validate(MoodScore score)
    {
        if (!(Volume is >= 0f and <= 1.5f) || Pan is < -1f or > 1f || !(Step is > 0f and <= 8f) || Octave is < 0 or > 8 || Dynamics.Count is not (0 or 2) || Dynamics.Any(d => d is < 0f or > 2f))
        {
            throw new FormatException($"a {Instrument} part needs a volume from 0 to 1.5, a pan from -1 to 1, a step above 0 up to 8 beats, an octave from 0 to 8 and two dynamics from 0 to 2");
        }

        _ = Notation.BarRanges(Bars, score.Bars);
        switch (Play)
        {
            case Playing.Melody when Notation.Melody(Notes).Count == 0:
                throw new FormatException($"a {Instrument} melody needs notes");
            case Playing.Ostinato or Playing.Arpeggio when Notation.Degrees(Pattern).Count == 0:
                throw new FormatException($"a {Instrument} {Play} needs a pattern of chord tones");
            case Playing.Rhythm when Notation.Hits(Pattern).Count == 0:
                throw new FormatException($"a {Instrument} rhythm needs a pattern of hits");
            case Playing.Pad or Playing.Bass or Playing.Swell or Playing.Melody or Playing.Ostinato or Playing.Arpeggio or Playing.Rhythm:
                break;
        }
    }
}
