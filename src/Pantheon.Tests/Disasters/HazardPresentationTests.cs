using System;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Disasters;
using Pantheon.Game.History;
using Pantheon.Game.Launch;
using Pantheon.Game.Multiplayer;
using Pantheon.Game.Presentation;
using Pantheon.Networking.Protocol;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.History;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>How the hazards of ADR-029 are told, drawn, driven and shared.</summary>
public sealed class HazardPresentationTests
{
    [Fact]
    public void TheWords_NameTheHazards_AndTheVolcanoAfterTheVillageNearest()
    {
        var world = PantheonWorlds.Create(0.8f);

        Assert.Equal(
            ["the wave", "the landslide", "the eruption", "the tornado", "the falling star"],
            new[] { DisasterKind.Tsunami, DisasterKind.Landslide, DisasterKind.Eruption, DisasterKind.Tornado, DisasterKind.Meteor }.Select(DisasterText.Kind));
        Assert.Equal("Mount " + world.Settlements[3].Name, DisasterText.VolcanoName(world, world.Settlements[3].RegionId));
    }

    [Fact]
    public void Alerts_TellOfEveryHazard_ThatConcernsThePlayer()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var mine = world.Settlements[0];
        var theirs = world.Settlements[1];
        WorldEvent[] events =
        [
            new TsunamiStruck { Tick = 1, Shore = mine.Position, RegionId = mine.RegionId },
            new LandslideStruck { Tick = 1, RegionId = 0, ToRegionId = mine.RegionId },
            new LandslideStruck { Tick = 1, RegionId = 0, ToRegionId = theirs.RegionId },
            new VolcanoChanged { Tick = 1, RegionId = mine.RegionId, Phase = VolcanoPhase.Rumbling },
            new VolcanoChanged { Tick = 1, RegionId = mine.RegionId, Phase = VolcanoPhase.Erupting },
            new VolcanoChanged { Tick = 1, RegionId = mine.RegionId, Phase = VolcanoPhase.Dormant },
            new TornadoTouchedDown { Tick = 1, TornadoId = 4, Position = mine.Position },
            new MeteorStruck { Tick = 1, Position = theirs.Position },
            new BlightChanged { Tick = 1, SettlementId = mine.Id, Started = true, Blight = 0.3f },
            new BlightChanged { Tick = 1, SettlementId = mine.Id, Started = false, Blight = 0.1f },
            new SwarmArrived { Tick = 1, SettlementId = mine.Id, SwarmId = 2 },
            new SwarmArrived { Tick = 1, SettlementId = theirs.Id, SwarmId = 2 },
        ];

        var texts = events.Select(e => new AlertFeed().Push([e], world, 1, 1.0).SingleOrDefault()).Select(a => a?.Text).ToList();

