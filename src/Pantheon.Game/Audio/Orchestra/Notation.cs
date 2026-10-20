using System;
using System.Collections.Generic;
using System.Globalization;

namespace Pantheon.Game.Audio.Orchestra;

/// <summary>A chord: its root and the notes above it as pitch classes and semitones, and its bass.</summary>
/// <param name="Root">The root's pitch class (C is 0).</param>
/// <param name="Intervals">Semitones above the root of each of its notes, the root first.</param>
/// <param name="Bass">The pitch class in the bass: the root, or a slash chord's bass.</param>
public sealed record Chord(int Root, IReadOnlyList<int> Intervals, int Bass);

/// <summary>A chord held for some beats.</summary>
/// <param name="Chord">The chord.</param>
/// <param name="Beats">How long it lasts.</param>
public sealed record ChordSpan(Chord Chord, float Beats);

/// <summary>A drum's step: how hard it is struck, and whether it is rolled through the step.</summary>
/// <param name="Velocity">How hard (an accent above 1).</param>
/// <param name="Roll">Whether it is a roll.</param>
public readonly record struct Hit(float Velocity, bool Roll);

/// <summary>A chord tone a pattern names: which of the chord's notes, and how many octaves up or down.</summary>
/// <param name="Tone">The chord's note, from 0 (the root).</param>
/// <param name="Octaves">Octaves up (or down when negative).</param>
public readonly record struct Degree(int Tone, int Octaves);

/// <summary>
/// The score's notation (ADR-071): chord symbols (<c>Dm</c>, <c>Bb</c>, <c>F/A</c>, <c>Csus4</c>,
/// <c>A7</c>, <c>C#dim7</c>), notes in scientific pitch (<c>D5</c> is the D above middle C, <c>Bb3</c>),
/// chord-tone patterns, drum patterns and bar ranges. Pure.
/// </summary>
public static class Notation
{
    private static readonly (string Name, int[] Intervals)[] Qualities =
    [
        ("maj7", [0, 4, 7, 11]),
        ("m7b5", [0, 3, 6, 10]),
        ("dim7", [0, 3, 6, 9]),
        ("sus2", [0, 2, 7]),
        ("sus4", [0, 5, 7]),
        ("add9", [0, 4, 7, 14]),
        ("aug", [0, 4, 8]),
        ("dim", [0, 3, 6]),
        ("m7", [0, 3, 7, 10]),
        ("7", [0, 4, 7, 10]),
        ("m", [0, 3, 7]),
        (string.Empty, [0, 4, 7]),
    ];

