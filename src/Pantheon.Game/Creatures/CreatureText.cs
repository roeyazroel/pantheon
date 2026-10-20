using System;
using System.Globalization;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Game.Creatures;

/// <summary>Player-facing wording for Creatures: what they are doing and why a command was refused. Pure and culture-invariant.</summary>
public static class CreatureText
{
    /// <summary>A path's name (spec §24, ADR-030).</summary>
    /// <param name="path">The path.</param>
    /// <returns>For example "Nature Keeper".</returns>
    public static string Path(CreatureSpecialisation path) => path switch
    {
        CreatureSpecialisation.Guardian => "Guardian",
        CreatureSpecialisation.Warrior => "Warrior",
        CreatureSpecialisation.Destroyer => "Destroyer",
        CreatureSpecialisation.Healer => "Healer",
        CreatureSpecialisation.NatureKeeper => "Nature Keeper",
        CreatureSpecialisation.Missionary => "Missionary",
        CreatureSpecialisation.Trickster => "Trickster",
        CreatureSpecialisation.Commander => "Commander",
    };

    /// <summary>What a path does, in a few words.</summary>
    /// <param name="path">The path.</param>
    /// <returns>For example "heals the people it helps".</returns>
    public static string PathEffect(CreatureSpecialisation path) => path switch
    {
        CreatureSpecialisation.Guardian => "shields your villages near it from disasters",
        CreatureSpecialisation.Warrior => "fights harder",
        CreatureSpecialisation.Destroyer => "kills more when it attacks",
        CreatureSpecialisation.Healer => "heals the people it helps",
        CreatureSpecialisation.NatureKeeper => "heals the land where it goes",
        CreatureSpecialisation.Missionary => "wins the godless to you",
        CreatureSpecialisation.Trickster => "sows doubt in your rivals",
        CreatureSpecialisation.Commander => "makes your villages near it harder to attack",
    };

    /// <summary>An action as a short verb phrase.</summary>
    /// <param name="action">The action.</param>
    /// <param name="place">The settlement it concerns, if any.</param>
    /// <returns>For example "feeding Elar".</returns>
    public static string Doing(CreatureAction action, string? place) => action switch
    {
        CreatureAction.Wander => "wandering",
        CreatureAction.Rest => "resting",
        CreatureAction.Forage => "foraging",
        CreatureAction.EatFromStores => At("eating the stores of", place),
        CreatureAction.Feed => At("feeding", place),
        CreatureAction.HelpBuild => At("building in", place),
        CreatureAction.Farm => At("farming the fields of", place),
        CreatureAction.GatherWood => At("fetching wood for", place),
        CreatureAction.Play => At("playing in", place),
        CreatureAction.Attack => At("attacking", place),
        CreatureAction.Ignore => At("walking away from", place),
        CreatureAction.GoTo => place is null ? "going where it was sent" : At("going to defend", place),
        CreatureAction.Fight => With("fighting", place),
        CreatureAction.Befriend => With("playing with", place),
        CreatureAction.Flee => With("running from", place),
    };

    /// <summary>Why a Creature gave up what it was doing (ADR-047).</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>For example "the village no longer needs it".</returns>
    public static string GaveUp(GiveUpReason reason) => reason switch
    {
        GiveUpReason.NoLongerInNeed => "the village no longer needs it",
        GiveUpReason.NothingToBuild => "there is nothing left to build",
        GiveUpReason.StoresEmpty => "the stores are empty",
        GiveUpReason.NowAnAlly => "the village follows an ally now",
        GiveUpReason.VillageGone => "the village is gone",
        GiveUpReason.Desperate => "it is too hungry or tired to go on",
        GiveUpReason.OtherGone => "the other Creature is gone",
    };

    /// <summary>The name of what an activity or memory concerns: its settlement, or the other Creature.</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlementId">The settlement, if any.</param>
    /// <param name="creatureId">The other Creature, if any.</param>
    /// <returns>The name, or <c>null</c> when it concerns neither or they are gone.</returns>
    public static string? Concerning(WorldState world, int? settlementId, int? creatureId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return settlementId is { } id ? world.Settlements.Find(s => s.Id == id)?.Name
            : creatureId is { } other ? world.Creatures.Find(c => c.Id == other)?.Name
            : null;
    }