        Assert.Equal($"A great wave comes ashore by {mine.Name}: the low land floods.", texts[0]);
        Assert.StartsWith($"A landslide buries the land by {mine.Name}", texts[1], StringComparison.Ordinal);
        Assert.Null(texts[2]);
        Assert.Equal($"Mount {mine.Name} rumbles: the volcano is waking.", texts[3]);
        Assert.StartsWith($"Mount {mine.Name} erupts", texts[4], StringComparison.Ordinal);
        Assert.Equal($"Mount {mine.Name} falls quiet.", texts[5]);
        Assert.Equal($"A tornado touches down near {mine.Name}.", texts[6]);
        Assert.Equal($"A falling star strikes near {theirs.Name}!", texts[7]);
        Assert.StartsWith($"Blight in the fields of {mine.Name}", texts[8], StringComparison.Ordinal);
        Assert.Equal($"The blight has left the fields of {mine.Name}.", texts[9]);
        Assert.StartsWith($"Locusts descend on {mine.Name}", texts[10], StringComparison.Ordinal);
        Assert.Null(texts[11]);
    }

    [Fact]
    public void TheHistory_RemembersTheGreatHazards()
    {
        var world = PantheonWorlds.Create(0.8f);
        var village = world.Settlements[0];
        WorldEvent[] events =
        [
            new SettlementDamaged { Tick = 1, SettlementId = village.Id, Cause = DisasterKind.Tsunami, HousesLost = 9, Deaths = 2 },
            new SettlementDamaged { Tick = 1, SettlementId = village.Id, Cause = DisasterKind.Landslide, HousesLost = 2, Deaths = 1 },
            new SettlementDamaged { Tick = 1, SettlementId = village.Id, Cause = DisasterKind.Tornado, HousesLost = 6, Deaths = 0 },
            new SettlementDamaged { Tick = 1, SettlementId = village.Id, Cause = DisasterKind.Tornado, HousesLost = 1, Deaths = 0 },
            new VolcanoChanged { Tick = 1, RegionId = village.RegionId, Phase = VolcanoPhase.Erupting },
            new VolcanoChanged { Tick = 1, RegionId = village.RegionId, Phase = VolcanoPhase.Rumbling },
            new MeteorStruck { Tick = 1, Position = village.Position },
            new BlightChanged { Tick = 1, SettlementId = village.Id, Started = true, Blight = 0.3f },
            new BlightChanged { Tick = 1, SettlementId = village.Id, Started = false, Blight = 0.1f },
            new SwarmArrived { Tick = 1, SettlementId = village.Id, SwarmId = 1 },
        ];

        new HistoryRecorder(TestContent.Shipped.History).Record(world, events, 0);

        Assert.Equal(
            [
                $"The Great Wave of {village.Name}", $"The Landslide at {village.Name}", $"The Whirlwind of {village.Name}", $"The Eruption of Mount {village.Name}",
                $"The Falling Star of {village.Name}", $"The Blight of {village.Name}", $"The Locusts of {village.Name}",
            ],
            world.History.Select(r => HistoryText.Title(r, world)));
    }

    [Fact]
    public void TheDebugCommands_StartEachHazard_InACycle()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        var village = world.Settlements[0];
        var kinds = Enum.GetValues<HazardKind>();

        Assert.Equal(kinds, kinds.Select(k => HazardCommands.Next(k)).Prepend(HazardCommands.Next(null)).Take(kinds.Length));
        Assert.Equal(HazardKind.Tornado, HazardCommands.Next(HazardKind.Eruption));

        Assert.StartsWith("A tornado touches down", HazardCommands.Start(session, HazardKind.Tornado, village.Position), StringComparison.Ordinal);
        Assert.StartsWith("Locusts rise", HazardCommands.Start(session, HazardKind.Locusts, village.Position), StringComparison.Ordinal);
        Assert.StartsWith("Blight takes", HazardCommands.Start(session, HazardKind.Blight, village.Position), StringComparison.Ordinal);
        Assert.StartsWith("The slope at region", HazardCommands.Start(session, HazardKind.Landslide, village.Position), StringComparison.Ordinal);
        Assert.StartsWith("A falling star strikes", HazardCommands.Start(session, HazardKind.Meteor, village.Position), StringComparison.Ordinal);
        HazardCommands.Start(session, HazardKind.Tsunami, village.Position);
        HazardCommands.Start(session, HazardKind.Eruption, village.Position);
        Assert.Equal("Point at the land to start a hazard", HazardCommands.Start(session, HazardKind.Tornado, new GroundPosition(-50f, -50f)));

        Assert.Single(world.Disasters.Tornadoes);
        Assert.Single(world.Disasters.Swarms);
        Assert.Contains(world.Regions.Cells, r => r.Blight > 0f);
        Assert.Contains(world.Regions.Cells, r => r.Rubble > 0f);
        Assert.Contains(world.Disasters.Marks, m => m.Kind == TerrainMarkKind.Crater);
    }

    [Fact]
    public void LaunchOptions_StartAnyHazard()
    {
        Assert.Equal(
            [DisasterAtStart.Tornado, DisasterAtStart.Landslide, DisasterAtStart.Meteor, DisasterAtStart.Blight, DisasterAtStart.Locusts, DisasterAtStart.Tsunami, DisasterAtStart.Eruption],
            new[] { "tornado", "landslide", "meteor", "blight", "locusts", "tsunami", "Eruption" }.Select(v => LaunchOptions.Parse([$"--disaster={v}"]).Disaster!.Value));
        Assert.Contains("tornado", Assert.Single(LaunchOptions.Parse(["--disaster=volcano"]).Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void TheDebugPanel_ShowsTheHazards_AndEachVillagesBlightAndRubble()
    {
        var world = DivineWorlds.Create(0f, 300f);
        HazardWorlds.Mountain(world, 100f);
        HazardModel.Wake(HazardWorlds.Hazards.Volcano, world);
        HazardModel.SpawnTornado(HazardWorlds.Hazards.Tornado, world, new GroundPosition(100f, 100f), 0f, 30f);
        HazardModel.SpawnSwarm(world, new GroundPosition(200f, 200f), 60f);
        world.Regions.Cells[world.Settlements[0].RegionId].Rubble = 0.6f;
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = SettlementWorlds.Rules,
            DivineRules = DivineWorlds.Rules,
            DisasterRules = HazardWorlds.Rules,
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "test",
            TerrainIsFallback = false,
            ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 380f,
            RegionBoundariesVisible = false,
        };

        var lines = DebugReadout.Build(snapshot).Lines;

        Assert.Contains(lines, l => l.StartsWith("Hazards: Mount Town1 rumbles (0:40 left); 1 tornadoes, 1 swarms, 0 blighted, 1 under rubble, 0 marks", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  tornado #1 at (100, 100), 0:30 left", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  locusts #2 at (200, 200): size 1.00, 1:00 left", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("blight 0.00, rubble 0.60", StringComparison.Ordinal));
    }

    [Fact]
    public void TheRegionMap_AndTheDangerOverlay_ShowBlightAndRubble()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        var (blighted, buried) = (world.Regions.Cells[0], world.Regions.Cells[1]);
        (blighted.Blight, buried.Rubble) = (0.6f, 0.8f);

        Assert.Equal([0.6f, 0f], new[] { RegionDebugLayer.Blight, RegionDebugLayer.Rubble }.Select(l => RegionDebugMap.GetValue(blighted, l)));
        Assert.Equal(RegionDebugLayer.Rubble, RegionDebugMap.Next(RegionDebugLayer.Blight));
        Assert.Equal(RegionDebugLayer.Timber, RegionDebugMap.Next(RegionDebugLayer.Rubble));

        var image = OverlayMap.Build(world, OverlayKind.Danger, 1, DivineWorlds.Rules, SettlementWorlds.Rules, TestContent.DivineView);

        Assert.Contains("blight in purple", image.Legend, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0x7a, 0x2a, 0x8c }, image.Rgba[..3]);
        Assert.Equal(new byte[] { 0x8a, 0x7f, 0x72 }, image.Rgba[4..7]);
        Assert.Equal(0, image.Rgba[11]);
    }

    [Fact]
    public void TheCover_ShowsRubbleOverBlight_AndMarksFadeWithAge()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        Assert.Null(HazardCover.Build(world));
        (world.Regions.Cells[0].Blight, world.Regions.Cells[0].Rubble, world.Regions.Cells[1].Blight, world.Regions.Cells[2].Blight) = (1f, 1f, 1f, 0.1f);

        var image = HazardCover.Build(world)!;

        Assert.Equal(new byte[] { 0x6e, 0x62, 0x53, 217 }, image.Rgba[..4]);
        Assert.Equal(new byte[] { 0x8a, 0x7a, 0x2e, 217 }, image.Rgba[4..8]);
        Assert.Equal(0, image.Rgba[11]);

        var mark = new TerrainMark { Kind = TerrainMarkKind.Crater, X = 0f, Z = 0f, Radius = 10f, Tick = 100 };
        Assert.Equal(1f, HazardCover.MarkOpacity(mark, 100, HazardWorlds.Hazards));
        Assert.Equal(0.5f, HazardCover.MarkOpacity(mark, 3100, HazardWorlds.Hazards), 4);
        Assert.Equal(0f, HazardCover.MarkOpacity(mark, 9000, HazardWorlds.Hazards));
        Assert.Equal(3, Enum.GetValues<TerrainMarkKind>().Select(HazardCover.MarkColor).Distinct().Count());
    }

    [Fact]
    public void TheDisastersSection_CarriesTheHazards_AndTheRegionsSectionBlightAndRubble()
    {
        var host = DivineWorlds.Create(0f, 300f);
        HazardWorlds.Mountain(host, 100f);
        HazardModel.Wake(HazardWorlds.Hazards.Volcano, host);
        HazardModel.SpawnTornado(HazardWorlds.Hazards.Tornado, host, new GroundPosition(100f, 100f), 0f, 30f);
        HazardModel.SpawnSwarm(host, new GroundPosition(200f, 200f), 60f);
        HazardModel.AddMark(host, TerrainMarkKind.Slide, new GroundPosition(300f, 300f), 40f);
        var replica = DivineWorlds.Create(0f, 300f);

        WorldSections.Apply(replica, WorldSection.Disasters, WorldSections.Write(host, WorldSection.Disasters));

        Assert.Equal((1, 1, 1, 3), (replica.Disasters.Tornadoes.Count, replica.Disasters.Swarms.Count, replica.Disasters.Marks.Count, replica.Disasters.NextHazardId));
        Assert.Equal(VolcanoPhase.Rumbling, replica.Disasters.Volcano!.Phase);

        var region = host.Regions.Cells[3];
        (region.Blight, region.Rubble) = (0.4f, 0.7f);
        RegionChange.Of(region).ApplyTo(replica.Regions.Cells[3]);
        Assert.Equal((0.4f, 0.7f), (replica.Regions.Cells[3].Blight, replica.Regions.Cells[3].Rubble));
    }
}
