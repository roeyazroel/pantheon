using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Migration;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Migration;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Migration;

/// <summary>Migration's rules as pure functions (spec §31–32, ADR-016).</summary>
public sealed class MigrationModelTests
{
    private static readonly MigrationRules Rules = MigrationWorlds.Rules;

    private static SettlementConditions Conditions(float foodNeed = 0f, float dangerNeed = 0f) => new(0f, 1f, foodNeed, 0f, 0f, dangerNeed);

    [Fact]
    public void EachHardship_PushesPeopleOut_AndNamesTheCause()
    {
        var world = MigrationWorlds.Create(200f);
        var settlement = world.Settlements[0];

        Assert.Equal(new MigrationPressure(0.8f, MigrationCause.Famine), MigrationModel.Push(Rules, world, settlement, Conditions(foodNeed: 0.8f)));
        Assert.Equal(new MigrationPressure(0.4f, MigrationCause.Danger), MigrationModel.Push(Rules, world, settlement, Conditions(dangerNeed: 0.5f)));

        settlement.Housing = 60;
        var homeless = MigrationModel.Push(Rules, world, settlement, Conditions());
        Assert.Equal(MigrationCause.Homelessness, homeless.Cause);
        Assert.Equal(0.4f, homeless.Value, 4);
    }

    [Fact]
    public void FearAndDoubt_OfTheGodsFollowed_PushPeopleOut()
    {
        var world = MigrationWorlds.Create(200f);
        var settlement = world.Settlements[0];
        DivineWorlds.Believe(settlement, adherence: 0.5f, faith: 0.5f, fear: 0.9f, doubt: 0.2f);

        var pressure = MigrationModel.Push(Rules, world, settlement, Conditions());

        Assert.Equal(MigrationCause.Fear, pressure.Cause);
        Assert.Equal((0.6f * 0.9f) + (0.5f * 0.2f), pressure.Value, 4);
    }

    [Fact]
    public void Love_KeepsPeopleHome()
    {
        var world = MigrationWorlds.Create(200f);
        var settlement = world.Settlements[0];
        DivineWorlds.Believe(settlement, adherence: 0.8f, faith: 0.8f, love: 1f);

        var pressure = MigrationModel.Push(Rules, world, settlement, Conditions(foodNeed: 0.8f));

        Assert.Equal(0.8f * (1f - Rules.LoveLoyalty), pressure.Value, 4);
    }

    [Fact]
    public void War_NeedsTwoGodsAtWar_EachFollowedByEnough()
    {
        var world = MigrationWorlds.Create(200f);
        var settlement = world.Settlements[0];
        DivineWorlds.Believe(settlement, adherence: 0.4f, faith: 0.5f);
        var theirs = DivineWorlds.Believe(settlement, adherence: 0.4f, faith: 0.5f, godId: 2);

        Assert.Equal(0f, MigrationModel.War(Rules, world, settlement));

        MigrationWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        Assert.Equal(0.8f, MigrationModel.War(Rules, world, settlement), 4);
        Assert.Equal(MigrationCause.War, MigrationModel.Push(Rules, world, settlement, Conditions()).Cause);

        theirs.Adherence = 0.1f;
        Assert.Equal(0f, MigrationModel.War(Rules, world, settlement));
    }

    [Fact]
    public void Migrants_GiveUpTheGodsTheyFearOrDoubt_InProportion()
    {
        var world = MigrationWorlds.Create(200f);
        var settlement = world.Settlements[0];
        DivineWorlds.Believe(settlement, adherence: 0.6f, faith: 0.5f, fear: 0.8f, doubt: 0.2f);
        DivineWorlds.Believe(settlement, adherence: 0.3f, faith: 0.5f, doubt: 0.1f, godId: 3);

        var (beliefs, abandoned) = MigrationModel.MigrantBeliefs(Rules, settlement.Beliefs);

        Assert.Equal(1, abandoned);
        Assert.Equal(0.6f * (1f - (Rules.AbandonShare * 0.8f)), beliefs[0].Adherence, 4);
        Assert.Equal(0.3f * (1f - (Rules.AbandonShare * 0.1f)), beliefs[1].Adherence, 4);
        Assert.Equal(0.6f, settlement.Beliefs[0].Adherence);
    }

    [Fact]
    public void AMildDoubt_IsNoAbandonment()
    {
        var world = MigrationWorlds.Create(200f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f, doubt: 0.25f);

        Assert.Null(MigrationModel.MigrantBeliefs(Rules, world.Settlements[0].Beliefs).Abandoned);
    }

    [Fact]
    public void Appeal_WeighsRoomFoodSafetyTheirGodsAndDistance()
    {
        var world = MigrationWorlds.Create(200f, 400f);
        var (here, there) = (world.Settlements[0], world.Settlements[1]);
        DivineWorlds.Believe(here, adherence: 0.8f, faith: 0.5f);
        var migrants = new List<GodBelief> { new() { GodId = 1, Adherence = 0.8f, Faith = 0.5f, Love = 0f, Fear = 0f, Doubt = 0f } };

        var near = MigrationModel.Appeal(Rules, here, Conditions(), migrants, 100f);

        Assert.True(near > MigrationModel.Appeal(Rules, here, Conditions(), migrants, 800f));
        Assert.True(near > MigrationModel.Appeal(Rules, here, Conditions(foodNeed: 0.8f), migrants, 100f));
        Assert.True(near > MigrationModel.Appeal(Rules, here, Conditions(dangerNeed: 0.8f), migrants, 100f));
        Assert.True(near > MigrationModel.Appeal(Rules, there, Conditions(), migrants, 100f));
        Assert.Equal(0.8f, MigrationModel.FaithMatch(migrants, here.Beliefs), 4);
    }

