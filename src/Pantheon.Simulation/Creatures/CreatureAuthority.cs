using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Creatures;

/// <summary>Why the authority refuses a Creature command, or <see cref="None"/> when it is carried out.</summary>
public enum CreatureCommandRejection
{
    /// <summary>The command was carried out.</summary>
    None,

    /// <summary>No god has the requested id.</summary>
    UnknownGod,

    /// <summary>No Creature has the requested id.</summary>
    UnknownCreature,

    /// <summary>The issuer does not control the god, or the Creature is not the god's.</summary>
    NotAuthorized,

    /// <summary>The Creature is banished and cannot hear its god.</summary>
    Banished,

    /// <summary>The Creature has done nothing recently to reward or punish.</summary>
    NothingToJudge,

    /// <summary>The target lies outside the world.</summary>
    OutsideWorld,

    /// <summary>The Creature cannot reach the target over land.</summary>
    Unreachable,

    /// <summary>No rival Creature is near where the god pointed (ADR-062).</summary>
    NoRivalThere,

    /// <summary>A treaty between the gods forbids their Creatures to fight (ADR-025).</summary>
    Sworn,
}

/// <summary>The outcome of a Creature command.</summary>
/// <param name="Rejection">Why it was refused, or <see cref="CreatureCommandRejection.None"/>.</param>
/// <param name="JudgedAction">The action a reward or punishment applied to.</param>
/// <param name="JudgedSituation">The situation of that action.</param>
/// <param name="Preference">The Creature's preference for that action afterwards.</param>
public readonly record struct CreatureCommandResult(
    CreatureCommandRejection Rejection,
    CreatureAction? JudgedAction = null,
    CreatureSituation? JudgedSituation = null,
    float? Preference = null)
{
    /// <summary>Whether the command was carried out.</summary>
    public bool IsAccepted => Rejection == CreatureCommandRejection.None;
}

/// <summary>
/// The authority for a god's commands to its Creature (spec §73, ADR-010). A reward or a
/// punishment judges what the Creature is doing, or the last thing it did within
/// <c>FeedbackWindowSeconds</c>: it moves the learned preference for that action in that
/// situation (spec §25), the trait behind it (§26) and the Creature's loyalty. A punishment also
/// stops what it is doing. A go-to sends it to a place it can reach over land.
/// </summary>
public sealed class CreatureAuthority
{
    private readonly CreatureRules _rules;
    private readonly CreatureBrain _brain;
    private readonly Domain.Gods.DomainRules? _domains;

    /// <summary>Creates the authority.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="brain">The brain, for commanded walks.</param>
    /// <param name="domains">Domain rules, for the Knowledge domain's faster learning (ADR-021); <c>null</c> for none.</param>
    public CreatureAuthority(CreatureRules rules, CreatureBrain brain, Domain.Gods.DomainRules? domains = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(brain);
        _rules = rules;
        _brain = brain;
        _domains = domains;
    }

    /// <summary>Checks a command and carries it out when it is allowed.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The command.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <param name="events">Receives <see cref="CreatureChanged"/> for rewards and punishments.</param>
    /// <returns>The outcome; the world changed only if it is accepted.</returns>
    public CreatureCommandResult Execute(WorldState world, CreatureCommand command, int? issuerGodId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(events);
        if (GodStanding.Find(world, command.GodId) is null)
        {
            return new CreatureCommandResult(CreatureCommandRejection.UnknownGod);
        }

        if (world.Creatures.Find(c => c.Id == command.CreatureId) is not { } creature)
        {
            return new CreatureCommandResult(CreatureCommandRejection.UnknownCreature);
        }

        if ((issuerGodId is { } issuer && issuer != command.GodId) || creature.GodId != command.GodId)
        {
            return new CreatureCommandResult(CreatureCommandRejection.NotAuthorized);
        }

        if (creature.Activity.Phase == CreaturePhase.Banished)
        {
            return new CreatureCommandResult(CreatureCommandRejection.Banished);
        }

        return command.Kind switch
        {
            CreatureCommandKind.Reward => Judge(world, creature, CreatureFeedback.Rewarded, events),
            CreatureCommandKind.Punish => Judge(world, creature, CreatureFeedback.Punished, events),
            CreatureCommandKind.GoTo => GoTo(world, creature, command.Target),
            CreatureCommandKind.Fight => Fight(world, creature, command.Target),
        };
    }

