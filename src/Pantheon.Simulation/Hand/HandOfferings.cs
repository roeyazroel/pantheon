using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Hand;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Culture;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Hand;

/// <summary>
/// Offerings (spec §55, ADR-039): a god's hand takes an offering of food, wood and stone from a
/// village it leads, whose people are glad their god accepts it, and gives it where it points: to
/// another village, which is grateful by its need, or to the god's Creature, which eats the food.
/// </summary>
internal sealed class HandOfferings
{
    private readonly HandRules _rules;
    private readonly DivineRules _divine;
    private readonly SettlementRules _settlements;

    /// <summary>Creates the offerings over the hand's rules.</summary>
    /// <param name="rules">Hand rules.</param>
    /// <param name="divine">Divine rules, for a village's needs.</param>
    /// <param name="settlements">Settlement rules, for a village's needs.</param>
    public HandOfferings(HandRules rules, DivineRules divine, SettlementRules settlements)
    {
        _rules = rules;
        _divine = divine;
        _settlements = settlements;
    }

    /// <summary>Takes an offering from the village the god leads nearest the point.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god, whose hand is empty.</param>
    /// <param name="target">Where the hand reaches.</param>
    /// <returns>Whether it was taken, or why not.</returns>
    public HandRejection Take(WorldState world, GodState god, GroundPosition target)
    {
        if (HandAuthority.Nearest(world, target, _rules.Reach) is not { } village || TreatyModel.Leader(village) != god.Id)
        {
            return HandRejection.NoOffering;
        }

        var food = Math.Min(_rules.OfferingFood, village.Food * _rules.OfferingShare);
        var wood = Math.Min(_rules.OfferingWood, village.Wood * _rules.OfferingShare);
        var stone = Math.Min(_rules.OfferingStone, village.Stone * _rules.OfferingShare);
        if (food < 1f && wood < 1f && stone < 1f)
        {
            return HandRejection.NoOffering;
        }

        (village.Food, village.Wood, village.Stone) = (village.Food - food, village.Wood - wood, village.Stone - stone);
        HandModel.Impress(village.Beliefs, god.Id, _rules.Offered, 1f);
        god.Held = new HeldObject { Kind = HeldKind.Offering, Amount = 1, SettlementId = village.Id, Food = food, Wood = wood, Stone = stone, Tick = world.Tick };
        return HandRejection.None;
    }

    /// <summary>
    /// Gives the offering to whichever is nearer the point within reach: a village, whose people are
    /// grateful by their need unless it is the village it came from; or the god's Creature, which
    /// eats the food while the wood and stone go back home.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god, whose hand holds the offering.</param>
    /// <param name="held">The offering.</param>
    /// <param name="target">Where the hand points.</param>
    /// <param name="events">Receives <see cref="OfferingGiven"/>.</param>
    /// <returns>Whether it was given, or why not; the hand keeps it when it was not.</returns>
    public HandRejection Give(WorldState world, GodState god, HeldObject held, GroundPosition target, ICollection<WorldEvent> events)
    {
        var village = HandAuthority.Nearest(world, target, _rules.Reach);
        var creature = Creature(world, god, target);
        if (creature is not null && (village is null || Distance(creature.Position, target) < Distance(village.Position, target)))
        {
            Feed(world, god, held, creature, events);
            return HandRejection.None;
        }

        if (village is null)
        {
            return HandRejection.NoPlaceForOffering;
        }

        god.Held = null;
        (village.Food, village.Wood, village.Stone) = (village.Food + held.Food, village.Wood + held.Wood, village.Stone + held.Stone);
        if (village.Id != held.SettlementId)
        {
            var need = BeliefModel.AssessConditions(_divine, village, Hazards.Of(_settlements, world, village)).FoodNeed;
            HandModel.Impress(village.Beliefs, god.Id, _rules.Gift, 0.5f + (0.5f * SimMath.Clamp01(need)));
            ConductModel.Note(world, god.Id, need > 0.3f ? ConductModel.Act.Help : ConductModel.Act.Gift, 0.5f);
        }

        events.Add(new OfferingGiven { Tick = world.Tick, GodId = god.Id, FromSettlementId = held.SettlementId, ToSettlementId = village.Id, Food = held.Food, Wood = held.Wood, Stone = held.Stone });
        return HandRejection.None;
    }

    /// <summary>The Creature eats the food and is the more loyal for it; the wood and stone go back to the village they came from.</summary>
    private void Feed(WorldState world, GodState god, HeldObject held, CreatureState creature, ICollection<WorldEvent> events)
    {
        god.Held = null;
        creature.Needs.Hunger = SimMath.Clamp01(creature.Needs.Hunger - (held.Food * _rules.CreatureFeedPerFood));
        creature.Loyalty = SimMath.Clamp01(creature.Loyalty + _rules.CreatureFeedLoyalty);
        if (world.Settlements.Find(s => s.Id == held.SettlementId) is { } home)
        {
            (home.Wood, home.Stone) = (home.Wood + held.Wood, home.Stone + held.Stone);
        }

        events.Add(new OfferingGiven { Tick = world.Tick, GodId = god.Id, FromSettlementId = held.SettlementId, ToCreatureId = creature.Id, Food = held.Food, Wood = 0f, Stone = 0f });
    }

    /// <summary>The god's own Creature within reach of the point, unless banished.</summary>
    private CreatureState? Creature(WorldState world, GodState god, GroundPosition target) =>
        god.CreatureId is { } id && world.Creatures.Find(c => c.Id == id) is { } creature
            && creature.Activity.Phase != CreaturePhase.Banished && Distance(creature.Position, target) <= _rules.Reach
            ? creature
            : null;

    /// <summary>Metres between two points.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
