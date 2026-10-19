using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Ecology;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Nature;

/// <summary>The land's life (spec §18, ADR-028): harm, scars and their healing, wildlife and hunting, and pollution.</summary>
public sealed class EcologyTests
{
    private static readonly EcologyRules Rules = NatureWorlds.Ecology;

    [Fact]
    public void Harm_HealsDownToTheScar_AndHarmAboveTheThresholdBecomesScar()
    {
        var (light, lightScar) = EcologyModel.Heal(Rules, 0.4f, 0f, 1f, nature: false);
        var (heavy, heavyScar) = EcologyModel.Heal(Rules, 0.9f, 0f, 1f, nature: false);
        var (scarred, scar) = EcologyModel.Heal(Rules, 0.3f, 0.3f, 100f, nature: false);

        Assert.Equal((0.36f, 0f), (light, lightScar), new FloatPair(4));
        Assert.Equal(0.04f, heavyScar, 4);
        Assert.Equal(heavyScar + ((0.9f - heavyScar) * 0.9f), heavy, 4);
        Assert.Equal((0.3f, 0.3f), (scarred, scar));
    }

    [Fact]
    public void AGodOfNature_HealsFaster_AndHealsScars()
    {
        var (plain, plainScar) = EcologyModel.Heal(Rules, 0.4f, 0.2f, 1f, nature: false);
        var (tended, tendedScar) = EcologyModel.Heal(Rules, 0.4f, 0.2f, 1f, nature: true);

        Assert.Equal(0.2f, plainScar);
        Assert.Equal(0.15f, tendedScar, 4);
        Assert.True(tended < plain);
    }

    [Fact]
    public void TheLandsHealth_CapacityAndBiodiversity_FallWithHarmAndPollution()
    {
        var region = SettlementWorlds.Create(100, 200, 100f, 0.5f).Regions.Cells[0];
        (region.Vegetation, region.Wildlife) = (0.8f, 0.6f);
        var whole = (EcologyModel.WildlifeCapacity(region), EcologyModel.Biodiversity(region));

        (region.Damage, region.Pollution) = (0.5f, 0.2f);

        Assert.Equal((0.8f, 0.7f), whole, new FloatPair(4));
        Assert.Equal(0.4f, EcologyModel.Health(region), 4);
        Assert.Equal(0.32f, EcologyModel.WildlifeCapacity(region), 4);
        Assert.Equal(0.28f, EcologyModel.Biodiversity(region), 4);
    }

    [Fact]
    public void FireAndFlood_HarmTheLand_AndFireKillsItsAnimals_WhichReturnAfter()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        var burning = world.Regions.Cells[0];
        var flooded = world.Regions.Cells[1];
        burning.Wildlife = 0.5f;
        (burning.Fire, flooded.Flood) = (1f, 1f);
        var system = new EcologySystem(Rules, SettlementWorlds.Rules);

        system.Update(new TickContext(world, new SimulationRandom(1), []));

        Assert.True(burning.Damage > 0f && flooded.Damage > 0f && burning.Damage > flooded.Damage);
        Assert.True(burning.Wildlife < 0.5f);

