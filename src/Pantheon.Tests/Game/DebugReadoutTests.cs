using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.World;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Population;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The debug overlay text shows the required readouts.</summary>
public sealed class DebugReadoutTests
{
    private static readonly WorldState World = WorldGenerator.Generate(new WorldSeed(1234), TestContent.SmallWorldGen).State;

    [Fact]
    public void DebugReadout_ShowsCoreReadouts()
    {
        var text = DebugReadout.Build(Snapshot()).ToText();

        Assert.Contains("FPS 60", text);
        Assert.Contains("Tick 0", text);
        Assert.Contains("Seed 1234", text);
        Assert.Contains("Terrain Terrain3D", text);
        Assert.Contains("Camera Regional", text);
        Assert.Contains("Settlement step every 10 ticks, next in 10", text);
        Assert.Contains($"Settlement {World.Settlements[0].Name}", text);
        Assert.Contains("population 40 / 60   12 houses   health 100%", text);
        Assert.Contains("Region boundaries OFF", text);
        Assert.Contains(DebugReadout.CameraHints, text);
        Assert.Contains(DebugReadout.KeyHints, text);
        Assert.Contains(DebugReadout.DebugHints, text);
        Assert.DoesNotContain("FALLBACK", text);
    }

    [Fact]
    public void DebugReadout_ShowsRegionUnderCursor()
    {
        var settlement = World.Settlements[0];
        var cursor = new Vector3(settlement.Position.X, 12f, settlement.Position.Z);
        var region = World.Regions.GetRegion(settlement.RegionId);

        var lines = DebugReadout.Build(Snapshot() with { CursorWorldPosition = cursor }).Lines;

        Assert.Contains(lines, line => line.StartsWith($"Region #{settlement.RegionId} ", StringComparison.Ordinal) && line.EndsWith(region.Biome.ToString(), StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("fert ", StringComparison.Ordinal));
    }

    [Fact]
    public void DebugReadout_MarksPauseFallbackAndStatus()
    {
        var snapshot = Snapshot() with
        {
            IsPaused = true,
            TerrainBackend = "Fallback mesh",
            TerrainIsFallback = true,
            RegionBoundariesVisible = true,
            StatusMessage = "Saved",
        };

        var lines = DebugReadout.Build(snapshot).Lines;

        Assert.Contains("PAUSED", lines[0]);
        Assert.Contains(lines, line => line.Contains("FALLBACK RENDERER", StringComparison.Ordinal));
        Assert.Contains("Region boundaries ON", lines);
        Assert.Contains("Saved", lines);
        Assert.Equal("Cursor -", lines.Single(line => line.StartsWith("Cursor", StringComparison.Ordinal)));
    }

    [Fact]
    public void DebugReadout_ShowsFoodJobsAndChangesPerSecond()
    {
        // 25 regions of fertility 0.5: harvest = 0.3 * 0.5 * 300 * (1 - e^(-24/300)) = 3.46 per step.
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 120f, fertility: 0.5f);

        var lines = DebugReadout.Build(SettlementSnapshot(world)).Lines;

        Assert.Contains("  food 120.0 / 480   net +1.46/s   harvest 3.46/s   eaten 2.00/s", lines);
        Assert.Contains("  jobs: 24 farming, 0 building, 16 not working   farmland 25 regions, fertility 0.50", lines);
        Assert.Contains("  births 0.16/s   deaths 0.02/s   no house needed: 67% full, builds from 85%", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("  !", StringComparison.Ordinal));
    }

    [Fact]
    public void DebugReadout_RatesFollowTheTickRate()
    {
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 120f, fertility: 0.5f);

        var lines = DebugReadout.Build(SettlementSnapshot(world) with { TicksPerSecond = 20.0 }).Lines;

