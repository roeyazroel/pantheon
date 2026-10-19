using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Society;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Society;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Society;

/// <summary>What makes a settlement safe, happy, settled, rich and learned, and what its priority builds (spec §29–30, §36, ADR-023).</summary>
public sealed class SocietyModelTests
{
    private static readonly SocietyRules Rules = SocietyWorlds.Rules;

    [Fact]
    public void Danger_AndWarOverIt_MakeASettlementUnsafe_AndWallsRestoreSome()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];

        var calm = SocietyModel.SafetyTarget(Rules, village, danger: 0f, war: 0f);
        var besieged = SocietyModel.SafetyTarget(Rules, village, danger: 0.3f, war: 0.5f);
        village.Society.Buildings.Add(new Building { Kind = BuildingKind.Walls, GodId = null });
        var walled = SocietyModel.SafetyTarget(Rules, village, danger: 0.3f, war: 0.5f);

        Assert.Equal(1f, calm);
        Assert.Equal(1f - 0.3f - (0.8f * 0.5f), besieged, 3);
        Assert.Equal(besieged + Rules.DefenceSafety, walled, 3);
    }

    [Fact]
    public void AFedHealthyLovedVillage_IsHappy_AndAFearfulHomelessOneIsNot()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.9f, faith: 0.8f, love: 0.9f);
        var happy = SocietyModel.HappinessTarget(Rules, village, foodNeed: 0f, thirst: 0f);

        village.Beliefs[0].Love = 0f;
        village.Beliefs[0].Fear = 0.9f;
        village.Housing = 50;
        village.Health = 0.5f;
        var miserable = SocietyModel.HappinessTarget(Rules, village, foodNeed: 0.8f, thirst: 1f);

        Assert.True(happy > 0.8f, happy.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(miserable < 0.2f, miserable.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AUnitedPeacefulVillage_IsSettled_AndADividedOneAtWarIsNot()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", Domain.Gods.GodController.Ai, Domain.Gods.GodPersonality.Tyrant, 0f));
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.9f, faith: 0.8f);
        village.Society.Happiness = 0.8f;
        var settled = SocietyModel.StabilityTarget(Rules, village, SocietyModel.War(world, village));

        village.Beliefs[0].Adherence = 0.45f;
        DivineWorlds.Believe(village, adherence: 0.45f, faith: 0.8f, godId: 2);
        DiplomacyModel.EnsureStance(world, 1, 2).Status = DiplomaticStatus.War;
        var torn = SocietyModel.StabilityTarget(Rules, village, SocietyModel.War(world, village));

        Assert.Equal(0.9f, SocietyModel.War(world, village), 3);
        Assert.True(settled > 0.85f);
        Assert.True(torn < settled - 0.3f);
    }

    [Fact]
    public void Surplus_WithFullStores_AndTrade_MakeWealth_AndSomeIsUsedUp()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        village.Society.Wealth = 100f;

        var full = SocietyModel.WealthChange(Rules, village, surplus: 2f, storesFull: true, neighbours: 3, seconds: 1f);
        var empty = SocietyModel.WealthChange(Rules, village, surplus: 2f, storesFull: false, neighbours: 0, seconds: 1f);

        Assert.Equal((0.3f * 2f) + (0.02f * 3) - (0.001f * 100f), full, 4);
        Assert.Equal(-0.1f, empty, 4);
    }

    [Fact]
    public void Learning_GrowsWithWealthToSpare_AndOrder()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var poor = SocietyModel.EducationChange(Rules, village, 10f);
        village.Society.Wealth = 1000f;

        var rich = SocietyModel.EducationChange(Rules, village, 10f);
        village.Society.Stability = 0f;
        var unsettled = SocietyModel.EducationChange(Rules, village, 10f);

        Assert.True(poor < 0f);
        Assert.True(rich > 0.005f);
        Assert.True(unsettled < 0f);
    }

    [Fact]
    public void APriority_CallsForItsBuildings_TheTempleOfItsGod_AndLeveesOnlyBesideWater()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var society = village.Society;
        (society.Priority, society.PriorityGodId) = (SettlementPriority.Temple, 1);
        var temple = SocietyModel.NextProject(Rules, world, village);
        society.Buildings.Add(new Building { Kind = BuildingKind.Temple, GodId = 1 });
        var built = SocietyModel.NextProject(Rules, world, village);
        society.Priority = SettlementPriority.Preparation;
        var dry = SocietyModel.NextProject(Rules, world, village);
        WaterWorlds.LayRiver(world);
        var wet = SocietyModel.NextProject(Rules, world, village);

        Assert.Equal(BuildingKind.Temple, temple!.Kind);
        Assert.Null(built);
        Assert.Equal(BuildingKind.Firebreak, dry!.Kind);
        Assert.Equal(BuildingKind.Levee, wet!.Kind);
        Assert.Equal(TechnologyField.Engineering, SocietyModel.Focus(Rules, village));
    }
}
