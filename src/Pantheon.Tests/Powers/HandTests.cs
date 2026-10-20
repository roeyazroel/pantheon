using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Hand;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Hand;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Powers;

/// <summary>The Divine Hand (spec §55, ADR-030): lifting, carrying, rescuing, throwing and planting.</summary>
public sealed class HandTests
{
    private static readonly HandRules Rules = TestContent.Shipped.DivineRules.Hand;
    private static readonly HandAuthority Hand = new(Rules, DivineWorlds.Rules, SettlementWorlds.Rules);

    [Fact]
    public void PeopleLiftedOutOfAFire_AndSetDownInASafeVillage_JoinIt_AndLoveTheirGodForIt()
    {
        var world = DivineWorlds.Create(0f, 300f, 450f);
        var (burning, safe) = (world.Settlements[0], world.Settlements[1]);
        var belief = DivineWorlds.Believe(burning, adherence: 0.8f, faith: 0.6f);
        DivineWorlds.Believe(safe, adherence: 0.5f, faith: 0.5f);
        world.Regions.Cells[burning.RegionId].Fire = 1f;
        DisasterWorlds.Parch(world);
        var events = new List<WorldEvent>();

        Assert.Equal(HandRejection.None, Use(world, HandAction.PickUp, burning.Position, events));
        var held = world.Gods[0].Held!;
        Assert.Equal((HeldKind.Villagers, 10, true), (held.Kind, held.Amount, held.FromDanger));
        Assert.Equal(90, burning.Population);

        var love = BeliefModel.Find(safe, DivineWorlds.GodId)!.Love;
        Assert.Equal(HandRejection.None, Use(world, HandAction.SetDown, safe.Position, events));

        Assert.Null(world.Gods[0].Held);
        Assert.Equal(110, safe.Population);
        Assert.True(BeliefModel.Find(safe, DivineWorlds.GodId)!.Love > love);
        Assert.True(Assert.Single(events.OfType<VillagersSetDown>()).Rescued);
        Assert.True(world.Gods[0].Conduct.Help > 0f);
        Assert.True(Assert.Single(events.OfType<VillagersLifted>()).FromDanger);
        Assert.Equal(0f, belief.Fear);
    }

    [Fact]
    public void PeopleTakenFromASafeHome_AreAfraid_AndSetDownInTheWilds_WalkToTheNearestVillage()
    {
        var world = DivineWorlds.Create(0f, 300f, 600f);
        var home = world.Settlements[0];
        var belief = DivineWorlds.Believe(home, adherence: 0.8f, faith: 0.6f);
        var events = new List<WorldEvent>();

        Use(world, HandAction.PickUp, home.Position, events);
        Assert.True(belief.Fear > 0f);
        Assert.True(world.Gods[0].Conduct.Harm > 0f);

        var wilds = new GroundPosition(520f, 512f);
        Assert.Equal(HandRejection.None, Use(world, HandAction.SetDown, wilds, events));

        var group = Assert.Single(world.Migration.Groups);
        Assert.Equal((MigrationCause.Carried, 10, world.Settlements[1].Id), (group.Cause, group.Population, group.DestinationSettlementId));
        Assert.False(Assert.Single(events.OfType<VillagersSetDown>()).Rescued);
    }

    [Fact]
    public void PeopleSetDownInTheSea_Drown()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        HazardWorlds.Coast(world, 2);
        var events = new List<WorldEvent>();

        Use(world, HandAction.PickUp, world.Settlements[0].Position, events);
        Assert.Equal(HandRejection.None, Use(world, HandAction.SetDown, new GroundPosition(60f, 512f), events));

