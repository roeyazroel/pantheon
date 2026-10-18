using System;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Disasters;
using Pantheon.Game.Hud;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Simulation.Disasters;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>How disasters are shown and driven (ADR-014): overlay, labels, alerts, HUD, readouts and debug commands.</summary>
public sealed class DisasterPresentationTests
{
    [Fact]
    public void DangerOverlay_ShowsFireAshAndShields()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Regions.Cells[0].Fire = 1f;
        world.Regions.Cells[1].Scorch = 0.5f;
        world.Settlements[0].ShieldedUntilTick = 100;

        var image = OverlayMap.Build(world, OverlayKind.Danger, DivineWorlds.GodId, DivineWorlds.Rules, SettlementWorlds.Rules, TestContent.DivineView);
        var shielded = world.Settlements[0].RegionId * 4;

        Assert.Equal(RgbColor.ToByte(RgbColor.ParseHex("#ff5a1f").R), image.Rgba[0]);
        Assert.Equal(RgbColor.ToByte(RgbColor.ParseHex("#3a2e26").R), image.Rgba[4]);
        Assert.Equal(RgbColor.ToByte(RgbColor.ParseHex("#7fc8ff").B), image.Rgba[shielded + 2]);
        Assert.Equal(0, image.Rgba[(world.Regions.Cells.Count - 1) * 4 + 3]);
        Assert.Equal(OverlayKind.Danger, OverlayMap.Next(OverlayKind.Weather));
        Assert.Null(OverlayMap.Next(OverlayKind.Danger));
    }

    [Fact]
    public void Labels_MarkDroughtFireRuinsAndShields()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var settlement = world.Settlements[0];
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0.05f;
        }

        var (column, row) = world.Regions.GetCoordinates(settlement.RegionId);
        world.Regions.Cells[(row * world.Regions.Columns) + column + 1].Fire = 0.5f;
        settlement.Ruins = 2;
        settlement.ShieldedUntilTick = 100;

        Assert.Equal("Town1\n100 people\ndrought · fire nearby · 2 in ruins · shielded", SettlementLabel.Format(settlement, world, SettlementWorlds.Rules));
        world.Regions.Cells[settlement.RegionId].Fire = 0.5f;
        Assert.Equal(["drought", "burning", "2 in ruins", "shielded"], DisasterText.Marks(settlement, world, SettlementWorlds.Rules));
    }

    [Fact]
    public void Alerts_TellOfQuakesFiresAndDamageAmongYourPeople_ButNotStrangers()
    {
        var world = RivalWorlds.Create(Domain.Gods.GodPersonality.Tyrant, 0f, 300f, 900f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.5f);
        var quake = DisasterModel.StartQuake(DisasterWorlds.Rules, world.Disasters, world.Settlements[0].Position, 6.5f, 0, RivalWorlds.RivalId);
        WorldEvent[] events =
        [
            new EarthquakeStruck { Tick = 1, QuakeId = quake.Id, Epicentre = world.Settlements[0].Position, Magnitude = 6.5f, GodId = RivalWorlds.RivalId },
            new WildfireStarted { Tick = 1, RegionId = world.Settlements[0].RegionId, Cause = FireCause.Lightning },
            new WildfireStarted { Tick = 1, RegionId = world.Settlements[1].RegionId, Cause = FireCause.Hearth },
            new SettlementDamaged { Tick = 1, SettlementId = world.Settlements[0].Id, Cause = DisasterKind.Earthquake, HousesLost = 1, Deaths = 3 },
            new SettlementDamaged { Tick = 1, SettlementId = world.Settlements[1].Id, Cause = DisasterKind.Fire, HousesLost = 4, Deaths = 1 },
        ];

        var raised = new AlertFeed().Push(events, world, DivineWorlds.GodId, 10.0);

        Assert.Equal(
            ["Rivalis shakes the earth under Town1 (magnitude 6.5)!", "Fire near Town1, lit by lightning.", "The earthquake struck Town1: 1 house destroyed, 3 dead."],
            raised.Select(a => a.Text));
    }

    [Fact]
    public void Hud_NamesFiresAndShakingAmongYourPeople()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        world.Weather.SpellEndsTick = world.Tick + 600;
        world.Weather.Spell = WeatherSpell.Normal;
        var home = world.Settlements.First(s => s.Beliefs.Any(b => b.GodId == session.LocalGodId && b.Adherence > 0f));
        world.Regions.Cells[home.RegionId].Fire = 0.6f;
        DisasterModel.StartQuake(session.Content.Disasters, world.Disasters, home.Position, 6f, world.Tick, null);

        var model = HudReadout.Build(session, null, null);

        Assert.True(model.WeatherWarning);
        Assert.Contains("Fire near " + home.Name, model.Weather, StringComparison.Ordinal);
        Assert.Contains("The earth shakes at " + home.Name, model.Weather, StringComparison.Ordinal);
    }

    [Fact]
    public void DebugPanel_ListsQuakesFiresAndASettlementsDangers()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DisasterModel.StartQuake(DisasterWorlds.Rules, world.Disasters, new GroundPosition(300f, 512f), 6f, 0, 7);
        world.Regions.Cells[world.Settlements[0].RegionId].Fire = 0.75f;
        world.Settlements[0].Ruins = 3;
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = SettlementWorlds.Rules,
            DivineRules = DivineWorlds.Rules,
            DisasterRules = DisasterWorlds.Rules,
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

        Assert.Contains("Disasters: 1 earthquakes, 1 regions burning, 0 scorched", lines);
        Assert.Contains(lines, l => l.StartsWith("  quake #1 M6.0 at (300, 512)", StringComparison.Ordinal) && l.EndsWith("caused by god 7", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"  fire in region #{world.Settlements[0].RegionId}: 0.75"), StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  dangers: fire threat 0.75, shaking 0.50; 3 in ruins", StringComparison.Ordinal));
        Assert.Equal(RegionDebugLayer.Fire, RegionDebugMap.Next(RegionDebugLayer.SoilMoisture));
        Assert.Equal(RegionDebugLayer.Scorch, RegionDebugMap.Next(RegionDebugLayer.Fire));
        Assert.Null(RegionDebugMap.Next(RegionDebugLayer.Scorch));
    }

    [Fact]
    public void DebugCommands_StartAQuakeOrAFireAtAPoint()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var target = session.World.Settlements[0].Position;

        var quake = DisasterCommands.StartQuake(session, target);
        var fire = DisasterCommands.StartFire(session, target);

        var shaking = Assert.Single(session.World.Disasters.Earthquakes);
        Assert.Equal((DisasterCommands.DebugMagnitude, (float?)null), (shaking.Magnitude, (float?)shaking.CausedByGodId));
        Assert.StartsWith("An earthquake of magnitude 6.5", quake, StringComparison.Ordinal);
        Assert.Equal(DisasterCommands.DebugFire, session.World.Regions.Cells[session.World.Settlements[0].RegionId].Fire);
        Assert.StartsWith("Region #", fire, StringComparison.Ordinal);
        Assert.Equal("Point at the land to start a fire", DisasterCommands.StartFire(session, new GroundPosition(-50f, -50f)));
    }

    [Fact]
    public void LaunchOption_StartsADisaster()
    {
        Assert.Equal(DisasterAtStart.Quake, LaunchOptions.Parse(["--disaster=quake"]).Disaster);
        Assert.Equal(DisasterAtStart.Fire, LaunchOptions.Parse(["--disaster=FIRE"]).Disaster);
        Assert.Equal(OverlayKind.Danger, LaunchOptions.Parse(["--overlay=danger"]).Overlay);
        Assert.Single(LaunchOptions.Parse(["--disaster=flood"]).Warnings);
    }

    [Fact]
    public void ShippedDisasterContent_Parses_AndBrokenRulesAreRejected()
    {
        Assert.Equal(10, TestContent.Shipped.Disasters.StepIntervalTicks);
        Assert.Equal(["fire", "shield", "earthquake"], TestContent.Shipped.Miracles.Miracles.Skip(6).Select(m => m.Id));

        var text = TestContent.ReadText(ContentJson.DisastersFileName).Replace("\"FullMagnitude\": 7.5", "\"FullMagnitude\": 4.0", StringComparison.Ordinal);
        var files = GameContentFiles.Read(name => name == ContentJson.DisastersFileName ? text : TestContent.ReadText(name));
        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));
        Assert.Contains("disasters.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("HarmlessMagnitude", exception.Message, StringComparison.Ordinal);
    }
}
