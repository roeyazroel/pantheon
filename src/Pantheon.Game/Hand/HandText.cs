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
            HandAction.PickUp => held.Kind == HeldKind.Villagers && held.FromDanger
                ? Format("You lift {0} out of danger. R sets them down, Shift+R throws", thing)
                : Format("You lift {0}. R sets {1} down, Shift+R throws", thing, held.Kind == HeldKind.Villagers ? "them" : "it"),
            HandAction.SetDown => held.Kind switch
            {
                HeldKind.Villagers => Format("You set {0} down", thing),
                HeldKind.Tree => "You plant the tree",
                HeldKind.Rock => "You set the rock down",
                HeldKind.Creature => Format("You set {0} down", thing),
            },
            HandAction.Throw => Format("You hurl {0}", thing),
        };
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
    };

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
