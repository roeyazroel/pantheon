using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Disasters;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>The rules of wildfires and earthquakes (spec §19–20, ADR-014) as pure functions.</summary>
public sealed class DisasterModelTests
{
    private static readonly DisasterRules Rules = DisasterWorlds.Rules;

    [Fact]
    public void AStrongerQuake_ShakesHarderAndWider_ForItsTime()
    {
        var disasters = new DisasterState { Earthquakes = [], NextEarthquakeId = 1 };

        var small = DisasterModel.StartQuake(Rules, disasters, new GroundPosition(0f, 0f), 5.5f, tick: 100, godId: null);
        var great = DisasterModel.StartQuake(Rules, disasters, new GroundPosition(0f, 0f), 7.5f, tick: 100, godId: 2);
        var harmless = DisasterModel.StartQuake(Rules, disasters, new GroundPosition(0f, 0f), 4f, tick: 100, godId: null);

        Assert.Equal([1, 2, 3], disasters.Earthquakes.ConvertAll(q => q.Id));
        Assert.Equal(1f / 3f, small.Intensity, 4);
        Assert.Equal(1f, great.Intensity);
        Assert.Equal(0f, harmless.Intensity);
        Assert.Equal(250f, small.Radius, 2);
        Assert.Equal(750f, great.Radius, 2);
        Assert.Equal(100 + 120, great.EndsAtTick);
        Assert.Equal(2, great.CausedByGodId);
    }

    [Fact]
    public void Shaking_FadesWithDistance_ToNothingAtTheRadius()
    {
        var disasters = new DisasterState { Earthquakes = [], NextEarthquakeId = 1 };
        var quake = DisasterModel.StartQuake(Rules, disasters, new GroundPosition(500f, 500f), 7.5f, 0, null);

        Assert.Equal(1f, DisasterModel.Shaking(quake, new GroundPosition(500f, 500f)));
        Assert.Equal(0.25f, DisasterModel.Shaking(quake, new GroundPosition(875f, 500f)), 4);
        Assert.Equal(0f, DisasterModel.Shaking(quake, new GroundPosition(1251f, 500f)));
        Assert.Equal(0.25f, DisasterModel.ShakingAt(disasters, new GroundPosition(875f, 500f)), 4);
    }

    [Fact]
    public void Flammability_NeedsDryVegetatedUnburntLand()
    {
        var region = DivineWorlds.Create(0f, 300f).Regions.Cells[0];
        region.Vegetation = 0.8f;
        region.SoilMoisture = 0.3f;

        // Dryness 0.5, squared, times the vegetation.
        Assert.Equal(0.8f * 0.25f, DisasterModel.Flammability(Rules, region), 4);
        region.Scorch = 0.5f;
        Assert.Equal(0.8f * 0.25f * 0.5f, DisasterModel.Flammability(Rules, region), 4);
        region.SoilMoisture = 0.6f;
        Assert.Equal(0f, DisasterModel.Flammability(Rules, region));
        region.SoilMoisture = 0f;
        region.Biome = Biome.Sea;
        Assert.Equal(0f, DisasterModel.Flammability(Rules, region));
    }

    [Fact]
    public void Ignite_LightsOnlyWhatCanBurn_AndOnlyFeedsAFire()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var wet = world.Regions.Cells[0];
        var dry = world.Regions.Cells[1];
        dry.SoilMoisture = 0f;
        dry.Vegetation = 0.8f;

        Assert.False(DisasterModel.Ignite(Rules, wet, 0.5f));
        Assert.Equal(0f, wet.Fire);
        Assert.True(DisasterModel.Ignite(Rules, dry, 0.4f));
        Assert.False(DisasterModel.Ignite(Rules, dry, 0.2f));
        Assert.Equal(0.4f, dry.Fire);
    }

    [Fact]
    public void Danger_IsTheNearestFireOrTheShaking_WhicheverIsWorse()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var settlement = world.Settlements[0];
        var (column, row) = world.Regions.GetCoordinates(settlement.RegionId);
        world.Regions.Cells[(row * world.Regions.Columns) + column + 2].Fire = 0.8f;

        Assert.Equal(0.8f * 0.3f, DisasterModel.FireThreat(SettlementWorlds.Rules, world.Regions, settlement), 4);
        world.Regions.Cells[(row * world.Regions.Columns) + column + 1].Fire = 0.5f;
        Assert.Equal(0.5f * 0.6f, DisasterModel.FireThreat(SettlementWorlds.Rules, world.Regions, settlement), 4);
        DisasterModel.StartQuake(Rules, world.Disasters, settlement.Position, 6.5f, 0, null);
        Assert.Equal(2f / 3f, DisasterModel.DangerTo(SettlementWorlds.Rules, world, settlement), 4);
        Assert.Equal(2f / 3f, Hazards.Of(SettlementWorlds.Rules, world, settlement).Danger, 4);
    }

    [Fact]
    public void Shield_LastsUntilItsTick()
    {
        var settlement = DivineWorlds.Create(0f, 300f).Settlements[0];
        settlement.ShieldedUntilTick = 50;

        Assert.True(DisasterModel.IsShielded(settlement, 49));
        Assert.False(DisasterModel.IsShielded(settlement, 50));
    }
}
