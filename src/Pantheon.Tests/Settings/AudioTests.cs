using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game.Audio;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Settings;

/// <summary>The game's sound (spec §60, ADR-021): synthesized sounds and music, and what the world sounds like.</summary>
public sealed class AudioTests
{
    [Fact]
    public void EverySound_IsMadeTheSameWayEachTime_WithinRange_AndAudible()
    {
        foreach (var kind in Enum.GetValues<SoundKind>())
        {
            var samples = SoundBank.Render(kind);

            Assert.InRange(samples.Length, SoundBank.SampleRate / 4, 8 * SoundBank.SampleRate);
            Assert.All(samples, s => Assert.InRange(s, -1f, 1f));
            Assert.True(samples.Max(Math.Abs) > 0.5f, kind + " is too quiet");
            Assert.Equal(samples, SoundBank.Render(kind));
        }
    }

    [Theory]
    [InlineData(SoundKind.Wind)]
    [InlineData(SoundKind.Rain)]
    public void TheLoops_RunOnIntoTheirStart_WithoutAJump(SoundKind kind)
    {
        var samples = SoundBank.Render(kind);

        Assert.True(SoundBank.Loops(kind));
        Assert.True(Math.Abs(samples[^1] - samples[0]) < 0.25f);
    }

    [Fact]
    public void EachMoodsMusic_IsAStereoLoopOfFourChords_AndTheMoodsDiffer()
    {
        var calm = MusicComposer.Render(MusicMood.Calm);
        var tension = MusicComposer.Render(MusicMood.Tension);
        var triumph = MusicComposer.Render(MusicMood.Triumph);

        Assert.Equal((int)(MusicComposer.LoopSeconds * SoundBank.SampleRate) * 2, calm.Length);
        Assert.All(new[] { calm, tension, triumph }, music => Assert.InRange(music.Max(Math.Abs), 0.5f, 1f));
        Assert.NotEqual(calm, tension);
        Assert.NotEqual(calm, triumph);
        Assert.Equal(calm, MusicComposer.Render(MusicMood.Calm));
    }

    [Fact]
    public void Miracles_Disasters_AndCreatures_AreHeardWhereTheyHappen_AndTheTricksterIsNotHeardAtAll()
    {
        var world = DivineWorlds.Create(100f, 300f);
        var target = new GroundPosition(300f, 512f);
        var events = new List<WorldEvent>
        {
            Cast("lightning", target),
            Cast("food", target),
            Cast("sow_discord", target),
            new EarthquakeStruck { Tick = 0, QuakeId = 1, Epicentre = target, Magnitude = 6f, GodId = null },
            new DomainChosen { Tick = 0, GodId = 2, Domain = GodDomain.War },
            new DomainChosen { Tick = 0, GodId = 1, Domain = GodDomain.Storm },
        };

        var cues = AudioDirector.Cues(events, world, TestContent.Shipped.Miracles, 1);

        Assert.Equal([SoundKind.Bolt, SoundKind.Chime, SoundKind.Rumble, SoundKind.Shimmer], cues.Select(c => c.Kind));
        Assert.Equal(target, cues[0].Position);
        Assert.Null(cues[^1].Position);
    }

    [Fact]
    public void TheMusic_GrowsTenseInWarAndTriumphantInDominion()
    {
        var world = DivineWorlds.Create(100f, 300f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 0f));

        var calm = AudioDirector.Mood(world, 1);
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        var war = AudioDirector.Mood(world, 1);
        world.Gods[0].DominantSinceTick = 0;

        Assert.Equal((MusicMood.Calm, MusicMood.Tension, MusicMood.Triumph), (calm, war, AudioDirector.Mood(world, 1)));
    }

    [Fact]
    public void UnderAStorm_TheRainAndWindAreLoud_AndUnderAClearSkyOnlyABreeze()
    {
        var world = DivineWorlds.Create(100f, 300f);
        var here = new GroundPosition(300f, 512f);

        var clear = AudioDirector.Ambience(WeatherWorlds.Rules, world, here);
        WeatherWorlds.AddCell(world, here, 300f, humidity: 1f, pressure: 1f);
        var storm = AudioDirector.Ambience(WeatherWorlds.Rules, world, here);

        Assert.Equal(0f, clear.Rain);
        Assert.True(clear.Wind is > 0f and < 0.3f);
        Assert.True(storm.Rain > 0.3f && storm.Wind > clear.Wind);
    }

    /// <summary>A miracle god 1 cast.</summary>
    private static MiracleCast Cast(string miracle, GroundPosition target) =>
        new() { Tick = 0, GodId = 1, MiracleId = miracle, Target = target, Cost = 1f, AffectedSettlementIds = [], Deaths = 0 };
}
