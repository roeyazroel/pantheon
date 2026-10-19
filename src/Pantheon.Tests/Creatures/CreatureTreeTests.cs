using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Hand;
using Pantheon.Domain.World;
using Pantheon.Game.Alerts;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>The Creature's hierarchical state machine and behaviour trees (spec §86, ADR-047).</summary>
public sealed class CreatureTreeTests
{
    private static readonly SimulationEngine Engine = new(SettlementWorlds.NoBirthsOrDeaths, DivineWorlds.Rules, CreatureWorlds.Rules);

    [Fact]
    public void ACreatureGoingToHelpBuild_GivesUp_WhenThereIsNothingLeftToBuild()
    {
        var (world, creature) = CreatureWorlds.Create(0f, 600f);
        var village = world.Settlements[1];
        Go(creature, CreatureAction.HelpBuild, CreatureSituation.VillageBuilding, village);

        var events = Run(world, creature, 30);

        var given = Assert.Single(events.OfType<CreatureGaveUp>());
        Assert.Equal((CreatureAction.HelpBuild, GiveUpReason.NothingToBuild, (int?)village.Id), (given.Action, given.Reason, given.SettlementId));
        Assert.Empty(events.OfType<CreatureActed>());
        Assert.Equal(CreaturePhase.Deciding, creature.Activity.Phase);
    }

    [Fact]
    public void ACreatureGoingToFeedAVillage_GivesUp_WhenItNeedsNoFood_ButFeedsABarrenOneThatStillDoes()
    {
        var gaveUp = new List<bool>();
        foreach (var food in new[] { 4000f, 0f })
        {
            var (world, creature) = CreatureWorlds.Create(0f, 300f);
            var village = world.Settlements[1];
            village.Food = food;
            world.Regions.Cells.ForEach(r => r.Fertility = 0f);
            Go(creature, CreatureAction.Feed, CreatureSituation.VillageInNeed, village);

            var events = Run(world, creature, 1200);

            gaveUp.Add(events.OfType<CreatureGaveUp>().Any(e => e.Reason == GiveUpReason.NoLongerInNeed));
            Assert.Equal(food > 0f ? 0 : 1, events.OfType<CreatureActed>().Count(e => e.Action == CreatureAction.Feed));
        }

        Assert.Equal([true, false], gaveUp);
    }

