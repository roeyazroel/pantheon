using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Launch;
using Pantheon.Game.Performance;
using Pantheon.Game.Presentation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Interface;

/// <summary>The stress scenario and the performance, navigation and authority tools (spec §104, §106, ADR-044).</summary>
public sealed class PerformanceToolTests
{
    [Fact]
    public void TheStressScenario_AddsItsSize_DrawsTheCrowdInThousands_AndLeavesTheShippedSizesAlone()
    {
        var shipped = TestContent.Shipped;

        var content = StressScenario.Content(shipped);
        var presentation = StressScenario.Presentation(new PresentationContent(TestContent.Palette, TestContent.SettlementView, TestContent.DivineView, TestContent.WeatherView));

        var stress = content.Sizes.Sizes[^1];
        Assert.Equal((StressScenario.SizeName, 50, 8), (stress.Name, stress.Settlements, stress.Gods));
        Assert.Equal(shipped.Sizes.Sizes.Count + 1, content.Sizes.Sizes.Count);
        Assert.Equal(1000, presentation.SettlementView.Citizens.MaxRepresentatives);
        Assert.True(presentation.SettlementView.Citizens.RegionalTierExitDistance > presentation.SettlementView.Citizens.RegionalTierEnterDistance);
        Assert.Equal(StressScenario.SizeName, LaunchOptions.Parse(["--stress=on"]).NewWorld(content).Size);
        Assert.Single(LaunchOptions.Parse(["--stress=maybe"]).Warnings);
    }

    [Fact]
    public void TheStressWorld_HoldsFiftyVillagesOfAHundredThousand_EightGodsAndCreatures_TwentyCells_AndThreeDisasters()
    {
        var content = StressScenario.Content(TestContent.Shipped);
        var session = GameSession.CreateNew(content, new WorldSeed(42), new NewWorldOptions(Size: StressScenario.SizeName));

        var said = StressScenario.Apply(session);
        var world = session.World;

        Assert.Equal((50, 100_000, 8, 8, 20), (world.Settlements.Count, world.Settlements.Sum(s => s.Population), world.Gods.Count, world.Creatures.Count, world.Weather.Cells.Count));
        Assert.Single(world.Disasters.Earthquakes);
        Assert.Contains(world.Regions.Cells, r => r.Fire > 0f);
        Assert.Contains(world.Regions.Cells, r => r.Flood > 0f);
        Assert.Equal(StressScenario.Describe(world), said);
        Assert.Contains(world.Settlements, s => s.Position == StressScenario.Busiest(world));
        session.RunTicks(100);
        Assert.Equal(100, world.Tick);
    }

    [Fact]
    public void F1_ShowsWhatTheClientMeasured_TheSave_AndTheWorldsLoad()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var sample = new PerformanceSample
        {
            FramesPerSecond = 118, FrameMilliseconds = 8.46, WorstFrameMilliseconds = 23.5, SimulationMilliseconds = 1.234, WorstSimulationMilliseconds = 4.4,
            TickMilliseconds = 0.31, TicksPerSecond = 160, ManagedMegabytes = 212.4, EngineMegabytes = 640.2, Objects = 23456, Nodes = 812, DrawCalls = 340,
            Primitives = 1_234_000, CitizensShown = 4321, Debris = 37, DebrisMoving = 12, Save = new SaveTiming(10.3, 165, 240),
            Budget = new BudgetStatus(6.0, 14, 3, 0),
        };

        var lines = DebugReadout.Build(Snapshot(session, sample)).Lines;

