using System;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Settlements;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>The pure rules of the statistical settlement model.</summary>
public sealed class SettlementEconomyTests
{
    private static readonly SettlementRules Rules = SettlementWorlds.Rules;

    [Fact]
    public void SurveyFarmland_CountsLandWithinTheRadiusAndSkipsSea()
    {
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 100f, fertility: 0.5f);
        var cells = world.Regions.Cells;
        cells[6].Biome = Biome.Sea;
        cells[6].Fertility = 0f;
        cells[7].Fertility = 0.9f;
        cells[0].Fertility = 0.1f;

        var near = SettlementEconomy.SurveyFarmland(world.Regions, SettlementWorlds.CenterRegionId, radius: 1);
        var wide = SettlementEconomy.SurveyFarmland(world.Regions, SettlementWorlds.CenterRegionId, radius: 2);
        var own = SettlementEconomy.SurveyFarmland(world.Regions, SettlementWorlds.CenterRegionId, radius: 0);

        Assert.Equal(8, near.LandRegions);
        Assert.Equal((0.9f + (7 * 0.5f)) / 8f, near.MeanFertility, 5);
        Assert.Equal(24, wide.LandRegions);
        Assert.Equal(1, own.LandRegions);
        Assert.Equal(0.5f, own.MeanFertility);
    }

    [Fact]
    public void SurveyFarmland_ClipsAtTheGridEdge()
    {
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 100f, fertility: 0.5f);

        var corner = SettlementEconomy.SurveyFarmland(world.Regions, regionId: 0, radius: 2);

        Assert.Equal(9, corner.LandRegions);
        Assert.Throws<ArgumentOutOfRangeException>(() => SettlementEconomy.SurveyFarmland(world.Regions, regionId: 25, radius: 2));
    }

    [Fact]
    public void SurveyFarmland_WithoutLand_IsEmpty()
    {
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 100f, fertility: 0f);

        Assert.Equal(new FarmlandSurvey(0, 0f), SettlementEconomy.SurveyFarmland(world.Regions, SettlementWorlds.CenterRegionId, 2));
    }

    [Fact]
    public void Production_HasDiminishingReturnsAndIsCappedByTheLand()
    {
        var land = new FarmlandSurvey(LandRegions: 10, MeanFertility: 0.5f);
        var maximum = Rules.FoodPerFarmerPerStep * 0.5f * Rules.FarmersPerRegion * 10;

        var harvests = new[] { 0f, 10f, 50f, 100f, 200f, 400f, 10_000f }
            .Select(farmers => SettlementEconomy.ComputeProduction(Rules, farmers, land))
            .ToArray();

        Assert.Equal(0f, harvests[0]);
        Assert.All(harvests.Zip(harvests.Skip(1)), pair => Assert.True(pair.Second > pair.First));
        Assert.True(harvests[2] - harvests[1] > harvests[3] - harvests[2], "Each farmer adds less than the one before.");
        Assert.All(harvests, harvest => Assert.True(harvest <= maximum));
        Assert.Equal(maximum, harvests[^1], 3);
    }

    [Fact]
    public void Production_ForAFewFarmers_IsAboutTheirFullYield()
    {
        var land = new FarmlandSurvey(LandRegions: 24, MeanFertility: 0.5f);

        var harvest = SettlementEconomy.ComputeProduction(Rules, farmers: 5f, land);

        var fullYield = 5f * Rules.FoodPerFarmerPerStep * 0.5f;
        Assert.InRange(harvest, 0.98f * fullYield, fullYield);
    }

    [Fact]
    public void Production_WithoutLandOrFarmers_IsZero()
    {
        Assert.Equal(0f, SettlementEconomy.ComputeProduction(Rules, 100f, new FarmlandSurvey(0, 0f)));
        Assert.Equal(0f, SettlementEconomy.ComputeProduction(Rules, 0f, new FarmlandSurvey(9, 0.8f)));
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.9f, 0.1f)]
    public void Health_FallsWithTheUnfedShare(float fedShare, float unfedShare)
    {
        var health = SettlementEconomy.ComputeHealth(Rules, 0.8f, fedShare);

        Assert.Equal(0.8f - (Rules.StarvationHealthLossPerStep * unfedShare), health, 6);
    }

    [Fact]
    public void Health_RecoversWhenFed_UpToFull()
    {
        Assert.Equal(0.5f + Rules.HealthRecoveryPerStep, SettlementEconomy.ComputeHealth(Rules, 0.5f, 1f), 6);
        Assert.Equal(1f, SettlementEconomy.ComputeHealth(Rules, 0.999f, 1f));
        Assert.Equal(0f, SettlementEconomy.ComputeHealth(Rules with { StarvationHealthLossPerStep = 1f }, 0.3f, 0f));
    }

    [Fact]
    public void FedShare_IsTheShareOfTheNeedThatIsMet()
    {
        Assert.Equal(1f, SettlementEconomy.ComputeFedShare(available: 30f, consumption: 20f));
        Assert.Equal(0.25f, SettlementEconomy.ComputeFedShare(available: 5f, consumption: 20f));
        Assert.Equal(1f, SettlementEconomy.ComputeFedShare(available: 0f, consumption: 0f));
    }

    [Fact]
    public void IllHealth_RaisesDeathsAndLowersBirths()
    {
        var healthy = (Birth: SettlementEconomy.ComputeBirthChance(Rules, 1f), Death: SettlementEconomy.ComputeDeathChance(Rules, 1f));
        var sick = (Birth: SettlementEconomy.ComputeBirthChance(Rules, 0.5f), Death: SettlementEconomy.ComputeDeathChance(Rules, 0.5f));
        var dying = (Birth: SettlementEconomy.ComputeBirthChance(Rules, 0f), Death: SettlementEconomy.ComputeDeathChance(Rules, 0f));

        Assert.Equal(Rules.BirthChancePerPersonPerStep, healthy.Birth);
        Assert.Equal(Rules.DeathChancePerPersonPerStep, healthy.Death);
        Assert.Equal(Rules.BirthChancePerPersonPerStep * 0.5, sick.Birth, 10);
        Assert.Equal(Rules.DeathChancePerPersonPerStep + (Rules.IllHealthDeathChancePerPersonPerStep * 0.25), sick.Death, 10);
        Assert.Equal(0.0, dying.Birth);
        Assert.True(dying.Death > sick.Death);
    }

    [Theory]
    [InlineData(40, 60, 500f, 0f, false)]
    [InlineData(51, 60, 500f, 0f, true)]
    [InlineData(40, 60, 500f, 30f, true)]
    [InlineData(1000, 1000, 8000f, 0f, false)]
    public void Construction_IsWantedWhenCrowdedOrUnderWay_BelowTheCap(int population, int housing, float food, float progress, bool expected)
    {
        var world = SettlementWorlds.Create(population, housing, food, fertility: 0.5f, constructionProgress: progress);

        Assert.Equal(expected, SettlementEconomy.WantsConstruction(Rules, SettlementWorlds.Settlement(world)));
    }

    [Fact]
    public void AllocateLabour_BuildsOnlyWhenWantedAndFunded()
    {
        var crowdedAndFed = SettlementWorlds.Settlement(SettlementWorlds.Create(55, 60, food: 400f, fertility: 0.5f));
        var crowdedAndHungry = SettlementWorlds.Settlement(SettlementWorlds.Create(55, 60, food: 10f, fertility: 0.5f));
        var roomy = SettlementWorlds.Settlement(SettlementWorlds.Create(30, 60, food: 400f, fertility: 0.5f));

        var building = SettlementEconomy.AllocateLabour(Rules, crowdedAndFed);
        var waiting = SettlementEconomy.AllocateLabour(Rules, crowdedAndHungry);
        var farming = SettlementEconomy.AllocateLabour(Rules, roomy);

        Assert.Equal(55 * Rules.WorkforceShare, building.Workers, 4);
        Assert.Equal(55 * Rules.WorkforceShare * Rules.MaxBuilderShare, building.Builders, 4);
        Assert.Equal(building.Workers - building.Builders, building.Farmers, 4);
        Assert.Equal(0f, waiting.Builders);
        Assert.Equal(waiting.Workers, waiting.Farmers);
        Assert.Equal(0f, farming.Builders);
        Assert.False(SettlementEconomy.CanFundConstruction(Rules, crowdedAndHungry));
    }

    [Fact]
    public void AllocateLabour_CapsBuildersBySiteAndRemainingWork()
    {
        var large = SettlementWorlds.Settlement(SettlementWorlds.Create(900, 950, food: 7000f, fertility: 0.5f));
        var nearlyDone = SettlementWorlds.Settlement(SettlementWorlds.Create(900, 950, food: 7000f, fertility: 0.5f, constructionProgress: Rules.WorkPerHouse - 2f));

        Assert.Equal(Rules.BuildersPerSite, SettlementEconomy.AllocateLabour(Rules, large).Builders);
        Assert.Equal(2f, SettlementEconomy.AllocateLabour(Rules, nearlyDone).Builders, 4);
    }

    [Fact]
    public void Assess_ReportsStarvationAndBlockers()
    {
        var starvingOutlook = Assess(SettlementWorlds.Create(100, 120, food: 0f, fertility: 0f));
        var fullOutlook = Assess(SettlementWorlds.Create(60, 60, food: 400f, fertility: 0.5f));
        var growingOutlook = Assess(SettlementWorlds.Create(40, 60, food: 400f, fertility: 0.5f));

        Assert.True(starvingOutlook.IsStarving);
        Assert.Equal(0f, starvingOutlook.FedShare);
        Assert.Equal(BirthBlocker.NoFoodSurplus, starvingOutlook.BirthBlocker);
        Assert.Equal(1f - Rules.StarvationHealthLossPerStep, starvingOutlook.HealthAfter, 6);
        Assert.Equal(BirthBlocker.NoFreeHousing, fullOutlook.BirthBlocker);
        Assert.Equal(0.0, fullOutlook.ExpectedBirths);
        Assert.Equal(BirthBlocker.None, growingOutlook.BirthBlocker);
        Assert.Equal(40 * Rules.BirthChancePerPersonPerStep, growingOutlook.ExpectedBirths, 10);
        Assert.False(growingOutlook.IsStarving);
        Assert.True(growingOutlook.NetFood > 0f);
    }

    /// <summary>Outlook of the only settlement of a world.</summary>
    private static SettlementOutlook Assess(WorldState world) =>
        SettlementEconomy.Assess(Rules, SettlementWorlds.Settlement(world), world.Regions);
}