    /// <summary>A piece's harmony: its chords and how long each lasts.</summary>
    /// <param name="chords">The chords: a symbol a bar, or <c>Dm:2</c> for a chord of so many beats.</param>
    /// <param name="beatsPerBar">Beats in a bar, for a chord without its beats.</param>
    /// <returns>The chords in order.</returns>
    /// <exception cref="FormatException">A symbol does not read.</exception>
    public static List<ChordSpan> Harmony(string chords, int beatsPerBar)
    {
        ArgumentNullException.ThrowIfNull(chords);
        var spans = new List<ChordSpan>();
        foreach (var token in chords.Split([' ', '|', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var (symbol, beats) = Split(token, beatsPerBar);
            spans.Add(new ChordSpan(ChordOf(symbol), beats));
        }

        return spans;
    }

    /// <summary>A chord from its symbol.</summary>
    /// <param name="symbol">For example <c>F#m7</c> or <c>Bb/D</c>.</param>
    /// <returns>The chord.</returns>
    /// <exception cref="FormatException">It does not read.</exception>
    public static Chord ChordOf(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var slash = symbol.IndexOf('/', StringComparison.Ordinal);
        var main = slash < 0 ? symbol : symbol[..slash];
        var (root, rest) = PitchClass(main);
        foreach (var (name, intervals) in Qualities)
        {
            if (rest == name)
            {
                var bass = slash < 0 ? root : PitchClass(symbol[(slash + 1)..]) is (var low, "") ? low : throw new FormatException($"'{symbol}' has no bass note after its slash");
                return new Chord(root, intervals, bass);
            }
        }

        throw new FormatException($"'{symbol}' is not a chord this score knows");
    }

    /// <summary>A note's MIDI number from its name in scientific pitch: <c>C4</c> is 60.</summary>
    /// <param name="note">For example <c>D5</c>, <c>F#3</c> or <c>Bb4</c>.</param>
    /// <returns>Its number.</returns>
    /// <exception cref="FormatException">It does not read.</exception>
    public static int Midi(string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        var (pitch, rest) = PitchClass(note);
        return int.TryParse(rest, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var octave) && octave is >= 0 and <= 9
            ? ((octave + 1) * 12) + pitch
            : throw new FormatException($"'{note}' needs an octave from 0 to 9");
    }

    /// <summary>A melody's notes: <c>D5:1.5</c> a note and its beats (one when left out), <c>-:1</c> a rest.</summary>
    /// <param name="notes">The notes.</param>
    /// <returns>Each note's number (none for a rest) and beats.</returns>
    /// <exception cref="FormatException">A note does not read.</exception>
    public static List<(int? Midi, float Beats)> Melody(string notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        var melody = new List<(int?, float)>();
        foreach (var token in notes.Split([' ', '|', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, beats) = Split(token, 1);
            melody.Add((name == "-" ? null : Midi(name), beats));
        }

        return melody;
    }

    /// <summary>A pattern of chord tones: 1, 3, 5 and 7 the chord's notes; 8, 10, 12 and 14 an octave up; 15 two; a leading <c>-</c> an octave down.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The tones.</returns>
    /// <exception cref="FormatException">A tone does not read.</exception>
    public static List<Degree> Degrees(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var degrees = new List<Degree>();
        foreach (var token in pattern.Split([' ', '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            var down = token.StartsWith('-');
            var degree = (down ? token[1..] : token) switch
            {
                "1" => new Degree(0, 0),
                "3" => new Degree(1, 0),
                "5" => new Degree(2, 0),
                "7" => new Degree(3, 0),
                "8" => new Degree(0, 1),
                "10" => new Degree(1, 1),
                "12" => new Degree(2, 1),
                "14" => new Degree(3, 1),
                "15" => new Degree(0, 2),
                _ => throw new FormatException($"'{token}' is not a chord tone (1, 3, 5, 7, 8, 10, 12, 14, 15)"),
            };
            degrees.Add(down ? degree with { Octaves = degree.Octaves - 1 } : degree);
        }

        return degrees;
    }

    /// <summary>A drum pattern, a step a character: <c>x</c> a hit, <c>X</c> an accent, <c>o</c> a soft hit, <c>r</c> a roll, <c>.</c> a rest.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The steps, a rest as <c>null</c>.</returns>
    /// <exception cref="FormatException">A step does not read.</exception>
    public static List<Hit?> Hits(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var hits = new List<Hit?>();
        foreach (var step in pattern)
        {
            hits.Add(step switch
            {
                'x' => new Hit(1f, false),
                'X' => new Hit(1.3f, false),
                'o' => new Hit(0.55f, false),
                'r' => new Hit(0.7f, true),
                '.' => null,
                ' ' or '|' => (Hit?)new Hit(-1f, false),
                _ => throw new FormatException($"'{step}' is not a step (x, X, o, r or .)"),
            });
        }

        hits.RemoveAll(h => h is { Velocity: < 0f });
        return hits;
    }

    /// <summary>A part's runs of bars, from 1: <c>1-8,13</c>; every bar when empty.</summary>
    /// <param name="bars">The bars.</param>
    /// <param name="total">The piece's bars.</param>
    /// <returns>Each run's first and last bar.</returns>
    /// <exception cref="FormatException">A run does not read or lies outside the piece.</exception>
    public static List<(int First, int Last)> BarRanges(string bars, int total)
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (bars.Trim().Length == 0)
        {
            return [(1, total)];
        }

        var runs = new List<(int, int)>();
        foreach (var run in bars.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dash = run.IndexOf('-', StringComparison.Ordinal);
            var first = Bar(dash < 0 ? run : run[..dash]);
            var last = dash < 0 ? first : Bar(run[(dash + 1)..]);
            runs.Add(first >= 1 && last >= first && last <= total ? (first, last) : throw new FormatException($"bars '{run}' do not lie within 1 to {total}"));
        }

        return runs;
    }

    /// <summary>A bar's number.</summary>
    private static int Bar(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var bar) ? bar : throw new FormatException($"'{text}' is not a bar's number");

    /// <summary>A token's name and its beats after a colon, or the default.</summary>
    private static (string Name, float Beats) Split(string token, float beats)
    {
        var colon = token.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return (token, beats);
        }

        return float.TryParse(token[(colon + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var length) && length > 0f
            ? (token[..colon], length)
            : throw new FormatException($"'{token}' needs a length in beats after its colon");
    }

    /// <summary>A note letter with its accidental, and what follows it.</summary>
    private static (int PitchClass, string After) PitchClass(string text)
    {
        if (text.Length == 0 || text[0] is < 'A' or > 'G')
        {
            throw new FormatException($"'{text}' does not begin with a note from A to G");
        }

        var pitch = text[0] switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            _ => 11,
        };
        var at = 1;
        if (text.Length > 1 && text[1] is '#' or 'b')
        {
            pitch += text[1] == '#' ? 1 : -1;
            at = 2;
        }

        return ((pitch + 12) % 12, text[at..]);
    }
}
