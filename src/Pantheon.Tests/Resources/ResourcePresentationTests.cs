using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Events;
using Pantheon.Domain.Roads;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Book;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Launch;
using Pantheon.Game.Multiplayer;
using Pantheon.Game.Presentation;
using Pantheon.Game.Resources;
using Pantheon.Game.Society;
using Pantheon.Networking.Protocol;
using Pantheon.Persistence;
using Pantheon.Simulation.Roads;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Resources;

/// <summary>How wood, stone, forests and roads are told, shown, driven and shared (ADR-033).</summary>
public sealed class ResourcePresentationTests
{
    [Fact]
    public void Alerts_TellOfRoadsAmongYourVillages_AndNoOthers()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var (mine, theirs, far) = (world.Settlements[0], world.Settlements[1], world.Settlements[2]);
        WorldEvent[] events =
        [
            new RoadBuilt { Tick = 1, RoadId = 1, FromSettlementId = mine.Id, ToSettlementId = theirs.Id },
            new RoadChanged { Tick = 1, RoadId = 1, FromSettlementId = mine.Id, ToSettlementId = theirs.Id, Open = false },
            new RoadChanged { Tick = 1, RoadId = 1, FromSettlementId = mine.Id, ToSettlementId = theirs.Id, Open = true },
            new RoadBuilt { Tick = 1, RoadId = 2, FromSettlementId = theirs.Id, ToSettlementId = far.Id },
        ];

        var alerts = events.Select(e => new AlertFeed().Push([e], world, 1, 1.0).SingleOrDefault()).ToList();