    [Fact]
    public void ACreatureGoingToAttack_TurnsBack_WhenTheVillageNowFollowsAnAlly_AndOneGoingToEat_WhenTheStoresAreEmpty()
    {
        var (world, creature) = CreatureWorlds.Create(0f, 600f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 100f));
        var village = world.Settlements[1];
        DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.5f, godId: 2);
        DiplomacyModel.EnsureStance(world, creature.GodId, 2).Status = DiplomaticStatus.Alliance;
        Go(creature, CreatureAction.Attack, CreatureSituation.VillageThriving, village);
        var attack = Run(world, creature, 30);

        village.Food = 0f;
        Go(creature, CreatureAction.EatFromStores, CreatureSituation.Hungry, village);
        var eat = Run(world, creature, 30);

        Assert.Equal(GiveUpReason.NowAnAlly, Assert.Single(attack.OfType<CreatureGaveUp>()).Reason);
        Assert.Equal(GiveUpReason.StoresEmpty, Assert.Single(eat.OfType<CreatureGaveUp>()).Reason);
        Assert.Equal(100, village.Population);
    }

    [Fact]
    public void APressingNeed_EndsACommandedWalk_AndCountsACommandedWaitAsDone()
    {
        var (world, creature) = CreatureWorlds.Create(0f, 600f);
        Go(creature, CreatureAction.GoTo, CreatureSituation.Commanded, null, world.Settlements[1].Position);
        creature.Needs.Hunger = 0.95f;
        var walk = Run(world, creature, 5);

        creature.Needs.Hunger = 0.95f;
        (creature.Activity.Action, creature.Activity.Phase, creature.Activity.TicksLeft) = (CreatureAction.GoTo, CreaturePhase.Performing, 500);
        var wait = Run(world, creature, 5);

        Assert.Equal(GiveUpReason.Desperate, Assert.Single(walk.OfType<CreatureGaveUp>()).Reason);
        Assert.Empty(wait.OfType<CreatureGaveUp>());
        Assert.Equal(CreatureAction.GoTo, Assert.Single(wait.OfType<CreatureActed>()).Action);
    }

    [Fact]
    public void TheStateMachine_TellsFreeEngagedHeldAndBanished_AndTheTreeWhereItsSequenceStands()
    {
        var (world, creature) = CreatureWorlds.Create(0f, 600f);
        Go(creature, CreatureAction.Feed, CreatureSituation.VillageInNeed, world.Settlements[1]);

        Assert.Equal((CreatureMode.Free, "Free › Travelling"), (CreatureModes.ModeOf(world, creature), CreatureModes.Describe(world, creature)));
        Assert.Equal("guard: still in need ▸ travel ▶ perform · deed", CreatureTree.Describe(creature));
        creature.Activity.Phase = CreaturePhase.Performing;
        Assert.Equal("guard: still in need ▸ travel ✓ perform ▶ deed", CreatureTree.Describe(creature));
        (creature.Activity.Action, creature.Activity.TargetCreatureId) = (CreatureAction.Befriend, 9);
        Assert.Equal((CreatureMode.Engaged, "Engaged › Playing"), (CreatureModes.ModeOf(world, creature), CreatureModes.Describe(world, creature)));
        world.Gods[0].Held = new HeldObject { Kind = HeldKind.Creature, Amount = 1, Tick = 0 };
        Assert.Equal(CreatureMode.Held, CreatureModes.ModeOf(world, creature));
        world.Gods[0].Held = null;
        creature.Activity.Phase = CreaturePhase.Banished;
        Assert.Equal("Banished", CreatureModes.Describe(world, creature));
    }

    [Fact]
    public void TheGodIsTold_WhenItsCreatureGivesUp_AndNotOfAnothers()
    {
        var (world, creature) = CreatureWorlds.Create(0f, 600f);
        var village = world.Settlements[1];
        List<WorldEvent> events =
        [
            new CreatureGaveUp { Tick = 0, CreatureId = creature.Id, Action = CreatureAction.HelpBuild, SettlementId = village.Id, Reason = GiveUpReason.NothingToBuild },
        ];

        var mine = new AlertFeed().Push(events, world, creature.GodId, 0);
        var theirs = new AlertFeed().Push(events, world, creature.GodId + 1, 0);

        Assert.Equal($"{creature.Name} gives up building in {village.Name}: there is nothing left to build.", Assert.Single(mine).Text);
        Assert.Empty(theirs);
    }

    /// <summary>Sets a Creature walking to a village or a point for an action.</summary>
    private static void Go(CreatureState creature, CreatureAction action, CreatureSituation situation, Domain.Settlements.SettlementState? village, GroundPosition? to = null)
    {
        var target = to ?? village!.Position;
        var activity = creature.Activity;
        (activity.Action, activity.Situation, activity.TargetSettlementId, activity.TargetCreatureId, activity.Target) = (action, situation, village?.Id, null, target);
        activity.Path.Clear();
        activity.Path.Add(target);
        (activity.Phase, activity.TicksLeft) = (CreaturePhase.Travelling, 0);
    }

    /// <summary>Runs the engine until the Creature has decided again, or for a number of ticks, collecting the events.</summary>
    private static List<WorldEvent> Run(WorldState world, CreatureState creature, int ticks)
    {
        var events = new List<WorldEvent>();
        for (var i = 0; i < ticks && creature.Activity.Phase != CreaturePhase.Deciding; i++)
        {
            Engine.Step(world, events);
        }

        return events;
    }
}