        var drowned = Assert.Single(events.OfType<VillagersSetDown>());
        Assert.True(drowned.Drowned);
        Assert.Equal(90, world.Settlements[0].Population);
        Assert.Empty(world.Migration.Groups);
    }

    [Fact]
    public void AThrownRock_WrecksAndKills_Frightens_AndLeavesACrater_AndStrikingOnesOwnIsCruel()
    {
        var world = DivineWorlds.Create(0f, 300f, 600f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        var target = world.Settlements[1];
        (target.Population, target.Housing) = (400, 400);
        var theirs = DivineWorlds.Believe(target, adherence: 0.7f, faith: 0.5f);
        world.Regions.Cells.ForEach(r => r.Vegetation = 0.2f);
        var events = new List<WorldEvent>();

        Use(world, HandAction.PickUp, new GroundPosition(450f, 700f), events);
        Assert.Equal(HeldKind.Rock, world.Gods[0].Held!.Kind);
        Assert.Equal(HandRejection.None, Use(world, HandAction.Throw, target.Position, events));

        var struck = Assert.Single(events.OfType<HandStruck>());
        Assert.Equal(target.Id, struck.SettlementId);
        Assert.True(struck.HousesLost > 0 && struck.Deaths > 0);
        Assert.Equal(target.Ruins, struck.HousesLost);
        Assert.True(theirs.Fear > 0f);
        Assert.Contains(world.Disasters.Marks, m => m.Kind == TerrainMarkKind.Crater);
        Assert.True(world.Gods[0].Conduct.Cruelty > 0f);
    }

    [Fact]
    public void ATreeIsPulledFromGreenLand_AndPlantedWhereItIsSetDown()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        world.Regions.Cells.ForEach(r => r.Vegetation = 0.8f);
        var (from, to) = (new GroundPosition(450f, 700f), new GroundPosition(150f, 700f));
        world.Regions.TryGetRegionIdAt(from.X, from.Z, out var fromId);
        world.Regions.TryGetRegionIdAt(to.X, to.Z, out var toId);

        Use(world, HandAction.PickUp, from, []);
        Use(world, HandAction.SetDown, to, []);

        Assert.Equal(0.8f - Rules.TreeCover, world.Regions.Cells[fromId].Vegetation, 4);
        Assert.Equal(0.8f + Rules.TreeCover, world.Regions.Cells[toId].Vegetation, 4);
    }

    [Fact]
    public void TheGodsCreature_WaitsInTheHand_AndIsSetDownWhereTheHandGoes_OrHurtWhenThrown()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        var system = new CreatureSystem(CreatureWorlds.Rules, DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths);
        var events = new List<WorldEvent>();

        Assert.Equal(HandRejection.None, Use(world, HandAction.PickUp, creature.Position, events));
        Assert.Equal(HeldKind.Creature, world.Gods[0].Held!.Kind);
        Assert.True(CreatureModel.IsHeld(world, creature));
        Assert.Equal("Held in its god's hand", Pantheon.Game.Creatures.CreatureText.DescribeActivity(creature, world));
        var hunger = creature.Needs.Hunger;
        system.Update(new TickContext(world, new SimulationRandom(1), events));
        Assert.Equal(hunger, creature.Needs.Hunger);

        var there = new GroundPosition(creature.Position.X + 100f, creature.Position.Z);
        Use(world, HandAction.SetDown, there, events);
        Assert.Equal(there, creature.Position);
        Assert.Equal(CreaturePhase.Deciding, creature.Activity.Phase);

        Use(world, HandAction.PickUp, creature.Position, events);
        var (health, loyalty) = (creature.Health, creature.Loyalty);
        Use(world, HandAction.Throw, new GroundPosition(there.X + 50f, there.Z), events);
        Assert.Equal(health - Rules.ThrownCreatureHealth, creature.Health, 4);
        Assert.True(creature.Loyalty < loyalty);
    }

    [Fact]
    public void TheHand_IsRefused_WhereItCannotReachOrDoWhatIsAsked()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        var home = world.Settlements[0].Position;
        var disabled = new HandAuthority(Rules with { Enabled = false }, DivineWorlds.Rules, SettlementWorlds.Rules);

        Assert.Equal(HandRejection.NoHand, disabled.Execute(world, new HandCommand(DivineWorlds.GodId, HandAction.PickUp, home), []));
        Assert.Equal(HandRejection.UnknownGod, Hand.Execute(world, new HandCommand(42, HandAction.PickUp, home), []));
        Assert.Equal(HandRejection.OffTheWorld, Use(world, HandAction.PickUp, new GroundPosition(-10f, -10f), []));
        Assert.Equal(HandRejection.NoInfluence, Use(world, HandAction.PickUp, new GroundPosition(1000f, 1000f), []));
        Assert.Equal(HandRejection.HandEmpty, Use(world, HandAction.SetDown, home, []));
        Use(world, HandAction.PickUp, home, []);
        Assert.Equal(HandRejection.HandFull, Use(world, HandAction.PickUp, home, []));

        Use(world, HandAction.SetDown, home, []);
        HazardWorlds.Coast(world, 4);
        Assert.Equal(HandRejection.NothingThere, Use(world, HandAction.PickUp, new GroundPosition(200f, 512f), []));
    }

    [Fact]
    public void AFightingCreature_IsNotLifted_NorSetDownInTheSea()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        (creature.Activity.Action, creature.Activity.Phase) = (CreatureAction.Fight, CreaturePhase.Performing);

        Assert.Equal(HandRejection.CreatureFighting, Use(world, HandAction.PickUp, creature.Position, []));
        Assert.Null(world.Gods[0].Held);

        creature.Activity.Phase = CreaturePhase.Deciding;
        Use(world, HandAction.PickUp, creature.Position, []);
        HazardWorlds.Coast(world, 2);
        Assert.Equal(HandRejection.NotOnLand, Use(world, HandAction.SetDown, new GroundPosition(60f, 512f), []));
        Assert.NotNull(world.Gods[0].Held);
    }

    [Fact]
    public void TheGodsCreature_IsSetDownBeyondItsInfluence_AndLiftedBeforeAVillagesPeople()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        var village = world.Settlements[0];
        var beyond = new GroundPosition(1000f, 1000f);
        Assert.Equal(HandRejection.NoInfluence, Use(world, HandAction.PickUp, beyond, []));

        // Its own Creature goes wherever its god sets it down, as it may walk anywhere (ADR-070).
        Use(world, HandAction.PickUp, creature.Position, []);
        Assert.Equal(HandRejection.None, Use(world, HandAction.SetDown, beyond, []));
        Assert.Equal(beyond, creature.Position);
        Use(world, HandAction.PickUp, creature.Position, []);
        Assert.Equal(HandRejection.None, Use(world, HandAction.Throw, new GroundPosition(beyond.X - 40f, beyond.Z), []));

        // Within half the hand's reach, the Creature is lifted even when the village lies nearer the hand.
        creature.Position = new GroundPosition(village.Position.X + (Rules.Reach * 0.4f), village.Position.Z);
        var people = village.Population;
        Assert.Equal(HandRejection.None, Use(world, HandAction.PickUp, new GroundPosition(village.Position.X + 2f, village.Position.Z), []));
        Assert.Equal(HeldKind.Creature, world.Gods[0].Held!.Kind);
        Assert.Equal(people, village.Population);
    }

    /// <summary>The god of the divine worlds uses its hand.</summary>
    private static HandRejection Use(WorldState world, HandAction action, GroundPosition target, List<WorldEvent> events) =>
        Hand.Execute(world, new HandCommand(DivineWorlds.GodId, action, target), events);
}