        burning.Fire = 0f;
        var after = burning.Wildlife;
        system.Update(new TickContext(world, new SimulationRandom(1), []));
        Assert.True(burning.Wildlife > after);
    }

    [Fact]
    public void AVillage_HuntsTheAnimalsAroundIt_ForFood()
    {
        var world = SettlementWorlds.Create(200, 200, 0f, 0.5f);
        var settlement = SettlementWorlds.Settlement(world);
        foreach (var region in EcologyModel.Around(world.Regions, settlement.RegionId, 1))
        {
            region.Wildlife = 0.5f;
        }

        var food = EcologyModel.Hunt(Rules, world.Regions, settlement, 1f);

        Assert.Equal(0.2f * 10f, food, 4);
        Assert.Equal(4.5f - 0.2f, EcologyModel.Around(world.Regions, settlement.RegionId, 1).Sum(r => r.Wildlife), 3);
        world.Regions.Cells.ForEach(r => r.Wildlife = 0f);
        Assert.Equal(0f, EcologyModel.Hunt(Rules, world.Regions, settlement, 1f));
    }

    [Fact]
    public void TheHunt_FeedsTheVillage_InItsOwnStep()
    {
        var hunting = SettlementWorlds.Create(200, 200, 0f, 0.5f);
        var fasting = SettlementWorlds.Create(200, 200, 0f, 0.5f);
        hunting.Regions.Cells.ForEach(r => r.Wildlife = 0.5f);

        new SettlementSystem(SettlementWorlds.NoBirthsOrDeaths, ecology: Rules).Update(new TickContext(hunting, new SimulationRandom(1), []));
        new SettlementSystem(SettlementWorlds.NoBirthsOrDeaths).Update(new TickContext(fasting, new SimulationRandom(1), []));

        Assert.True(SettlementWorlds.Settlement(hunting).Food > SettlementWorlds.Settlement(fasting).Food);
    }

    [Fact]
    public void HarmedAndPollutedLand_YieldsLess()
    {
        var world = SettlementWorlds.Create(100, 200, 0f, 0.6f);
        var settlement = SettlementWorlds.Settlement(world);
        var whole = SettlementEconomy.SurveyFarmland(world.Regions, settlement.RegionId, 1).MeanFertility;

        world.Regions.Cells.ForEach(r => (r.Damage, r.Pollution) = (0.5f, 0.5f));

        Assert.Equal(whole * 0.25f, SettlementEconomy.SurveyFarmland(world.Regions, settlement.RegionId, 1).MeanFertility, 4);
    }

    [Fact]
    public void ACrowdedVillage_PollutesItsLand_WhichClearsSlowly()
    {
        var world = SettlementWorlds.Create(600, 600, 1000f, 0.5f);
        var settlement = SettlementWorlds.Settlement(world);
        var system = new EcologySystem(Rules, SettlementWorlds.Rules);

        system.Update(new TickContext(world, new SimulationRandom(1), []));
        var region = world.Regions.Cells[settlement.RegionId];

        Assert.Equal(0.1f * 0.5f, region.Pollution, 4);
        settlement.Population = 50;
        var before = region.Pollution;
        system.Update(new TickContext(world, new SimulationRandom(1), []));
        Assert.Equal(before * 0.99f, region.Pollution, 5);
    }

    [Fact]
    public void AVillagesLand_IsReportedScarred_Once()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        var settlement = SettlementWorlds.Settlement(world);
        var system = new EcologySystem(Rules, SettlementWorlds.Rules);
        var events = new List<WorldEvent>();
        world.Regions.Cells.ForEach(r => r.Damage = 1f);

        for (var i = 0; i < 5; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events));
        }

        var scarred = Assert.Single(events.OfType<LandScarred>());
        Assert.Equal(settlement.Id, scarred.SettlementId);
        Assert.True(EcologySystem.ScarOf(world.Regions, settlement, SettlementWorlds.Rules.FarmlandRadius) >= Rules.ScarNotice);
    }

    [Fact]
    public void Smiting_HarmsTheLandItFallsOn()
    {
        var home = new GroundPosition(200f, 512f);
        var world = DivineWorlds.Create(1000f, home.X);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f);
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, ecology: Rules);
        var region = world.Regions.Cells[world.Settlements[0].RegionId];

        var check = caster.Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "lightning", world.Settlements[0].Position), DivineWorlds.GodId, []);

        Assert.True(check.IsAccepted, check.Rejection.ToString());
        Assert.Equal(Rules.HarmDamage, region.Damage, 4);
    }

    [Fact]
    public void AnEarthquake_HarmsTheLandItShakes()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        var centre = world.Regions.GetRegionCenter(SettlementWorlds.Settlement(world).RegionId);
        Pantheon.Simulation.Disasters.DisasterModel.StartQuake(DisasterWorlds.Rules, world.Disasters, centre, 6f, 0, godId: null);
        var disasters = new Pantheon.Simulation.Disasters.DisasterSystem(DisasterWorlds.Rules with { EarthquakesPerHour = 0f }, SettlementWorlds.Rules, DivineWorlds.Rules, WeatherWorlds.Rules, ecology: Rules);

        disasters.Update(new TickContext(world, new SimulationRandom(1), []));

        Assert.True(world.Regions.Cells[SettlementWorlds.Settlement(world).RegionId].Damage > 0f);
    }

    /// <summary>Compares pairs of floats to a precision.</summary>
    private sealed class FloatPair(int digits) : IEqualityComparer<(float, float)>
    {
        public bool Equals((float, float) x, (float, float) y) =>
            System.Math.Round(x.Item1, digits) == System.Math.Round(y.Item1, digits) && System.Math.Round(x.Item2, digits) == System.Math.Round(y.Item2, digits);

        public int GetHashCode((float, float) obj) => 0;
    }
}
