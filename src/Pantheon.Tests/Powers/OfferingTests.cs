using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Hand;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Hand;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Hand;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Powers;

/// <summary>Offerings taken from a god's villages and given where it points (spec §55, ADR-039).</summary>
public sealed class OfferingTests
{
    private static readonly HandRules Rules = TestContent.Shipped.DivineRules.Hand;
    private static readonly HandAuthority Hand = new(Rules, DivineWorlds.Rules, SettlementWorlds.Rules);

    [Fact]
    public void AGodTakesAnOffering_FromAVillageItLeads_WhosePeopleAreGladItIsAccepted()
    {
        var world = DivineWorlds.Create(0f, 300f, 450f);
        var home = world.Settlements[0];
        var belief = DivineWorlds.Believe(home, adherence: 0.8f, faith: 0.5f);
        (home.Food, home.Wood, home.Stone) = (400f, 40f, 20f);

        Assert.Equal(HandRejection.None, Use(world, HandAction.TakeOffering, home.Position, []));

        var held = world.Gods[0].Held!;
        Assert.Equal((HeldKind.Offering, home.Id), (held.Kind, held.SettlementId));
        Assert.Equal((Rules.OfferingFood, 10f, 5f), (held.Food, held.Wood, held.Stone));
        Assert.Equal((400f - Rules.OfferingFood, 30f, 15f), (home.Food, home.Wood, home.Stone));
        Assert.True(belief.Faith > 0.5f);
        Assert.Equal(HandRejection.HandFull, Use(world, HandAction.TakeOffering, home.Position, []));
    }

    [Fact]
    public void NoOffering_FromAVillageAnotherGodLeads_OrWithNothingInStore()
    {
        var world = DivineWorlds.Create(0f, 300f, 450f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 500f));
        var (theirs, empty) = (world.Settlements[0], world.Settlements[1]);
        DivineWorlds.Believe(theirs, adherence: 0.3f, faith: 0.5f);
        DivineWorlds.Believe(theirs, adherence: 0.6f, faith: 0.5f, godId: 2);
        DivineWorlds.Believe(empty, adherence: 0.8f, faith: 0.5f);
        empty.Food = 0f;