    [Fact]
    public void DemandingConversion_TakesItsShareOfOtherGodsAndTheGodless()
    {
        var migrants = new List<GodBelief>
        {
            new() { GodId = 1, Adherence = 0.5f, Faith = 0.7f, Love = 0.5f, Fear = 0f, Doubt = 0.2f },
            new() { GodId = 2, Adherence = 0.1f, Faith = 0.4f, Love = 0f, Fear = 0f, Doubt = 0f },
        };

        var moved = MigrationModel.Convert(Rules, migrants, hostGodId: 2, hostFaith: 0.8f);

        var expected = (0.4f + 0.5f) * Rules.ConversionDemand;
        Assert.Equal(expected, moved, 4);
        Assert.Equal(0.5f * (1f - Rules.ConversionDemand), migrants[0].Adherence, 4);
        var host = migrants[1];
        Assert.Equal(0.1f + expected, host.Adherence, 4);
        Assert.Equal(((0.4f * 0.1f) + (0.8f * expected)) / host.Adherence, host.Faith, 4);
        Assert.Equal(Rules.ConversionFear * expected / host.Adherence, host.Fear, 4);
        Assert.True(BeliefModel.TotalAdherence(migrants) <= 1f + 1e-5f);
    }

    [Fact]
    public void Arrivals_MixWithTheirHosts_ByPopulation()
    {
        var world = MigrationWorlds.Create(200f);
        var host = world.Settlements[0];
        host.Health = 1f;
        DivineWorlds.Believe(host, adherence: 0.8f, faith: 0.8f, love: 0.6f);
        var group = Group(population: 100, food: 50f, health: 0.5f, new GodBelief { GodId = 2, Adherence = 0.6f, Faith = 0.6f, Love = 0.2f, Fear = 0.4f, Doubt = 0.3f });

        MigrationModel.Merge(host, group);

        Assert.Equal(200, host.Population);
        Assert.Equal(450f, host.Food, 3);
        Assert.Equal(0.75f, host.Health, 4);
        Assert.Equal(0.4f, BeliefModel.Find(host, 1)!.Adherence, 4);
        Assert.Equal(0.6f, BeliefModel.Find(host, 1)!.Love, 4);
        var newcomers = BeliefModel.Find(host, 2)!;
        Assert.Equal(0.3f, newcomers.Adherence, 4);
        Assert.Equal(0.4f, newcomers.Fear, 4);
    }

    [Fact]
    public void Welcome_RaisesLoveByTheNewcomersShare_AndTurningAway_IsResented()
    {
        var world = MigrationWorlds.Create(200f);
        var host = world.Settlements[0];
        var belief = DivineWorlds.Believe(host, adherence: 0.8f, faith: 0.8f, love: 0.2f);

        MigrationModel.Welcome(Rules, host, 1, arrived: 50);

        Assert.Equal(0.2f + (Rules.WelcomeLove * 0.5f), belief.Love, 4);
        var migrants = new List<GodBelief> { new() { GodId = 1, Adherence = 0.5f, Faith = 0.5f, Love = 0.5f, Fear = 0f, Doubt = 0f } };
        MigrationModel.Resent(Rules, migrants, 1);
        Assert.Equal(0.5f - Rules.TurnedAwayLoss, migrants[0].Love, 4);
    }

    [Fact]
    public void MainGod_IsTheMostFollowed_LowestIdOnATie()
    {
        GodBelief Belief(int god, float adherence) => new() { GodId = god, Adherence = adherence, Faith = 0f, Love = 0f, Fear = 0f, Doubt = 0f };

        Assert.Equal(2, MigrationModel.MainGod([Belief(3, 0.3f), Belief(2, 0.3f), Belief(1, 0.1f)]));
        Assert.Null(MigrationModel.MainGod([Belief(1, 0f)]));
    }

    [Fact]
    public void FollowersHomeless_WeighsEachSettlementByTheGodsFollowersThere()
    {
        var world = MigrationWorlds.Create(200f, 450f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.5f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.5f, faith: 0.5f);
        world.Settlements[0].Housing = 50;

        Assert.Equal(0.25f, MigrationModel.FollowersHomeless(world, 1), 4);
        Assert.Equal(0f, MigrationModel.FollowersHomeless(world, 2));
    }

    /// <summary>A group at the origin with no way left to walk.</summary>
    internal static MigrationGroup Group(int population, float food, float health, params GodBelief[] beliefs) => new()
    {
        Id = 1,
        OriginSettlementId = 1,
        DestinationSettlementId = 1,
        Cause = MigrationCause.Famine,
        Population = population,
        Food = food,
        Health = health,
        Position = default,
        Path = [],
        Beliefs = [.. beliefs],
        TurnedAwayFrom = [],
        DepartedTick = 0,
    };
}
