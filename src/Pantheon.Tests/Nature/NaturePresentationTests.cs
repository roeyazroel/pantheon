using System;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.History;
using Pantheon.Game.Hud;
using Pantheon.Game.Launch;
using Pantheon.Game.Multiplayer;
using Pantheon.Game.Presentation;
using Pantheon.Game.Weather;
using Pantheon.Simulation.History;
using Pantheon.Simulation.Weather;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Nature;

/// <summary>How the seasons, the extremes and the land's life are told and driven (ADR-028).</summary>
public sealed class NaturePresentationTests
{
    [Fact]
    public void TheWords_NameTheSeasons_TheWaves_AndTheNewWeather()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        world.Tick = 30;
        NatureWorlds.Begin(world, Season.Winter);
        world.Tick += 4;

        Assert.Matches(@"^Winter of year 1 \(0:\d\d left\)$", SeasonText.Describe(NatureWorlds.Rules, world));
        Assert.Null(SeasonText.Describe(NatureWorlds.Rules, SettlementWorlds.Create(100, 200, 100f, 0.5f)));
        Assert.Equal(["A heat wave", "A cold wave"], new[] { WeatherSpell.HeatWave, WeatherSpell.ColdWave }.Select(WeatherText.Spell));
        Assert.Equal(["snow", "fog", "a hurricane"], new[] { WeatherKind.Snow, WeatherKind.Fog, WeatherKind.Hurricane }.Select(WeatherText.Kind));
        Assert.Equal("the hurricane", Pantheon.Game.Disasters.DisasterText.Kind(Domain.Disasters.DisasterKind.Hurricane));
    }

    [Fact]
    public void TheHud_ShowsTheSeason_AndNewWorldsHaveSeasonsUnlessAskedOtherwise()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var even = GameSession.CreateNew(TestContent.Small, new WorldSeed(77), new NewWorldOptions(Seasons: false));

        Assert.StartsWith("Spring of year 1 (2:30 left). ", HudReadout.Build(session, null, null).Weather, StringComparison.Ordinal);
        Assert.NotNull(session.World.Seasons);
        Assert.Null(even.World.Seasons);
    }

    [Fact]
    public void Alerts_TellOfSeasons_Waves_Hurricanes_AndScarredLand()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var mine = world.Settlements[0];
        var theirs = world.Settlements[1];
        WorldEvent[] events =
        [
            new SeasonChanged { Tick = 1, Season = Season.Winter, Year = 2 },
            new WeatherSpellChanged { Tick = 1, Previous = WeatherSpell.Normal, Spell = WeatherSpell.HeatWave, EndsAtTick = 100 },
            new WeatherSpellChanged { Tick = 1, Previous = WeatherSpell.ColdWave, Spell = WeatherSpell.Normal, EndsAtTick = 100 },
            new StormStruck { Tick = 1, SettlementId = mine.Id, CellId = 1, Wind = 0.95f, Hurricane = true },
            new LandScarred { Tick = 1, SettlementId = mine.Id, Scar = 0.2f },
            new LandScarred { Tick = 1, SettlementId = theirs.Id, Scar = 0.2f },
        ];

        var texts = events.Select(e => new AlertFeed().Push([e], world, 1, 1.0).SingleOrDefault()).Select(a => a?.Text).ToList();

        Assert.StartsWith("Winter of year 2: the fields lie fallow", texts[0], StringComparison.Ordinal);
        Assert.StartsWith("A heat wave begins", texts[1], StringComparison.Ordinal);
        Assert.Equal("The cold wave passes.", texts[2]);
        Assert.Equal($"A hurricane strikes {mine.Name}: its houses are torn down and its stores ruined.", texts[3]);
        Assert.StartsWith($"The land around {mine.Name} is scarred", texts[4], StringComparison.Ordinal);
        Assert.Null(texts[5]);
    }

    [Fact]
    public void TheHistory_RemembersGreatStorms_AndScarredLand()
    {
        var world = PantheonWorlds.Create(0.8f);
        var village = world.Settlements[0];

        new HistoryRecorder(TestContent.Shipped.History).Record(
            world,
            [
                new SettlementDamaged { Tick = 1, SettlementId = village.Id, Cause = Domain.Disasters.DisasterKind.Hurricane, HousesLost = 5, Deaths = 0 },
                new LandScarred { Tick = 1, SettlementId = village.Id, Scar = 0.3f },
            ],
            0);

        Assert.Equal(
            [$"The Great Storm of {village.Name}", $"The Scarring of {village.Name}"],
            world.History.Where(r => r.Kind is HistoryKind.Hurricane or HistoryKind.Scar).Select(r => HistoryText.Title(r, world)));
    }

    [Fact]
    public void TheDebugPanel_ShowsTheClimate_AndTheLandsLifeAtTheCursor()
    {
        var world = DivineWorlds.Create(0f, 300f);
        NatureWorlds.Begin(world, Season.Winter);
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = SettlementWorlds.Rules,
            DivineRules = DivineWorlds.Rules,
            WeatherRules = NatureWorlds.Rules,
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "test",
            TerrainIsFallback = false,
            ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 380f,
            RegionBoundariesVisible = false,
        };
        var region = world.Regions.Cells[world.Settlements[0].RegionId];
        (region.Wildlife, region.Damage, region.Scar) = (0.3f, 0.4f, 0.1f);

        var lines = DebugReadout.Build(snapshot).Lines;
        var point = WeatherReadout.DescribePoint(snapshot, world.Settlements[0].Position, region.Id);

        Assert.Contains(lines, l => l.StartsWith("Climate: Winter of year 1", StringComparison.Ordinal) && l.Contains("harvest x0.25", StringComparison.Ordinal));
        Assert.Contains("damage 0.40 (scar 0.10)", point, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRegionMap_AndTheOverlays_ShowSnowAndTheLandsLife()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        var region = world.Regions.Cells[0];
        (region.Snow, region.Wildlife, region.Damage, region.Scar, region.Pollution) = (0.5f, 0.4f, 0.3f, 0.2f, 0.1f);

        Assert.Equal(
            [0.5f, 0.4f, 0.3f, 0.2f, 0.1f],
            new[] { RegionDebugLayer.Snow, RegionDebugLayer.Wildlife, RegionDebugLayer.Damage, RegionDebugLayer.Scar, RegionDebugLayer.Pollution }.Select(l => RegionDebugMap.GetValue(region, l)));
        Assert.Null(RegionDebugMap.Next(RegionDebugLayer.Pollution));
        Assert.Null(OverlayMap.Next(OverlayKind.Nature));

        var image = OverlayMap.Build(world, OverlayKind.Nature, 1, DivineWorlds.Rules, SettlementWorlds.Rules, TestContent.DivineView);

        Assert.StartsWith("Nature overlay", image.Legend, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0xf4, 0xf7, 0xfb }, image.Rgba[..3]);
    }

    [Fact]
    public void LaunchOptions_AndTheDebugCommands_DriveTheSeasonsAndTheWeather()
    {
        var options = LaunchOptions.Parse(["--seasons=off", "--season=winter", "--weather=heatwave"]);
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77), new NewWorldOptions(Seasons: false));
        var rules = session.Content.Weather;

        Assert.Empty(options.Warnings);
        Assert.False(options.NewWorld(TestContent.Shipped).Seasons);
        Assert.Equal((Season.Winter, WeatherAtStart.HeatWave), (options.Season!.Value, options.Weather!.Value));
        Assert.Equal(WeatherAtStart.Hurricane, LaunchOptions.Parse(["--weather=Hurricane"]).Weather);
        Assert.Single(LaunchOptions.Parse(["--season=monsoon"]).Warnings);

        Assert.Equal("This world has no seasons", SeasonCommands.NextSeason(session));
        Assert.StartsWith("Autumn begins", SeasonCommands.SetSeason(session, Season.Autumn), StringComparison.Ordinal);
        Assert.Equal(Season.Autumn, ClimateModel.SeasonOf(rules, session.World));
        SeasonCommands.NextSeason(session);
        Assert.Equal(Season.Winter, ClimateModel.SeasonOf(rules, session.World));

        WeatherCommands.SummonStorm(session, new GroundPosition(500f, 600f), hurricane: true);
        Assert.Equal(WeatherKind.Hurricane, WeatherModel.Classify(rules, session.World.Weather.Cells[^1]));
    }

    [Fact]
    public void TheSnowCover_IsWhite_AsOpaqueAsTheSnowIsDeep_AndAbsentWithoutSnow()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        Assert.Null(SnowCover.Build(world));

        world.Regions.Cells[2].Snow = 1f;
        world.Regions.Cells[5].Snow = 0.5f;
        var image = SnowCover.Build(world)!;

        Assert.Equal((world.Regions.Columns, world.Regions.Rows), (image.Width, image.Height));
        Assert.Equal(new byte[] { 0xf4, 0xf7, 0xfb, 230 }, image.Rgba[8..12]);
        Assert.Equal(115, image.Rgba[(5 * 4) + 3]);
        Assert.Equal(0, image.Rgba[3]);
    }

    [Fact]
    public void TheRegionsSection_CarriesSnowAndTheLandsLife()
    {
        var region = SettlementWorlds.Create(100, 200, 100f, 0.5f).Regions.Cells[3];
        (region.Snow, region.Wildlife, region.Damage, region.Scar, region.Pollution) = (0.5f, 0.4f, 0.3f, 0.2f, 0.1f);
        var replica = SettlementWorlds.Create(100, 200, 100f, 0.5f).Regions.Cells[3];

        RegionChange.Of(region).ApplyTo(replica);

        Assert.Equal((0.5f, 0.4f, 0.3f, 0.2f, 0.1f), (replica.Snow, replica.Wildlife, replica.Damage, replica.Scar, replica.Pollution));
    }
}