    /// <summary>What a reward or punishment would apply to now, if anything.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>The situation and action, and the memory when it is a finished action; <c>null</c> when there is nothing to judge.</returns>
    public (CreatureSituation Situation, CreatureAction Action, CreatureMemory? Memory)? FindJudged(WorldState world, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        var activity = creature.Activity;
        if (activity.Phase is CreaturePhase.Performing or CreaturePhase.Travelling && activity.Action != CreatureAction.GoTo)
        {
            return (activity.Situation, activity.Action, null);
        }

        var window = (long)MathF.Round(_rules.FeedbackWindowSeconds * SimulationTime.TicksPerSecond);
        for (var i = creature.Memories.Count - 1; i >= 0; i--)
        {
            var memory = creature.Memories[i];
            if (world.Tick - memory.Tick > window)
            {
                break;
            }

            if (memory.Action != CreatureAction.GoTo)
            {
                return (memory.Situation, memory.Action, memory);
            }
        }

        return null;
    }

    /// <summary>Applies a reward or punishment to the judged action.</summary>
    private CreatureCommandResult Judge(WorldState world, CreatureState creature, CreatureFeedback feedback, ICollection<WorldEvent> events)
    {
        if (FindJudged(world, creature) is not { } judged)
        {
            return new CreatureCommandResult(CreatureCommandRejection.NothingToJudge);
        }

        var knowledge = _domains is not null && Domains.DomainModel.Holds(world, creature.GodId, Domain.Gods.GodDomain.Knowledge) ? _domains.Boons.KnowledgeLearning : 0f;
        var preference = CreatureModel.Learn(_rules, creature, judged.Situation, judged.Action, feedback, knowledge);
        CreatureTemperament.Judge(_rules, creature.Personality, judged.Action, feedback);
        var rewarded = feedback == CreatureFeedback.Rewarded;
        if (rewarded)
        {
            // A rewarded deed calls the Creature further down its paths (ADR-030).
            CreatureCalling.Deed(_rules.Callings, creature, judged.Action, _rules.Callings.RewardCalling);
        }

        creature.Loyalty = SimMath.Clamp01(creature.Loyalty + (rewarded ? _rules.LoyaltyPerReward : -_rules.LoyaltyPerPunishment));
        if (judged.Memory is { } memory)
        {
            memory.Feedback = feedback;
        }

        if (!rewarded && judged.Memory is null)
        {
            creature.Activity.Phase = CreaturePhase.Deciding;
            creature.Activity.Path.Clear();
            creature.Activity.TicksLeft = 0;
        }

        events.Add(new CreatureChanged
        {
            Tick = world.Tick,
            CreatureId = creature.Id,
            Change = rewarded ? CreatureChange.Rewarded : CreatureChange.Punished,
        });
        return new CreatureCommandResult(CreatureCommandRejection.None, judged.Action, judged.Situation, preference);
    }

    /// <summary>Sends the Creature to a place.</summary>
    /// <summary>
    /// Sets the Creature on the rival nearest a place (ADR-062): it hunts it down, re-planning as it
    /// moves, and falls on it when they meet, unless a treaty binds their gods.
    /// </summary>
    private CreatureCommandResult Fight(WorldState world, CreatureState creature, GroundPosition target)
    {
        CreatureState? rival = null;
        var nearest = _rules.Bonds.HuntReach;
        foreach (var other in world.Creatures)
        {
            var distance = MathF.Sqrt(((other.Position.X - target.X) * (other.Position.X - target.X)) + ((other.Position.Z - target.Z) * (other.Position.Z - target.Z)));
            if (other.GodId != creature.GodId && other.Activity.Phase != CreaturePhase.Banished && distance <= nearest)
            {
                (rival, nearest) = (other, distance);
            }
        }

        if (rival is null)
        {
            return new CreatureCommandResult(CreatureCommandRejection.NoRivalThere);
        }

        if (Politics.TreatyModel.SparesCreatures(world, creature.GodId, rival.GodId))
        {
            return new CreatureCommandResult(CreatureCommandRejection.Sworn);
        }

        return _brain.Hunt(world, creature, rival)
            ? new CreatureCommandResult(CreatureCommandRejection.None)
            : new CreatureCommandResult(CreatureCommandRejection.Unreachable);
    }

    private CreatureCommandResult GoTo(WorldState world, CreatureState creature, GroundPosition target)
    {
        if (!world.Regions.TryGetRegionIdAt(target.X, target.Z, out _))
        {
            return new CreatureCommandResult(CreatureCommandRejection.OutsideWorld);
        }

        return _brain.Command(world, creature, target)
            ? new CreatureCommandResult(CreatureCommandRejection.None)
            : new CreatureCommandResult(CreatureCommandRejection.Unreachable);
    }
}
