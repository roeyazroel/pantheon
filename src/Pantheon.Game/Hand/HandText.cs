using System;
using System.Globalization;
using Pantheon.Domain.Hand;
using Pantheon.Domain.World;

namespace Pantheon.Game.Hand;

/// <summary>Words for the Divine Hand (ADR-030). Pure and culture-invariant.</summary>
public static class HandText
{
    /// <summary>What a held thing is called.</summary>
    /// <param name="world">World state, for names.</param>
    /// <param name="godId">The god whose hand holds it.</param>
    /// <param name="held">What it holds.</param>
    /// <returns>For example "10 people of Elar".</returns>
    public static string Thing(WorldState world, int godId, HeldObject held)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(held);
        return held.Kind switch
        {
            HeldKind.Villagers => Format("{0} {1} of {2}", held.Amount, held.Amount == 1 ? "person" : "people", world.Settlements.Find(s => s.Id == held.SettlementId)?.Name ?? "a lost village"),
            HeldKind.Rock => "a rock",
            HeldKind.Tree => "a tree",
            HeldKind.Creature => world.Creatures.Find(c => c.GodId == godId)?.Name ?? "your Creature",
            HeldKind.Offering => "an offering of " + Goods(held.Food, held.Wood, held.Stone) + " from " + (world.Settlements.Find(s => s.Id == held.SettlementId)?.Name ?? "a lost village"),
        };
    }

    /// <summary>Stores in words, leaving out what there is none of.</summary>
    /// <param name="food">Food.</param>
    /// <param name="wood">Wood.</param>
    /// <param name="stone">Stone.</param>
    /// <returns>For example "40 food, 8 wood and 3 stone".</returns>
    public static string Goods(float food, float wood, float stone)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (var (amount, name) in new[] { (food, "food"), (wood, "wood"), (stone, "stone") })
        {
            if (amount >= 0.5f)
            {
                parts.Add(Format("{0:0} {1}", amount, name));
            }
        }

        return parts.Count switch
        {
            0 => "nothing",
            1 => parts[0],
            _ => string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[^1],
        };
    }

    /// <summary>What the hand did.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="action">What it did.</param>
    /// <param name="held">What it held before (for setting down and throwing) or holds now (for picking up).</param>
    /// <returns>The status line.</returns>
    public static string Done(WorldState world, int godId, HandAction action, HeldObject? held)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (held is null)
        {
            return "Your hand is empty";
        }

        var thing = Thing(world, godId, held);
        return action switch
        {
            HandAction.TakeOffering => Format("You take {0}. R gives it to the village or your Creature you point at", thing),
            HandAction.PickUp => held.Kind == HeldKind.Villagers && held.FromDanger
                ? Format("You lift {0} out of danger. R sets them down, Shift+R throws", thing)
                : Format("You lift {0}. R sets {1} down, Shift+R throws", thing, held.Kind == HeldKind.Villagers ? "them" : "it"),
            HandAction.SetDown => held.Kind switch
            {
                HeldKind.Villagers => Format("You set {0} down", thing),
                HeldKind.Tree => "You plant the tree",
                HeldKind.Rock => "You set the rock down",
                HeldKind.Creature => Format("You set {0} down", thing),
                HeldKind.Offering => "You give the offering",
            },
            HandAction.Throw => Format("You hurl {0}", thing),
        };
    }

    /// <summary>Where an offering went (ADR-039).</summary>
    /// <param name="world">World state, for names.</param>
    /// <param name="given">The offering given.</param>
    /// <returns>For example "You give Elar 40 food and 8 wood" or "Bramble eats the offering's 40 food".</returns>
    public static string Given(WorldState world, Domain.Events.OfferingGiven given)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(given);
        if (given.ToCreatureId is { } id)
        {
            return Format("{0} eats the offering's {1:0} food", world.Creatures.Find(c => c.Id == id)?.Name ?? "Your Creature", given.Food);
        }

        var place = world.Settlements.Find(s => s.Id == given.ToSettlementId)?.Name ?? "a lost village";
        return given.ToSettlementId == given.FromSettlementId
            ? Format("You give {0} back its own offering", place)
            : Format("You give {0} {1}", place, Goods(given.Food, given.Wood, given.Stone));
    }

    /// <summary>Why the hand could not do it.</summary>
    /// <param name="rejection">The reason.</param>
    /// <returns>The status line.</returns>
    public static string Describe(HandRejection rejection) => rejection switch
    {
        HandRejection.None => "Done",
        HandRejection.UnknownGod => "You have no god",
        HandRejection.NoInfluence => "Your hand cannot reach where your influence does not",
        HandRejection.HandFull => "Your hand is full: R sets it down, Shift+R throws",
        HandRejection.HandEmpty => "Your hand is empty",
        HandRejection.NothingThere => "Nothing to lift there",
        HandRejection.OffTheWorld => "Point at the island",
        HandRejection.NoHand => "Gods have no hands in this world",
        HandRejection.NotOnLand => "Set your Creature down on land",
        HandRejection.CreatureFighting => "Your Creature is fighting and will not be lifted",
        HandRejection.NoOffering => "Ctrl+R takes an offering from a village of yours with something in store",
        HandRejection.NoPlaceForOffering => "Give the offering to a village or to your Creature",
    };

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
