using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Hud;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Game.Weather;
using Pantheon.Persistence;
using Pantheon.Simulation.Weather;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>How the weather is shown and driven (ADR-013): overlay, clouds, words, readouts, alerts, HUD, labels and debug commands.</summary>
public sealed class WeatherPresentationTests
{
    private static readonly WeatherViewSettings View = TestContent.WeatherView;

    [Fact]
    public void WeatherOverlay_ColoursLandBySoilWater_AndLeavesTheSeaClear()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Regions.Cells[0].Biome = Domain.Ecology.Biome.Sea;
        world.Regions.Cells[1].SoilMoisture = 0f;
        world.Regions.Cells[2].SoilMoisture = SettlementWorlds.Rules.WellWateredSoil;
        world.Regions.Cells[3].SoilMoisture = 1f;

        var image = OverlayMap.Build(world, OverlayKind.Weather, DivineWorlds.GodId, DivineWorlds.Rules, SettlementWorlds.Rules, TestContent.DivineView);

        Assert.Equal(0, image.Rgba[3]);
        Assert.Equal(RgbColor.ToByte(RgbColor.ParseHex("#8b4a1c").R), image.Rgba[4]);
        Assert.Equal(RgbColor.ToByte(RgbColor.ParseHex("#e3c565").G), image.Rgba[9]);
        Assert.Equal(RgbColor.ToByte(RgbColor.ParseHex("#2f7fd8").B), image.Rgba[14]);
        Assert.Contains("drought", image.Legend, StringComparison.Ordinal);
    }

    [Fact]
    public void Clouds_AreLaidOutTheSameEveryTime_WithinTheirRadius_AndMoreForLargerCells()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var small = WeatherWorlds.AddCell(world, new GroundPosition(300f, 300f), 160f, 0.8f, 0.3f);
        var large = WeatherWorlds.AddCell(world, new GroundPosition(300f, 300f), 380f, 0.8f, 0.3f);

        var puffs = CloudLayout.Puffs(world.Seed, large, View);

        Assert.Equal(puffs, CloudLayout.Puffs(world.Seed, large, View));
        Assert.True(puffs.Count > CloudLayout.Puffs(world.Seed, small, View).Count);
        Assert.All(puffs, p => Assert.True(MathF.Sqrt((p.OffsetX * p.OffsetX) + (p.OffsetZ * p.OffsetZ)) <= large.Radius));
        Assert.All(puffs, p => Assert.InRange(p.Height, View.CloudHeight, View.CloudHeight + View.CloudThickness));
    }

    [Fact]
    public void Clouds_DarkenWithStorms_FadeAsTheyDry_AndRainOnlyWhenRaining()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var fair = WeatherWorlds.AddCell(world, new GroundPosition(300f, 300f), 300f, 0.8f, 0.1f);
        var storm = WeatherWorlds.AddCell(world, new GroundPosition(300f, 300f), 300f, 1f, 1f);
        var thin = WeatherWorlds.AddCell(world, new GroundPosition(300f, 300f), 300f, 0.2f, 0.1f);

        var (fairColor, fairOpacity) = CloudLayout.Look(fair, View);
        var (stormColor, stormOpacity) = CloudLayout.Look(storm, View);

        Assert.True(stormColor.R < fairColor.R);
        Assert.True(stormOpacity > fairOpacity);
        Assert.True(CloudLayout.Look(thin, View).Opacity < fairOpacity);
        Assert.Equal(0, CloudLayout.RainDrops(thin, View));
        Assert.True(CloudLayout.RainDrops(storm, View) > CloudLayout.RainDrops(fair, View));
        Assert.True(CloudLayout.Gloom(WeatherWorlds.Rules, world.Weather, new GroundPosition(300f, 300f)) > 0.8f);
        Assert.Equal(0f, CloudLayout.Gloom(WeatherWorlds.Rules, world.Weather, new GroundPosition(900f, 900f)));
    }

    [Fact]
    public void Lightning_StrikesOnlyFromThunderstorms_TheSameWayEveryTime()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var storm = WeatherWorlds.AddCell(world, new GroundPosition(300f, 300f), 300f, 1f, 1f);
        var shower = WeatherWorlds.AddCell(world, new GroundPosition(300f, 300f), 300f, 0.8f, 0.2f);
        var view = View with { FlashesPerSecond = 4f };

        var strikes = Enumerable.Range(0, 200).Select(i => CloudLayout.Lightning(world.Seed, storm, WeatherWorlds.Rules, view, i * 0.25)).ToList();

        Assert.Contains(strikes, s => s is not null);
        Assert.Equal(strikes, Enumerable.Range(0, 200).Select(i => CloudLayout.Lightning(world.Seed, storm, WeatherWorlds.Rules, view, i * 0.25)));
        Assert.All(strikes.OfType<LightningStrike>(), s => Assert.True(MathF.Sqrt((s.OffsetX * s.OffsetX) + (s.OffsetZ * s.OffsetZ)) <= storm.Radius));
        Assert.All(Enumerable.Range(0, 200), i => Assert.Null(CloudLayout.Lightning(world.Seed, shower, WeatherWorlds.Rules, view, i * 0.25)));
    }

    [Theory]
    [InlineData(0f, "the west")]
    [InlineData(MathF.PI, "the east")]
    [InlineData(MathF.PI / 2f, "the north")]
    [InlineData(-MathF.PI / 2f, "the south")]
    public void WindFrom_NamesWhereTheWindComesFrom(float direction, string expected)
    {
        Assert.Equal(expected, WeatherText.WindFrom(direction));
    }

    [Fact]
    public void Describe_NamesTheSpellItsTimeLeftAndTheWind()
    {
        var weather = WeatherWorlds.Calm();
        weather.Spell = WeatherSpell.Dry;
        weather.SpellEndsTick = 2000;

        Assert.Equal("A dry spell (3:20 left): little rain, and the land dries out. Wind from the west.", WeatherText.Describe(weather, tick: 0));
        Assert.Equal("a drought", WeatherText.Drought(0.4f, 0.35f));
        Assert.Equal("a severe drought", WeatherText.Drought(0.8f, 0.35f));
        Assert.Equal("well-watered soil", WeatherText.Drought(0f, 0.35f));
    }

    [Fact]
    public void DebugPanel_ShowsTheWeather_TheCells_AndASettlementsWater()
    {
        var world = DivineWorlds.Create(0f, 300f);
        WeatherWorlds.AddCell(world, new GroundPosition(300f, 512f), 250f, 1f, 1f);
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = SettlementWorlds.Rules,
            DivineRules = DivineWorlds.Rules,
            WeatherRules = WeatherWorlds.Rules,
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "test",
            TerrainIsFallback = false,
            ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 380f,
            RegionBoundariesVisible = false,
            SelectedSettlementId = world.Settlements[0].Id,
        };

        var lines = DebugReadout.Build(snapshot).Lines;

        Assert.Contains(lines, l => l.StartsWith("Weather: Normal weather", StringComparison.Ordinal) && l.EndsWith("1 cells.", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("#1 a thunderstorm at (300, 512)", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("water: soil 0.50 (well-watered soil", StringComparison.Ordinal) && l.Contains("STORM", StringComparison.Ordinal));
    }

    [Fact]
    public void Alerts_WarnOfDrySpells_DroughtAndStormsAmongYourPeople_ButNotStrangers()
    {
        var world = DivineWorlds.Create(0f, 300f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.5f);
        var feed = new AlertFeed();
        WorldEvent[] events =
        [
            new WeatherSpellChanged { Tick = 1, Previous = WeatherSpell.Normal, Spell = WeatherSpell.Dry, EndsAtTick = 9000 },
            new DroughtChanged { Tick = 1, SettlementId = world.Settlements[0].Id, Started = true, Drought = 0.5f },
            new DroughtChanged { Tick = 1, SettlementId = world.Settlements[1].Id, Started = true, Drought = 0.5f },
            new StormStruck { Tick = 1, SettlementId = world.Settlements[0].Id, CellId = 1, Wind = 0.9f },
            new WeatherSpellChanged { Tick = 2, Previous = WeatherSpell.Wet, Spell = WeatherSpell.Normal, EndsAtTick = 9000 },
        ];

        var raised = feed.Push(events, world, DivineWorlds.GodId, 10.0);

        Assert.Equal(
            ["A dry spell begins: little rain will fall, and the land will dry out.", "Drought in Town1: its crops wither. Rain would help.", "A storm strikes Town1: its stores and building work are being ruined."],
            raised.Select(a => a.Text));
        Assert.Equal(AlertSeverity.Danger, raised[2].Severity);
        Assert.Equal(world.Settlements[0].Position, raised[1].Location);
    }

    [Fact]
    public void Hud_ShowsTheWeatherOnlyWhenItMatters_AndNamesYourParchedSettlements()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77), new NewWorldOptions(Seasons: false));
        var world = session.World;
        world.Weather.Spell = WeatherSpell.Normal;
        world.Weather.SpellEndsTick = world.Tick + 600;

        Assert.Null(HudReadout.Build(session, null, null).Weather);

        var home = world.Settlements.First(s => s.Beliefs.Any(b => b.GodId == session.LocalGodId && b.Adherence > 0f));
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0.01f;
        }

        WeatherCommands.SetSpell(session, WeatherSpell.Dry);
        var model = HudReadout.Build(session, null, null);

        Assert.True(model.WeatherWarning);
        Assert.StartsWith("A dry spell (10:00 left)", model.Weather, StringComparison.Ordinal);
        Assert.Contains("Drought in " + home.Name, model.Weather, StringComparison.Ordinal);
    }

    [Fact]
    public void Label_SaysWhenASettlementIsInDrought()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var settlement = world.Settlements[0];

        Assert.Equal("Town1\n100 people", SettlementLabel.Format(settlement, world, SettlementWorlds.Rules));
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0.05f;
        }

        Assert.Equal("Town1\n100 people\ndrought", SettlementLabel.Format(settlement, world, SettlementWorlds.Rules));
    }

    [Fact]
    public void DebugCommands_CycleTheSpell_AndCallAStormAtAPoint()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var spell = session.World.Weather.Spell;

        var status = WeatherCommands.NextSpell(session);
        var storm = WeatherCommands.SummonStorm(session, new GroundPosition(500f, 600f));

        Assert.Equal(WeatherCommands.Next(spell), session.World.Weather.Spell);
        Assert.StartsWith(WeatherText.Spell(session.World.Weather.Spell), status, StringComparison.Ordinal);
        var cell = session.World.Weather.Cells[^1];
        Assert.Equal((500f, 600f), (cell.X, cell.Z));
        Assert.Equal(WeatherKind.Thunderstorm, WeatherModel.Classify(session.Content.Weather, cell));
        Assert.Contains("a thunderstorm", storm, StringComparison.Ordinal);
        Assert.Equal(
            [WeatherSpell.Dry, WeatherSpell.Wet, WeatherSpell.HeatWave, WeatherSpell.ColdWave, WeatherSpell.Normal],
            new[] { WeatherSpell.Normal, WeatherSpell.Dry, WeatherSpell.Wet, WeatherSpell.HeatWave, WeatherSpell.ColdWave }.Select(WeatherCommands.Next));
    }

    [Theory]
    [InlineData("dry", WeatherAtStart.Dry)]
    [InlineData("Storm", WeatherAtStart.Storm)]
    public void LaunchOption_SetsUpTheWeather(string value, WeatherAtStart expected)
    {
        var options = LaunchOptions.Parse(["--weather=" + value, "--overlay=weather"]);

        Assert.Equal(expected, options.Weather);
        Assert.Equal(OverlayKind.Weather, options.Overlay);
        Assert.Empty(options.Warnings);
        Assert.Single(LaunchOptions.Parse(["--weather=hail"]).Warnings);
    }

    [Fact]
    public void ShippedWeatherContent_Parses_AndBrokenRulesAreRejected()
    {
        var weather = TestContent.Shipped.Weather;
        Assert.Equal(5, weather.StepIntervalTicks);
        Assert.True(View.FadeFarDistance > View.FadeNearDistance);

        var text = TestContent.ReadText(ContentJson.WeatherFileName).Replace("\"MaxRadius\": 380.0", "\"MaxRadius\": 100.0", StringComparison.Ordinal);
        var files = GameContentFiles.Read(name => name == ContentJson.WeatherFileName ? text : TestContent.ReadText(name));
        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));
        Assert.Contains("weather.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MaxRadius", exception.Message, StringComparison.Ordinal);
        Assert.Throws<ContentFormatException>(() => WeatherViewSettings.Parse(TestContent.ReadText(WeatherViewSettings.FileName).Replace("\"FadeFarDistance\": 600.0", "\"FadeFarDistance\": 10.0", StringComparison.Ordinal)));
    }
}
