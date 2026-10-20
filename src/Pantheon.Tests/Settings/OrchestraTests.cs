using System;
using System.Linq;
using Pantheon.Game.Audio;
using Pantheon.Game.Audio.Orchestra;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Settings;

/// <summary>The orchestra (ADR-071): the score's notation, the arranging, and a piece played as a seamless loop.</summary>
public sealed class OrchestraTests
{
    [Theory]
    [InlineData("Dm", 2, new[] { 0, 3, 7 }, 2)]
    [InlineData("Bb", 10, new[] { 0, 4, 7 }, 10)]
    [InlineData("F/A", 5, new[] { 0, 4, 7 }, 9)]
    [InlineData("C#dim7", 1, new[] { 0, 3, 6, 9 }, 1)]
    [InlineData("Asus4", 9, new[] { 0, 5, 7 }, 9)]
    [InlineData("Em7", 4, new[] { 0, 3, 7, 10 }, 4)]
    [InlineData("Ebaug", 3, new[] { 0, 4, 8 }, 3)]
    public void ChordSymbols_Read(string symbol, int root, int[] intervals, int bass)
    {
        var chord = Notation.ChordOf(symbol);

        Assert.Equal((root, bass), (chord.Root, chord.Bass));
        Assert.Equal(intervals, chord.Intervals);
    }

    [Fact]
    public void NotesPatternsAndBars_Read_AndNonsenseIsRefused()
    {
        Assert.Equal(60, Notation.Midi("C4"));
        Assert.Equal(74, Notation.Midi("D5"));
        Assert.Equal(58, Notation.Midi("Bb3"));
        Assert.Equal(66, Notation.Midi("F#4"));
        Assert.Equal([(74, 1.5f), (null, 1f), (69, 1f)], Notation.Melody("D5:1.5 -:1 A4").Select(n => (n.Midi, n.Beats)));
        Assert.Equal([new Degree(0, 0), new Degree(2, 1), new Degree(2, -1), new Degree(0, 2)], Notation.Degrees("1 12 -5 15"));
        Assert.Equal([1f, 1.3f, 0.55f], Notation.Hits("xX.o").Where(h => h is not null).Select(h => h!.Value.Velocity));
        Assert.True(Notation.Hits("..r.")[2]!.Value.Roll);
        Assert.Equal([(1, 8), (13, 13)], Notation.BarRanges("1-8,13", 16));
        Assert.Equal([(1, 16)], Notation.BarRanges(string.Empty, 16));
        Assert.Throws<FormatException>(() => Notation.ChordOf("Hm"));
        Assert.Throws<FormatException>(() => Notation.ChordOf("Dzz"));
        Assert.Throws<FormatException>(() => Notation.Midi("D"));
        Assert.Throws<FormatException>(() => Notation.Degrees("2"));
        Assert.Throws<FormatException>(() => Notation.Hits("xz"));
        Assert.Throws<FormatException>(() => Notation.BarRanges("12-20", 16));
    }

    [Fact]
    public void TheShippedMusic_HasAPieceForEveryMood_EveryMelodyFillingWholeBars()
    {
        var music = TestContent.Music;

        Assert.Equal(Enum.GetValues<MusicMood>().Length, music.Scores.Count);
        foreach (var score in music.Scores)
        {
            Assert.InRange(score.Seconds, 30.0, 120.0);
            foreach (var part in score.Parts.Where(p => p.Play == Playing.Melody))
            {
                var beats = Notation.Melody(part.Notes).Sum(n => n.Beats);
                Assert.True(Math.Abs(beats % score.BeatsPerBar) < 1e-3, $"{score.Mood}'s {part.Instrument} melody lasts {beats} beats");
            }
        }
    }