        Assert.Contains("  food 120.0 / 480   net +2.92/s   harvest 6.92/s   eaten 4.00/s", lines);
    }

    [Fact]
    public void DebugReadout_WarnsWhenStarvingCrowdedAndShortOfFood()
    {
        // Harvest 0.3 * 0.1 * 300 * (1 - e^(-60/300)) = 1.63 against 5 eaten: a third is fed.
        var world = SettlementWorlds.Create(population: 100, housing: 100, food: 0f, fertility: 0.1f, health: 0.5f);

        var lines = DebugReadout.Build(SettlementSnapshot(world)).Lines;

        Assert.Contains("  population 100 / 100   20 houses   health 50%", lines);
        Assert.Contains(lines, line => line.EndsWith("building house 21: 0%", StringComparison.Ordinal));
        Assert.Contains("  ! starving: 33% fed   ! no births: houses full   ! house 21 waits for food", lines);
    }

    [Fact]
    public void DebugReadout_ShowsTheHouseUnderConstruction()
    {
        // Builders: min(34.8 workers * 0.2, 10 per site, 55 work left) = 6.96.
        var world = SettlementWorlds.Create(population: 58, housing: 60, food: 400f, fertility: 0.5f, constructionProgress: 45f);

        var lines = DebugReadout.Build(SettlementSnapshot(world)).Lines;

        Assert.Contains(lines, line => line.StartsWith("  jobs: 28 farming, 7 building, 23 not working", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.EndsWith("building house 13: 45%", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith("  !", StringComparison.Ordinal));
    }

    [Fact]
    public void DebugReadout_ReportsAnEmptySettlement()
    {
        var world = SettlementWorlds.Create(population: 0, housing: 60, food: 50f, fertility: 0.5f);

        var lines = DebugReadout.Build(SettlementSnapshot(world)).Lines;

        Assert.Contains("  ! no births: nobody left", lines);
    }

    [Fact]
    public void DebugReadout_ReadsAsTier1WithoutACrowdStatus()
    {
        var lines = DebugReadout.Build(Snapshot()).Lines;

        Assert.Contains("  sim tier 1 statistical", lines);
    }

    [Fact]
    public void DebugReadout_ShowsTheTierAndTheDistanceThatChangesIt()
    {
        var id = World.Settlements[0].Id;
        var statistical = new CrowdStatus(id, SimulationTier.Statistical, 0, 0f, 812.4f, 450f, 550f);
        var regional = new CrowdStatus(id, SimulationTier.Regional, 40, 1f, 212.6f, 450f, 550f);

        var far = DebugReadout.Build(Snapshot() with { Crowds = [statistical] }).Lines;
        var near = DebugReadout.Build(Snapshot() with { Crowds = [regional] }).Lines;

        Assert.Contains("  sim tier 1 statistical: camera 812 m, citizens shown within 450 m", far);
        Assert.Contains("  sim tier 2 regional: 40 citizens shown, 1 per 1.0 people, camera 213 m, statistical beyond 550 m", near);
    }

    [Fact]
    public void DebugReadout_ShowsHowManyPeopleEachRepresentativeStandsFor()
    {
        var world = SettlementWorlds.Create(population: 540, housing: 645, food: 2000f, fertility: 0.5f);
        var crowd = new CrowdStatus(SettlementWorlds.Settlement(world).Id, SimulationTier.Regional, 300, 1.8f, 90f, 450f, 550f);

        var lines = DebugReadout.Build(SettlementSnapshot(world) with { Crowds = [crowd] }).Lines;

        Assert.Contains("  sim tier 2 regional: 300 citizens shown, 1 per 1.8 people, camera 90 m, statistical beyond 550 m", lines);
    }

    [Fact]
    public void Summarize_DescribesEachSettlement()
    {
        var summary = DebugReadout.Summarize(World, TestContent.Shipped.SettlementRules, []);

        Assert.Equal($"seed 1234, tick 0, 256 regions; {World.Settlements[0].Name}: population 40 / 60, 12 houses, health 100%, food 120, tier 1 statistical, 0 citizens shown", summary);
    }

    [Fact]
    public void Summarize_IncludesTheClientsTier()
    {
        var crowd = new CrowdStatus(World.Settlements[0].Id, SimulationTier.Regional, 40, 1f, 200f, 450f, 550f);

        var summary = DebugReadout.Summarize(World, TestContent.Shipped.SettlementRules, [crowd]);

        Assert.EndsWith("food 120, tier 2 regional, 40 citizens shown", summary, StringComparison.Ordinal);
    }

    /// <summary>A typical frame snapshot of the generated world, without a cursor position.</summary>
    private static DebugSnapshot Snapshot() => new()
    {
        FramesPerSecond = 60.2,
        World = World,
        SettlementRules = TestContent.Shipped.SettlementRules,
        IsPaused = false,
        TimeScale = 1.0,
        TicksPerSecond = 10.0,
        TerrainBackend = "Terrain3D",
        TerrainIsFallback = false,
        ZoomBand = CameraZoomBand.Regional,
        CameraDistance = 380f,
        RegionBoundariesVisible = false,
    };

    /// <summary>A snapshot of a hand-built settlement world with the pinned test rules.</summary>
    private static DebugSnapshot SettlementSnapshot(WorldState world) => Snapshot() with
    {
        World = world,
        SettlementRules = SettlementWorlds.Rules,
    };
}