        Assert.Contains("Performance: 118 FPS, frame 8.5 ms (worst 23.5); simulation 1.23 ms a frame (worst 4.4), 0.31 ms a tick, 160 ticks/s", lines);
        Assert.Contains("  memory 212 MB managed, 640 MB engine; 23456 objects, 812 nodes; 340 draw calls, 1234.0k primitives; 4321 citizens shown", lines);
        Assert.Contains("  physics: 37 debris pieces, 12 still moving", lines);
        Assert.Contains("  simulation budget 6.0 ms a frame: cut short 14 times, 3 ticks waiting, 0 dropped", lines);
        Assert.DoesNotContain(PerformanceReadout.Describe(sample with { Debris = 0 }), l => l.Contains("physics", StringComparison.Ordinal));
        Assert.Contains("  save: 10.3 MB written in 165 ms, read back in 240 ms", lines);
        Assert.Contains(PerformanceReadout.Census(session.World), lines);
        var world = session.World;
        Assert.Equal(
            $"World load: {world.Settlements.Count} settlements, {world.Settlements.Sum(s => s.Population)} people, 4 gods, {world.Creatures.Count} Creatures, {world.Weather.Cells.Count} weather cells, 0 earthquakes, 0 regions burning, 0 flooded, 0 groups on the road, 0 notables, 0 followed villagers",
            PerformanceReadout.Census(world));
        Assert.DoesNotContain(DebugReadout.Build(Snapshot(session, null)).Lines, l => l.StartsWith("Performance:", StringComparison.Ordinal));
    }

    [Fact]
    public void TheNavigationOverlay_ShowsWalkableLand_BlockedLand_Roads_AndTheWaysWalkersAreOn()
    {
        var world = DivineWorlds.Create(0f, 300f, 700f);
        var (rubble, land) = (world.Regions.Cells[3], world.Regions.Cells[4]);
        rubble.Rubble = 1f;
        var path = new List<GroundPosition> { new(700f, 512f) };
        world.Migration.Groups.Add(new MigrationGroup
        {
            Id = 1, OriginSettlementId = 1, DestinationSettlementId = 2, Cause = MigrationCause.Famine, Population = 10, Food = 10f, Health = 1f,
            Position = new GroundPosition(300f, 512f), DepartedTick = 0, Path = path, Beliefs = [new Domain.Belief.GodBelief { GodId = 1, Adherence = 1f, Faith = 0.5f, Love = 0f, Fear = 0f, Doubt = 0f }], TurnedAwayFrom = [],
        });

        var image = Build(world, OverlayKind.Navigation, 1);
        world.Regions.TryGetRegionIdAt(500f, 512f, out var onTheWay);

        Assert.StartsWith("Navigation overlay - Navigation: land walkers may cross in green", image.Legend, StringComparison.Ordinal);
        Assert.True(Pixel(image, rubble.Id)[0] > Pixel(image, rubble.Id)[1]);
        Assert.True(Pixel(image, land.Id)[1] > Pixel(image, land.Id)[0]);
        Assert.Equal(TestContent.DivineView.GodColor(1), Color(image, onTheWay));
        Assert.Equal(255, Pixel(image, onTheWay)[3]);
        Assert.Single(NavigationOverlay.Walkers(world));
    }

    [Fact]
    public void TheAuthorityOverlay_ShowsWhoCommandsEachVillagesGod_AndTheOrderRunsOnToIt()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f, 0.8f);
        world.Gods.Single(g => g.Id == 3).Controller = GodController.Remote;

        var image = Build(world, OverlayKind.Authority, 1);

        Assert.Equal(
            [Authority.You, Authority.Ai, Authority.Player],
            world.Gods.Take(3).Select(g => AuthorityOverlay.Of(g, 1)));
        Assert.Equal(RgbColor.ParseHex("#f2c94c"), Color(image, world.Settlements[0].RegionId));
        Assert.Equal(RgbColor.ParseHex("#8c8c8c"), Color(image, world.Settlements[1].RegionId));
        Assert.Equal(RgbColor.ParseHex("#3d8fe0"), Color(image, world.Settlements[2].RegionId));
        Assert.Equal(OverlayKind.Authority, OverlayMap.Next(OverlayKind.Navigation));
        Assert.Null(OverlayMap.Next(OverlayKind.Authority));
        Assert.Equal(OverlayKind.Authority, LaunchOptions.Parse(["--overlay=authority"]).Overlay);
    }

    [Fact]
    public void AJoinedPlayersPanel_SaysWhoDecides_AndHowFreshEachSectionIs()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        shared.Host.RunTicks(30);
        shared.Pass(3.0, ana);

        var lines = NetworkReadout.Describe(ana, "127.0.0.1:7777", ana.Session!);

        Assert.Equal("  authority: the host decides the world; this replica predicts your acts and takes the host's sections over its own", lines[1]);
        var ages = Assert.Single(lines, l => l.StartsWith("  ages: ", StringComparison.Ordinal));
        Assert.Contains("Gods 0.0 s", ages, StringComparison.Ordinal);
    }

    /// <summary>An overlay of a test world.</summary>
    private static OverlayImage Build(WorldState world, OverlayKind kind, int godId) =>
        OverlayMap.Build(world, kind, godId, DivineWorlds.Rules, SettlementWorlds.Rules, TestContent.DivineView);

    /// <summary>A region's pixel.</summary>
    private static byte[] Pixel(OverlayImage image, int regionId) => image.Rgba[(regionId * 4)..((regionId * 4) + 4)];

    /// <summary>A region's colour.</summary>
    private static RgbColor Color(OverlayImage image, int regionId)
    {
        var pixel = Pixel(image, regionId);
        return new RgbColor(pixel[0] / 255f, pixel[1] / 255f, pixel[2] / 255f);
    }

    /// <summary>Frame data for F1.</summary>
    private static DebugSnapshot Snapshot(GameSession session, PerformanceSample? sample) => new()
    {
        FramesPerSecond = 60,
        World = session.World,
        SettlementRules = session.Content.SettlementRules,
        DivineRules = session.Content.DivineRules,
        Performance = sample,
        IsPaused = false,
        TimeScale = 1,
        TicksPerSecond = 10,
        TerrainBackend = "Terrain3D",
        TerrainIsFallback = false,
        ZoomBand = CameraZoomBand.Regional,
        CameraDistance = 300f,
        RegionBoundariesVisible = false,
    };
}