    [Fact]
    public void ABrokenScore_IsRefused()
    {
        var json = System.IO.File.ReadAllText(System.IO.Path.Combine(TestContent.DataDirectory, MusicContent.FileName));

        Assert.Throws<ContentFormatException>(() => MusicContent.Parse(json.Replace("\"Chords\": \"Dm Eb Dm Eb", "\"Chords\": \"Dm Eb Dm", StringComparison.Ordinal)));
        Assert.Throws<ContentFormatException>(() => MusicContent.Parse(json.Replace("\"Mood\": \"Dark\"", "\"Mood\": \"Calm\"", StringComparison.Ordinal)));
        Assert.Throws<ContentFormatException>(() => MusicContent.Parse(json.Replace("D5:1.5 E5:0.5 F5:1 A5:1", "D5:1.5 Q5:0.5 F5:1 A5:1", StringComparison.Ordinal)));
    }

    [Fact]
    public void ChordsAreVoicedNearTheirOctave_EachVoiceMovingLittle()
    {
        var dm = Arranger.Voice(Notation.ChordOf("Dm"), 4, null);
        var bb = Arranger.Voice(Notation.ChordOf("Bb"), 4, dm);

        Assert.All(dm.Concat(bb), note => Assert.InRange(note, 58, 76));
        Assert.True(dm.Zip(bb, (a, b) => Math.Abs(a - b)).Sum() <= 4, "Dm moves to Bb by a step or two");
    }

    [Fact]
    public void APiece_IsArranged_FromItsParts()
    {
        var war = TestContent.Music.Score(MusicMood.War);

        var notes = Arranger.Arrange(war);

        Assert.All(notes, n => Assert.InRange(n.Start, 0.0, war.Seconds));
        Assert.Contains(notes, n => n.Instrument == Instrument.Taiko);
        Assert.Contains(notes, n => n.Shape == NoteShape.Swell);
        Assert.Contains(notes, n => n.Instrument == Instrument.Brass && n.Midi == Notation.Midi("D4"));
        Assert.DoesNotContain(notes, n => n.Instrument == Instrument.Taiko && n.Start < 8 * 4 * 60.0 / war.Tempo);
    }

    [Fact]
    public void APiece_IsPlayedTheSameEachTime_AsAQuietSeamlessLoop()
    {
        var music = TestContent.Music with { SampleRate = 16000 };
        var score = new MoodScore
        {
            Mood = MusicMood.Calm,
            Name = "A test",
            Tempo = 120,
            Bars = 2,
            Level = 0.5f,
            Chords = "Dm A7",
            Parts =
            [
                new ScorePart { Instrument = Instrument.Strings, Play = Playing.Pad, Octave = 4 },
                new ScorePart { Instrument = Instrument.Harp, Play = Playing.Arpeggio, Octave = 4, Pattern = "1 5 8 5" },
                new ScorePart { Instrument = Instrument.Brass, Play = Playing.Melody, Notes = "D4:2 A4:2" },
                new ScorePart { Instrument = Instrument.Choir, Play = Playing.Pad, Octave = 3, Volume = 0.4f },
                new ScorePart { Instrument = Instrument.Timpani, Play = Playing.Rhythm, Step = 1, Pattern = "x..r" },
                new ScorePart { Instrument = Instrument.Cymbal, Play = Playing.Swell, Step = 2, Bars = "2" },
            ],
        };

        var first = Orchestra.Render(score, music);

        Assert.Equal((int)Math.Round(4.0 * music.SampleRate) * 2, first.Length);
        Assert.Equal(first, Orchestra.Render(score, music));
        Assert.All(first, s => Assert.InRange(s, -0.99f, 0.99f));
        var rms = Math.Sqrt(first.Average(s => (double)s * s));
        Assert.InRange(rms, 0.05, 0.3);
        var step = Enumerable.Range(1, 4000).Average(i => Math.Abs(first[2 * i] - first[2 * (i - 1)]));
        Assert.True(Math.Abs(first[^2] - first[0]) < 8 * step, "the end runs on into the start");
    }
}
