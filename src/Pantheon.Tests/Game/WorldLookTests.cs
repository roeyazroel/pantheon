using System;
using System.Linq;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Weather;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>How the year and the weather change the look of the land (spec §17, §61, ADR-078).</summary>
public sealed class WorldLookTests
{
    [Theory]
    [InlineData(Season.Spring)]
    [InlineData(Season.Summer)]
    [InlineData(Season.Autumn)]
    [InlineData(Season.Winter)]
    public void ASeason_ShowsInFull_AtItsStart(Season season)
    {
        var (rules, world) = WorldIn(season, seasonShare: 0f);

        var look = WorldLook.Of(rules, world, new GroundPosition(100f, 100f));

        Assert.Equal(season == Season.Spring ? 1f : 0f, look.Spring, 3);
        Assert.Equal(season == Season.Autumn ? 1f : 0f, look.Autumn, 3);
        Assert.Equal(season == Season.Winter ? 1f : 0f, look.Winter, 3);
    }

    [Fact]
    public void ASeason_FadesIntoTheNext_NearItsEnd_NeverDarkeningTheLookAtOnce()
    {
        var (rules, world) = WorldIn(Season.Autumn, seasonShare: 0.9f);

        var look = WorldLook.Of(rules, world, new GroundPosition(100f, 100f));

        Assert.InRange(look.Autumn, 0.05f, 0.95f);
        Assert.InRange(look.Winter, 0.05f, 0.95f);
        Assert.Equal(1f, look.Autumn + look.Winter, 3);
    }

    [Fact]
    public void AWinter_TurnsIntoSpring_AroundTheYear()
    {
        var (rules, world) = WorldIn(Season.Winter, seasonShare: 0.98f);

        var look = WorldLook.Of(rules, world, new GroundPosition(100f, 100f));

        Assert.True(look.Spring > 0.9f && look.Winter < 0.1f);
    }

    [Fact]
    public void AWorldWithoutSeasons_StaysAsItIs()
    {
        var (rules, world) = WorldIn(Season.Winter, seasonShare: 0f);
        world.Seasons = null;

        var look = WorldLook.Of(rules, world, new GroundPosition(100f, 100f));

        Assert.Equal((0f, 0f, 0f), (look.Spring, look.Autumn, look.Winter));
    }

    [Fact]
    public void TheLook_IsTheSameEachTime_AndChangesNothingInTheWorld()
    {
        var (rules, world) = WorldIn(Season.Autumn, seasonShare: 0.8f);
        var tick = world.Tick;

        Assert.Equal(WorldLook.Of(rules, world, new GroundPosition(10f, 10f)), WorldLook.Of(rules, world, new GroundPosition(10f, 10f)));
        Assert.Equal(tick, world.Tick);
    }

    [Fact]
    public void TheDebugPanel_SaysHowFarTheYearHasTurnedTheLand()
    {
        Assert.Equal("Look: spring 0.00, autumn 0.65, winter 0.35.", WeatherReadout.DescribeLook(new WorldLookState(0f, 0.65f, 0.35f, 0f)));
    }

    /// <summary>A generated world some share of the way through a season.</summary>
    private static (WeatherRules Rules, WorldState World) WorldIn(Season season, float seasonShare)
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var rules = session.Content.Weather;
        var world = session.World;
        var length = ClimateModel.SeasonTicks(rules);
        world.Tick += length * 8;
        world.Seasons = new SeasonState { OriginTick = ClimateModel.OriginFor(rules, season, world.Tick - (long)(length * seasonShare)) };
        return (rules, world);
    }
}