        Assert.Equal(HandRejection.NoOffering, Use(world, HandAction.TakeOffering, theirs.Position, []));
        Assert.Equal(HandRejection.NoOffering, Use(world, HandAction.TakeOffering, empty.Position, []));
        Assert.Null(world.Gods[0].Held);
    }

    [Fact]
    public void AnOfferingToAHungryVillage_FillsItsStores_AndWinsMoreLoveThanOneToAVillageThatLacksNothing()
    {
        var gains = new List<float>();
        foreach (var food in new[] { 0f, 4000f })
        {
            var world = DivineWorlds.Create(0f, 300f, 450f);
            var (home, other) = (world.Settlements[0], world.Settlements[1]);
            DivineWorlds.Believe(home, adherence: 0.8f, faith: 0.5f);
            var belief = DivineWorlds.Believe(other, adherence: 0.4f, faith: 0.5f);
            other.Food = food;
            var events = new List<WorldEvent>();
            Use(world, HandAction.TakeOffering, home.Position, events);
            var love = belief.Love;

            Assert.Equal(HandRejection.None, Use(world, HandAction.SetDown, other.Position, events));

            gains.Add(belief.Love - love);
            Assert.Equal(food + Rules.OfferingFood, other.Food, 3);
            var given = Assert.Single(events.OfType<OfferingGiven>());
            Assert.Equal((home.Id, other.Id, Rules.OfferingFood), (given.FromSettlementId!.Value, given.ToSettlementId!.Value, given.Food));
            Assert.Null(world.Gods[0].Held);
            Assert.True(food == 0f ? world.Gods[0].Conduct.Help > 0f : world.Gods[0].Conduct.Gifts > 0f);
        }

        Assert.True(gains[0] > gains[1] && gains[1] > 0f);
    }

    [Fact]
    public void AnOfferingGivenBackHome_EarnsNoGratitude_AndOneLaidOnEmptyLandStaysInTheHand()
    {
        var world = DivineWorlds.Create(0f, 100f, 300f, 900f);
        var home = world.Settlements[0];
        var belief = DivineWorlds.Believe(home, adherence: 0.8f, faith: 0.5f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.6f, faith: 0.8f);
        DivineWorlds.Believe(world.Settlements[2], adherence: 0.6f, faith: 0.8f);
        Use(world, HandAction.TakeOffering, home.Position, []);
        Assert.True(InfluenceField.Build(DivineWorlds.Rules, world, DivineWorlds.GodId).At(new GroundPosition(600f, 512f)) >= Rules.MinInfluence);

        Assert.Equal(HandRejection.NoPlaceForOffering, Use(world, HandAction.SetDown, new GroundPosition(600f, 512f), []));
        Assert.Equal(HeldKind.Offering, world.Gods[0].Held!.Kind);
        var love = belief.Love;
        Assert.Equal(HandRejection.None, Use(world, HandAction.Throw, home.Position, []));

        Assert.Equal(400f, home.Food, 3);
        Assert.Equal(love, belief.Love);
        Assert.Equal(100, home.Population);
    }

    [Fact]
    public void TheCreature_EatsAnOfferingsFood_AndIsTheMoreLoyal_WhileTheWoodAndStoneGoHome()
    {
        var (world, creature) = CreatureWorlds.Create(0f, 600f);
        var home = world.Settlements[0];
        (home.Wood, home.Stone) = (40f, 20f);
        creature.Needs.Hunger = 0.9f;
        var loyalty = creature.Loyalty;
        var events = new List<WorldEvent>();
        Use(world, HandAction.TakeOffering, home.Position, events);
        creature.Position = new GroundPosition(150f, 512f);

        Assert.Equal(HandRejection.None, Use(world, HandAction.SetDown, creature.Position, events));

        Assert.Equal(0.9f - (Rules.OfferingFood * Rules.CreatureFeedPerFood), creature.Needs.Hunger, 3);
        Assert.Equal(loyalty + Rules.CreatureFeedLoyalty, creature.Loyalty, 3);
        Assert.Equal((40f, 20f), (home.Wood, home.Stone));
        Assert.Equal(creature.Id, Assert.Single(events.OfType<OfferingGiven>()).ToCreatureId);
    }

    [Fact]
    public void CtrlR_TakesAnOffering_AndTheHandSaysWhatItGaveAndToWhom()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var world = session.World;
        var me = session.LocalGodId!.Value;
        var home = world.Settlements.First(s => Pantheon.Simulation.Politics.TreatyModel.Leader(s) == me);
        var other = world.Settlements.Where(s => s.Id != home.Id).OrderBy(s => Math.Abs(s.Position.X - home.Position.X) + Math.Abs(s.Position.Z - home.Position.Z)).First();
        DivineWorlds.Believe(other, adherence: 0.3f, faith: 0.5f, godId: me);
        (home.Food, home.Wood, home.Stone) = (200f, 0f, 8f);

        var action = HandCommands.ActionFor(session, shift: false, ctrl: true);
        var taken = HandCommands.Use(session, action, home.Position);
        var given = HandCommands.Use(session, HandCommands.ActionFor(session, shift: false), other.Position);

        Assert.Equal(HandAction.TakeOffering, action);
        Assert.Equal($"You take an offering of 50 food and 2 stone from {home.Name}. R gives it to the village or your Creature you point at", taken);
        Assert.Equal($"You give {other.Name} 50 food and 2 stone", given);
        Assert.Equal("40 food, 8 wood and 3 stone", HandText.Goods(40f, 8f, 3f));
        Assert.Equal("Give the offering to a village or to your Creature", HandText.Describe(HandRejection.NoPlaceForOffering));
    }

    /// <summary>Uses the player's hand.</summary>
    private static HandRejection Use(WorldState world, HandAction action, GroundPosition target, List<WorldEvent> events) =>
        Hand.Execute(world, new HandCommand(DivineWorlds.GodId, action, target), events);
}