        Assert.Equal(($"A road now joins {mine.Name} and {theirs.Name}: walkers go faster, and trade and food travel it.", AlertSeverity.Good), (alerts[0]!.Text, alerts[0]!.Severity));
        Assert.Equal(($"The road between {mine.Name} and {theirs.Name} is cut: until it is mended no trade or food passes.", AlertSeverity.Warning), (alerts[1]!.Text, alerts[1]!.Severity));
        Assert.Equal($"The road between {mine.Name} and {theirs.Name} is mended and open again.", alerts[2]!.Text);
        Assert.Null(alerts[3]);
    }

    [Fact]
    public void ThePanel_TheBook_TheDebugPanel_AndTheOptions_ShowAndDriveThem()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        var home = world.Settlements[0];

        var panel = SettlementPanelReadout.Build(session, home.Id)!.Lines;
        Assert.Contains(panel, l => l.StartsWith("Wood 30 of 30   stone 15 of 20", StringComparison.Ordinal));
        Assert.Contains("Roads: none yet", panel);
        Assert.Contains(WorldPage.Build(session).Lines, l => l.Text == "Forests: 100% standing. No road joins the villages.");

        var status = ResourceCommands.BuildRoads(session);

        Assert.Matches(@"^\d+ roads built at once$", status);
        Assert.NotEmpty(world.Roads);
        Assert.All(world.Roads, r => Assert.True(RoadModel.IsOpen(session.Content.SettlementRules.Roads, r)));
        Assert.Matches(@"^Roads: to [^,(]+", SettlementPanelReadout.Build(session, world.Roads[0].FromSettlementId)!.Lines.Single(l => l.StartsWith("Roads:", StringComparison.Ordinal)));
        Assert.Contains(WorldPage.Build(session).Lines, l => l.Text.StartsWith(Format("Forests: 100% standing. {0} roads join the villages", world.Roads.Count), StringComparison.Ordinal));

        Assert.Equal("Every village's wood and stone are gone", ResourceCommands.SetStores(session, full: false));
        Assert.All(world.Settlements, s => Assert.Equal((0f, 0f), (s.Wood, s.Stone)));
        var lines = DebugReadout.Build(Snapshot(world, home.Id, session.Content.SettlementRules)).Lines;
        Assert.Contains(lines, l => l.StartsWith(Format("Resources: 100% of the forest standing, {0} roads built, 0 being built, 0 cut", world.Roads.Count), StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  resources: wood 0 of 30   stone 0 of 20   ", StringComparison.Ordinal) && l.Contains("gathering; land around: timber ", StringComparison.Ordinal));

        Assert.True(LaunchOptions.Parse(["--roads=on"]).Roads);
        Assert.Equal(true, LaunchOptions.Parse(["--stores=full"]).Stores);
        Assert.Equal(false, LaunchOptions.Parse(["--stores=empty"]).Stores);
        Assert.Single(LaunchOptions.Parse(["--stores=heaped"]).Warnings);
    }

    [Fact]
    public void Trees_StandAsTheTimberDoes_NeverInTheSea_AVillage_OrOnARoad()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        var region = world.Regions.Cells.Where(r => r.Biome == Pantheon.Domain.Ecology.Biome.Forest).OrderByDescending(r => r.Timber).First();
        var whole = Trees(world, session, region, []);

        Assert.Equal(ForestLayout.TreeCount(world.Regions, region), whole.Count + Skipped(world, session, region));
        region.Timber *= 0.5f;
        var thinned = Trees(world, session, region, []);
        Assert.True(thinned.Count < whole.Count);
        Assert.All(thinned, t => Assert.Contains(t, whole));

        var centre = world.Regions.GetRegionCenter(region.Id);
        var clearing = new Clearing(new Vector2(centre.X, centre.Z), world.Regions.RegionSize);
        Assert.Empty(Trees(world, session, region, [clearing]));
        Assert.All(world.Regions.Cells.Where(r => r.Biome == Pantheon.Domain.Ecology.Biome.Sea), r => Assert.Equal(0, ForestLayout.TreeCount(world.Regions, r)));

        var (a, b) = (new GroundPosition(centre.X - 100f, centre.Z), new GroundPosition(centre.X + 100f, centre.Z));
        world.Roads.Add(new RoadState { Id = 9, FromSettlementId = 1, ToSettlementId = 2, Path = [a, b], Length = 200f });
        Assert.All(Trees(world, session, region, []), t => Assert.True(MathF.Abs(t.Position.Z - centre.Z) >= ForestLayout.RoadClearance));
    }

    [Fact]
    public void ARoadIsDrawn_AlongWhatIsBuilt()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var (a, b) = (session.World.Settlements[0].Position, session.World.Settlements[1].Position);
        var length = MathF.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Z - a.Z) * (b.Z - a.Z)));
        var road = new RoadState { Id = 1, FromSettlementId = 1, ToSettlementId = 2, Path = [a, b], Length = length, Built = 0f };

        Assert.Empty(RoadLayout.Ribbon(road, session.Terrain));

        road.Built = length / 2f;
        var half = RoadLayout.Ribbon(road, session.Terrain);
        var end = (half[^1].Left + half[^1].Right) / 2f;
        Assert.Equal(length / 2f, Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(end.X, end.Z)), 1);
        Assert.All(half, e => Assert.Equal(RoadLayout.Width, Vector2.Distance(new Vector2(e.Left.X, e.Left.Z), new Vector2(e.Right.X, e.Right.Z)), 2));
        Assert.InRange(half.Count, (int)(length / 2f / RoadLayout.Step), (int)(length / 2f / RoadLayout.Step) + 3);
    }

    [Fact]
    public void TheRegionMap_TheSave_AndTheSections_CarryTheResources()
    {
        Assert.Equal(RegionDebugLayer.Stone, RegionDebugMap.Next(RegionDebugLayer.Timber));
        Assert.Null(RegionDebugMap.Next(RegionDebugLayer.Stone));

        var host = GameSession.CreateNew(TestContent.Small, new WorldSeed(77)).World;
        var replica = GameSession.CreateNew(TestContent.Small, new WorldSeed(77)).World;
        var region = host.Regions.Cells.First(r => r.Forest > 0.3f);
        (region.Timber, region.Stone) = (0.1f, 0.05f);
        Assert.Equal(0.1f, RegionDebugMap.GetValue(region, RegionDebugLayer.Timber));
        RegionChange.Of(region).ApplyTo(replica.Regions.Cells[region.Id]);
        Assert.Equal((0.1f, 0.05f), (replica.Regions.Cells[region.Id].Timber, replica.Regions.Cells[region.Id].Stone));

        var (a, b) = (host.Settlements[0], host.Settlements[1]);
        host.Roads.Add(new RoadState { Id = host.NextRoadId++, FromSettlementId = a.Id, ToSettlementId = b.Id, Path = [a.Position, b.Position], Length = 300f, Built = 120f, Wear = 0.1f });
        WorldSections.Apply(replica, WorldSection.Roads, WorldSections.Write(host, WorldSection.Roads));
        Assert.Equal(WorldSaveSerializer.Serialize(host), WorldSaveSerializer.Serialize(replica));
    }

    /// <summary>A region's trees.</summary>
    private static List<TreePlacement> Trees(WorldState world, GameSession session, Pantheon.Domain.World.RegionState region, List<Clearing> clearings)
    {
        var trees = new List<TreePlacement>();
        ForestLayout.Place(world, session.Terrain, region, clearings, trees);
        return trees;
    }

    /// <summary>A region's tree slots left empty by water.</summary>
    private static int Skipped(WorldState world, GameSession session, Pantheon.Domain.World.RegionState region)
    {
        var (column, row) = world.Regions.GetCoordinates(region.Id);
        var size = world.Regions.RegionSize;
        var skipped = 0;
        for (var slot = 0; slot < ForestLayout.TreeCount(world.Regions, region); slot++)
        {
            var x = (column + PresentationHash.Unit(world.Seed.Value, (ulong)region.Id, (ulong)slot, 1)) * size;
            var z = (row + PresentationHash.Unit(world.Seed.Value, (ulong)region.Id, (ulong)slot, 2)) * size;
            skipped += session.Terrain.SampleHeight(x, z) < 0.5f || session.Terrain.IsUnderFreshWater(x, z) ? 1 : 0;
        }

        return skipped;
    }

    /// <summary>A debug snapshot of a world with one settlement selected.</summary>
    private static DebugSnapshot Snapshot(WorldState world, int selected, Pantheon.Domain.Settlements.SettlementRules rules) => new()
    {
        FramesPerSecond = 60,
        World = world,
        SettlementRules = rules,
        DivineRules = DivineWorlds.Rules,
        IsPaused = false,
        TimeScale = 1,
        TicksPerSecond = 10,
        TerrainBackend = "test",
        TerrainIsFallback = false,
        ZoomBand = CameraZoomBand.Regional,
        CameraDistance = 380f,
        RegionBoundariesVisible = false,
        SelectedSettlementId = selected,
    };

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
}
