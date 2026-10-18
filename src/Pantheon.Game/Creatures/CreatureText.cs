using System;
using System.Globalization;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Game.Creatures;

/// <summary>Player-facing wording for Creatures: what they are doing and why a command was refused. Pure and culture-invariant.</summary>
public static class CreatureText
{
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
        CreatureAction.Play => At("playing in", place),
        CreatureAction.Attack => At("attacking", place),
        CreatureAction.Ignore => At("walking away from", place),
        CreatureAction.GoTo => "going where it was sent",
    };

    /// <summary>What the Creature is doing now, in a few words.</summary>
    /// <param name="creature">The Creature.</param>
    /// <param name="world">World, for settlement names.</param>
    /// <returns>For example "On the way: feeding Elar".</returns>
    public static string DescribeActivity(CreatureState creature, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(world);
        var activity = creature.Activity;
        var place = activity.TargetSettlementId is { } id ? world.Settlements.Find(s => s.Id == id)?.Name : null;
        var doing = Doing(activity.Action, place);
        return activity.Phase switch
        {
            CreaturePhase.Deciding => "Thinking what to do",
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
        },
        CreatureCommandRejection.UnknownGod or CreatureCommandRejection.UnknownCreature => "You have no Creature",
        CreatureCommandRejection.NotAuthorized => "That Creature does not answer to you",
        CreatureCommandRejection.Banished => Format("{0} is banished and cannot hear you", name),
        CreatureCommandRejection.NothingToJudge => Format("{0} has done nothing lately to reward or punish", name),
        CreatureCommandRejection.OutsideWorld => "There is nowhere to go there",
        CreatureCommandRejection.Unreachable => Format("{0} cannot walk there", name),
    };

    /// <summary>"verb place", or the verb with "a settlement" when the place is unknown.</summary>
    private static string At(string verb, string? place) => $"{verb} {place ?? "a settlement"}";

    /// <summary>Upper-cases the first letter.</summary>
    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
