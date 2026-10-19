using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Migration;

/// <summary>The migration step (spec §31–32, §82, ADR-016): leaving, the road, arriving, and the gods' policies.</summary>
public sealed class MigrationSystemTests
{
    /// <summary>A starving village at x = 200 and a comfortable one at x = 450.</summary>
    private static WorldState Famine()
    {
        var world = MigrationWorlds.Create(200f, 450f);
        world.Settlements[0].Food = 0f;
        world.Settlements[1].Housing = 160;
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: 0.6f, godId: 3);
        return world;
    }

    [Fact]
    public void AHardLife_BuildsTheWishToLeave_UntilAGroupSetsOut()
    {
        var world = Famine();
        var system = MigrationWorlds.System();

        var events = MigrationWorlds.Run(system, world, 5);
        Assert.True(world.Settlements[0].Emigration > 0f);
        Assert.Empty(world.Migration.Groups);

        MigrationWorlds.Run(system, world, 20, events);

        var started = Assert.Single(events.OfType<MigrationStarted>());
        Assert.Equal((1, 2, MigrationCause.Famine, (int?)1), (started.OriginSettlementId, started.DestinationSettlementId, started.Cause, started.GodId));
        Assert.True(started.People >= MigrationWorlds.Rules.MinGroupSize);
        Assert.Equal(100 - started.People, world.Settlements[0].Population);
        var group = Assert.Single(world.Migration.Groups);
        Assert.Equal(started.People, group.Population);
        Assert.Equal(0f, group.Food);
    }

    [Fact]
    public void AComfortableVillage_StaysHome_AndAFadingHardship_FadesTheWish()
    {
        var world = MigrationWorlds.Create(200f, 450f);
        var system = MigrationWorlds.System();
        world.Settlements[0].Emigration = 5f;

        MigrationWorlds.Run(system, world, 20);

        Assert.Empty(world.Migration.Groups);
        Assert.True(world.Settlements[0].Emigration < 5f);
    }

    [Fact]
    public void AGroup_WalksEatsAndArrives_WelcomedByItsHostsGod()
    {
        var world = Famine();
        world.Settlements[0].Food = 400f;
        var system = MigrationWorlds.System();
        var events = new List<WorldEvent>();
        var group = system.SendOut(world, world.Settlements[0], 30, events)!;
        var love = BeliefModel.Find(world.Settlements[1], 3)!.Love;

        MigrationWorlds.Run(system, world, 30, events);
        Assert.Contains(world.Migration.Groups, g => g.Id == group.Id && g.Position.X > 250f && g.Food < 30 * MigrationWorlds.Rules.FoodTakenPerPerson);
        MigrationWorlds.Run(system, world, 70, events);

        var ended = Assert.Single(events.OfType<MigrationEnded>());
        Assert.Equal((2, MigrationOutcome.Welcomed, (int?)3, (int?)1), (ended.SettlementId, ended.Outcome, ended.HostGodId, ended.GodId));
        Assert.Empty(world.Migration.Groups);
        Assert.Equal(130, world.Settlements[1].Population);
        Assert.True(BeliefModel.Find(world.Settlements[1], 3)!.Love > love);
        Assert.True(BeliefModel.Find(world.Settlements[1], 1)!.Adherence > 0f);
        Assert.Contains(DiplomacyModel.FindRelation(world, 1, 3)!.Memories, m => m.Act == DiplomaticAct.Sheltered);
    }

    [Fact]
    public void AGodDemandingConversion_ConvertsTheNewcomers_AndTheirGodResentsIt()
    {
        var world = Famine();
        world.Settlements[0].Food = 400f;
        world.Gods[2].RefugeePolicy = RefugeePolicy.DemandConversion;
        var system = MigrationWorlds.System();
        var events = new List<WorldEvent>();
        system.SendOut(world, world.Settlements[0], 30, events);

        MigrationWorlds.Run(system, world, 100, events);

        Assert.Equal(MigrationOutcome.Converted, Assert.Single(events.OfType<MigrationEnded>()).Outcome);
        var host = world.Settlements[1];
        Assert.True(BeliefModel.Find(host, 1)!.Adherence < 0.8f * 30 / 130f);
        Assert.True(BeliefModel.Find(host, 3)!.Fear > 0f);
        Assert.Contains(DiplomacyModel.FindRelation(world, 1, 3)!.Memories, m => m.Act == DiplomaticAct.Converted);
    }

    [Fact]
    public void AGodTurningMigrantsAway_SendsThemOn_AndTheyAndTheirGodResentIt()
    {
        var world = Famine();
        world.Settlements[0].Food = 400f;
        world.Gods[2].RefugeePolicy = RefugeePolicy.TurnAway;
        var system = MigrationWorlds.System();
        var events = new List<WorldEvent>();
        system.SendOut(world, world.Settlements[0], 30, events);

        MigrationWorlds.Run(system, world, 200, events);

        var turned = Assert.Single(events.OfType<MigrantsTurnedAway>());
        Assert.Equal((2, 3, 1), (turned.SettlementId, turned.ByGodId, turned.NextSettlementId));
        var home = Assert.Single(events.OfType<MigrationEnded>());
        Assert.Equal((MigrationOutcome.Returned, 1), (home.Outcome, home.SettlementId));
        Assert.Equal(70 + home.People, world.Settlements[0].Population);
        Assert.Equal(100, world.Settlements[1].Population);
        Assert.Contains(DiplomacyModel.FindRelation(world, 1, 3)!.Memories, m => m.Act == DiplomaticAct.TurnedAway);
    }

    [Fact]
    public void MigrantsWithoutFood_Sicken_AndDie_OnTheRoad()
    {
        var world = MigrationWorlds.Create(200f, 450f);
        var system = MigrationWorlds.System(MigrationWorlds.Rules with { Speed = 0.01f, DeathsPerSecond = 0.5f });
        world.Migration.Groups.Add(new MigrationGroup
        {
            Id = 1,
            OriginSettlementId = 1,
            DestinationSettlementId = 2,
            Cause = MigrationCause.Famine,
            Population = 20,
            Food = 0f,
            Health = 0.2f,
            Position = new GroundPosition(200f, 512f),
            Path = [new GroundPosition(450f, 512f)],
            Beliefs = [],
            TurnedAwayFrom = [],
            DepartedTick = 0,
        });
        world.Migration.NextGroupId = 2;

        var events = MigrationWorlds.Run(system, world, 5);
        Assert.True(world.Migration.Groups[0].Health < 0.2f);
        MigrationWorlds.Run(system, world, 60, events);

        Assert.Equal(MigrationOutcome.Perished, Assert.Single(events.OfType<MigrationEnded>()).Outcome);
        Assert.Empty(world.Migration.Groups);
    }

    [Fact]
    public void PeopleFleeingAGod_DoNotGoWhereItRules()
    {
        var world = MigrationWorlds.Create(200f, 400f, 650f);
        var (origin, its, other) = (world.Settlements[0], world.Settlements[1], world.Settlements[2]);
        DivineWorlds.Believe(origin, adherence: 0.8f, faith: 0.6f, fear: 1f, godId: 2);
        DivineWorlds.Believe(its, adherence: 0.8f, faith: 0.6f, godId: 2);
        DivineWorlds.Believe(other, adherence: 0.8f, faith: 0.6f, godId: 3);
        var events = MigrationWorlds.Run(MigrationWorlds.System(), world, 60);

        var started = events.OfType<MigrationStarted>().First();
        Assert.Equal((MigrationCause.Fear, (int?)2, 3), (started.Cause, started.AbandonedGodId, started.DestinationSettlementId));
    }

    [Fact]
    public void Migration_LeavesTheSharedStreamAlone_AndSurvivesASave()
    {
        var world = WorldGenerator(78);
        var settlement = world.Settlements[0];
        var system = MigrationWorlds.System();
        system.SendOut(world, settlement, 20, []);
        var random = new SimulationRandom(5);
        var before = random.GetState();

        system.Update(new TickContext(world, random, []));
        var copy = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));
        MigrationWorlds.Run(system, world, 20);
        MigrationWorlds.Run(system, copy, 20);

        Assert.Equal(before, random.GetState());
        WorldAssert.Equal(world, copy);
    }

    [Fact]
    public void AiGods_SetTheirPersonalitysPolicy_AndAWelcomingGodWithoutRoom_TurnsMigrantsAway()
    {
        var world = MigrationWorlds.Create(200f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f, godId: 3);
        var advisor = new RefugeeAdvisor(MigrationWorlds.Rules, new MigrationAuthority());
        var events = new List<WorldEvent>();

        foreach (var god in world.Gods.Skip(1))
        {
            advisor.Act(world, god, PantheonWorlds.Profile(god.Personality), events);
        }

        Assert.Equal([RefugeePolicy.Welcome, RefugeePolicy.DemandConversion, RefugeePolicy.Welcome, RefugeePolicy.TurnAway], world.Gods.Select(g => g.RefugeePolicy));
        Assert.Equal(2, events.OfType<RefugeePolicyChanged>().Count());

        world.Settlements[0].Housing = 60;
        var (policy, reason) = advisor.Choose(world, world.Gods[2], PantheonWorlds.Profile(GodPersonality.Guardian));
        Assert.Equal(RefugeePolicy.TurnAway, policy);
        Assert.StartsWith("no room", reason, System.StringComparison.Ordinal);
    }

    [Fact]
    public void TheAuthority_SetsOnlyTheIssuersOwnPolicy()
    {
        var world = MigrationWorlds.Create(200f);
        var authority = new MigrationAuthority();
        var events = new List<WorldEvent>();

        Assert.Equal(RefugeePolicyRejection.NotAuthorized, authority.SetPolicy(world, 2, RefugeePolicy.TurnAway, 1, events));
        Assert.Equal(RefugeePolicyRejection.UnknownGod, authority.SetPolicy(world, 9, RefugeePolicy.TurnAway, null, events));
        Assert.Equal(RefugeePolicyRejection.Unchanged, authority.SetPolicy(world, 1, RefugeePolicy.Welcome, 1, events));
        Assert.Equal(RefugeePolicyRejection.None, authority.SetPolicy(world, 1, RefugeePolicy.TurnAway, 1, events));

        Assert.Equal(RefugeePolicy.TurnAway, world.Gods[0].RefugeePolicy);
        Assert.Equal(RefugeePolicy.TurnAway, Assert.Single(events.OfType<RefugeePolicyChanged>()).Policy);
    }

    [Fact]
    public void FourGods_MigrateTheSameWayEachTime_AcrossASave()
    {
        var engine = TestContent.Shipped.CreateEngine();
        var straight = Pantheon.Game.GameSession.CreateNew(TestContent.Shipped, new WorldSeed(1)).World;
        var resumed = Pantheon.Game.GameSession.CreateNew(TestContent.Shipped, new WorldSeed(1)).World;

        engine.Run(straight, 12_000);
        engine.Run(resumed, 6_000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 6_000);

        WorldAssert.Equal(straight, resumed);
        Assert.True(straight.Migration.NextGroupId > 1);
    }

    /// <summary>A save-valid generated small world.</summary>
    private static WorldState WorldGenerator(ulong seed) =>
        Pantheon.Simulation.WorldGen.WorldGenerator.Generate(new WorldSeed(seed), TestContent.SmallWorldGen).State;
}