    /// <summary>What the Creature is doing now, in a few words.</summary>
    /// <param name="creature">The Creature.</param>
    /// <param name="world">World, for settlement names.</param>
    /// <returns>For example "On the way: feeding Elar".</returns>
    public static string DescribeActivity(CreatureState creature, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(world);
        var activity = creature.Activity;
        var place = Concerning(world, activity.TargetSettlementId, activity.TargetCreatureId);
        var doing = Doing(activity.Action, place);
        if (Simulation.Creatures.CreatureModel.IsHeld(world, creature))
        {
            return "Held in its god's hand";
        }

        return activity.Phase switch
        {
            CreaturePhase.Deciding => "Thinking what to do",
            CreaturePhase.Travelling when activity.Action == CreatureAction.Fight => "Going after " + (place ?? "another Creature"),
            CreaturePhase.Travelling when activity.Action == CreatureAction.Befriend => "Going to play with " + (place ?? "another Creature"),
            CreaturePhase.Travelling when activity.Action == CreatureAction.Flee => Capitalise(doing),
            CreaturePhase.Travelling => "On the way: " + doing,
            CreaturePhase.Performing => Capitalise(doing),
            CreaturePhase.Banished => string.Create(
                CultureInfo.InvariantCulture,
                $"Banished: back in {activity.TicksLeft / (float)SimulationTime.TicksPerSecond:0} s"),
        };
    }

    /// <summary>Why a command was refused, or what it did.</summary>
    /// <param name="result">The authority's outcome.</param>
    /// <param name="kind">The command.</param>
    /// <param name="name">The Creature's name.</param>
    /// <returns>One line.</returns>
    public static string DescribeCommand(CreatureCommandResult result, CreatureCommandKind kind, string name) => result.Rejection switch
    {
        CreatureCommandRejection.None => kind switch
        {
            CreatureCommandKind.Reward => Format("You reward {0} for {1}: it likes that more now ({2:0.00})", name, Doing(result.JudgedAction ?? CreatureAction.Wander, null), result.Preference ?? 0f),
            CreatureCommandKind.Punish => Format("You punish {0} for {1}: it likes that less now ({2:0.00})", name, Doing(result.JudgedAction ?? CreatureAction.Wander, null), result.Preference ?? 0f),
            CreatureCommandKind.GoTo => Format("{0} heads where you pointed", name),
            CreatureCommandKind.Fight => Format("{0} goes after its rival, to fight", name),
            CreatureCommandKind.Work => result.JudgedAction == CreatureAction.GatherWood ? Format("{0} goes to fetch wood", name)
                : result.JudgedAction == CreatureAction.HelpBuild ? Format("{0} goes to help build", name)
                : Format("{0} goes to farm the fields", name),
        },
        CreatureCommandRejection.UnknownGod or CreatureCommandRejection.UnknownCreature => "You have no Creature",
        CreatureCommandRejection.NotAuthorized => "That Creature does not answer to you",
        CreatureCommandRejection.Banished => Format("{0} is banished and cannot hear you", name),
        CreatureCommandRejection.NothingToJudge => Format("{0} has done nothing lately to reward or punish", name),
        CreatureCommandRejection.OutsideWorld => "There is nowhere to go there",
        CreatureCommandRejection.Unreachable => Format("{0} cannot walk there", name),
        CreatureCommandRejection.NoRivalThere => "There is no rival Creature there to fight: click near one",
        CreatureCommandRejection.NoWorkThere => "There is no village or forest there to work at: click a village or a wood",
        CreatureCommandRejection.Sworn => "A treaty forbids your Creatures to fight each other",
    };

    /// <summary>"verb place", or the verb with "a settlement" when the place is unknown.</summary>
    private static string At(string verb, string? place) => $"{verb} {place ?? "a settlement"}";

    /// <summary>"verb name", or the verb with "another Creature" when the Creature is unknown.</summary>
    private static string With(string verb, string? name) => $"{verb} {name ?? "another Creature"}";

    /// <summary>Upper-cases the first letter.</summary>
    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
