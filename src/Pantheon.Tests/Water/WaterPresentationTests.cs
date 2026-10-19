using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Disasters;
using Pantheon.Game.History;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.History;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Water;

/// <summary>How rivers, floods and thirst are shown and driven (ADR-022): the water's meshes, the readouts, alerts, history and the debug flood.</summary>
public sealed class WaterPresentationTests
{
    private static readonly Lazy<GeneratedWorld> Rivers = new(() => WorldGenerator.Generate(new WorldSeed(42), TestContent.RiverWorldGen));

    [Fact]
    public void TheFreshWaterMesh_CoversTheRiversAndLakes_AndNothingElse()
    {
        var dry = WorldGenerator.Generate(new WorldSeed(42), TestContent.SmallWorldGen);
        var mesh = WaterSurfaceMesh.BuildFreshWater(Rivers.Value.Terrain, 1);

        Assert.True(WaterSurfaceMesh.BuildFreshWater(dry.Terrain, 1).IsEmpty);
        Assert.False(mesh.IsEmpty);
        Assert.Equal(mesh.Positions.Length / 4 * 6, mesh.Indices.Length);
        Assert.All(mesh.Depths, depth => Assert.InRange(depth, 0f, 1f));
        Assert.All(mesh.Positions, position => Assert.True(position.Y >= -0.1f));
    }

    [Fact]
    public void Floodwater_RisesFromTheRiverOverTheLandBesideIt_OnlyWhereItFloods()
    {
        var world = Rivers.Value;
        var surface = FloodSurface.Build(world.Terrain, 2, 60f);
        var floods = new float[world.State.Regions.Cells.Count];
        var none = surface.Mesh(world.State.Regions, floods, 3f);
        var river = world.State.Water.Rivers[0];
        var mouth = river.Regions[^2];
        floods[mouth] = 1f;

        var flooded = surface.Mesh(world.State.Regions, floods, 3f);

        Assert.True(none.IsEmpty);
        Assert.False(flooded.IsEmpty);
        var centre = world.State.Regions.GetRegionCenter(mouth);
        Assert.All(flooded.Positions, p => Assert.True(MathF.Abs(p.X - centre.X) < 140f && MathF.Abs(p.Z - centre.Z) < 140f));
        Assert.Contains(flooded.Positions.Select((p, i) => (p, i)), v => flooded.Depths[v.i] > 0.5f);
    }

    [Fact]
    public void RiverBeds_DarkenWithDepth()
    {
        var terrain = Rivers.Value.Terrain;
        var palette = TestContent.Palette;
        var deep = Enumerable.Range(0, terrain.Heights.Length).First(i => !float.IsNaN(terrain.Hydrology.WaterSurface[i]) && terrain.Hydrology.WaterSurface[i] - terrain.Heights[i] > 2.5f);
        var (x, z) = (deep % terrain.Resolution, deep / terrain.Resolution);

        var color = TerrainColorizer.ComputeColor(terrain, palette, x, z);

        Assert.True(Math.Abs(color.R - palette.SeaFloor.R) < 0.1f && Math.Abs(color.G - palette.SeaFloor.G) < 0.1f);
    }

    [Fact]
    public void TheDebugPanel_ListsTheRiversLevels_AndASettlementsWater()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var river = WaterWorlds.LayRiver(world);
        river.Level = 1.3f;
        world.Regions.Cells[world.Settlements[0].RegionId].Flood = 0.4f;
        var lines = new List<string>();
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = SettlementWorlds.Rules,
            DivineRules = DivineWorlds.Rules,
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "test",
            TerrainIsFallback = false,
            ZoomBand = global::Pantheon.Game.Camera.CameraZoomBand.Strategic,
            CameraDistance = 100f,
            RegionBoundariesVisible = false,
            WaterRules = WaterWorlds.Rules,
            SelectedSettlementId = world.Settlements[0].Id,
        };

        WaterReadout.Append(lines, snapshot);
        lines.AddRange(DebugReadout.Build(snapshot).Lines);

        Assert.Equal("Rivers (1): Silverrun 1.30 FLOOD 50%", lines[0]);
        Assert.StartsWith("  lakes: none   flooded regions 1   watered regions 13", lines[1], StringComparison.Ordinal);
        Assert.Contains(lines, line => line.Contains("on the Silverrun (level 1.30)   FLOODED 40%", StringComparison.Ordinal));
    }

    [Fact]
    public void Alerts_TellOfFloodsAndDryWellsAmongYourPeople_ButNotYourOwnWave()
    {
        var world = DivineWorlds.Create(0f, 300f, 900f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", Domain.Gods.GodController.Ai, Domain.Gods.GodPersonality.Tyrant, 0f));
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.5f);
        var river = WaterWorlds.LayRiver(world);
        world.Regions.Cells[world.Settlements[0].RegionId].Flood = 0.6f;
        WorldEvent[] events =
        [
            new FloodStarted { Tick = 1, RiverId = river.Id, GodId = null, RegionId = river.Regions[^1] },
            new FloodStarted { Tick = 1, RiverId = null, GodId = 2, RegionId = world.Settlements[0].RegionId },
            new FloodStarted { Tick = 1, RiverId = null, GodId = DivineWorlds.GodId, RegionId = world.Settlements[0].RegionId },
            new ThirstStarted { Tick = 1, SettlementId = world.Settlements[0].Id },
            new ThirstStarted { Tick = 1, SettlementId = world.Settlements[1].Id },
        ];

        var raised = new AlertFeed().Push(events, world, DivineWorlds.GodId, 10.0);

        Assert.Equal(
            ["The Silverrun has burst its banks and floods Town1.", "Rivalis sends the sea over Town1.", "The wells of Town1 have run dry."],
            raised.Select(a => a.Text));
        Assert.Contains("flooded", DisasterText.Marks(world.Settlements[0], world, SettlementWorlds.Rules));
    }

    [Fact]
    public void AGreatFlood_EntersTheHistory()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var rules = TestContent.Shipped.History;

        new HistoryRecorder(rules).Record(world, [new SettlementDamaged { Tick = 0, SettlementId = 1, Cause = DisasterKind.Flood, HousesLost = rules.HousesLost, Deaths = 1 }, new ThirstStarted { Tick = 0, SettlementId = 1 }], 0);

        Assert.Equal([HistoryKind.Flood, HistoryKind.Thirst], world.History.Select(r => r.Kind));
        Assert.Equal("The Flood of Town1", HistoryText.Title(world.History[0], world));
        Assert.Equal("The Thirst of Town1", HistoryText.Title(world.History[1], world));
    }

    [Fact]
    public void TheDebugFlood_BurstsTheNearestRiver_OrSendsAWaveWhereThereIsNone()
    {
        var session = GameSession.CreateNew(TestContent.Small with { WorldGen = TestContent.RiverWorldGen }, new WorldSeed(42));
        var river = session.World.Water.Rivers[0];
        var near = session.World.Regions.GetRegionCenter(river.Regions[river.Regions.Count / 2]);

        var burst = DisasterCommands.StartFlood(session, near);
        session.World.Water.Rivers.Clear();
        var wave = DisasterCommands.StartFlood(session, near);

        Assert.Equal($"The {river.Name} bursts its banks; it floods the low land downstream", burst);
        Assert.Equal(session.Content.Water.MaxFloodLevel, river.Level);
        Assert.True(wave.StartsWith("A wave floods", StringComparison.Ordinal) || wave.StartsWith("No river nearby", StringComparison.Ordinal), wave);
    }
}
