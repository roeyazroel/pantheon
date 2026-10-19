using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on the Creatures of a deserialized world (part of <see cref="SaveIntegrity"/>).</summary>
internal static class CreatureIntegrity
{
    // Matches the simulation's maximum level (spec §23's MVP cap).
    private const int MaxLevel = 10;

    /// <summary>Records every inconsistency of the Creatures and of the gods' references to them.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        var settlementIds = new HashSet<int>();
        foreach (var settlement in world.Settlements)
        {
            if (settlement is not null)
            {
                settlementIds.Add(settlement.Id);
            }
        }

        var creatures = new Dictionary<int, CreatureState>();
        foreach (var creature in world.Creatures)
        {
            if (creature is null)
            {
                errors.Add("Creature list contains null.");
                continue;
            }

            if (!creatures.TryAdd(creature.Id, creature))
            {
                errors.Add($"Creature id {creature.Id} is used more than once.");
            }

            if (!godIds.Contains(creature.GodId))
            {
                errors.Add($"Creature {creature.Id} belongs to missing god {creature.GodId}.");
            }

            ValidateCreature(creature, settlementIds, errors);
        }

        foreach (var creature in creatures.Values)
        {
            ValidateBonds(creature, creatures, errors);
        }

        foreach (var god in world.Gods)
        {
            if (god?.CreatureId is { } id && (!creatures.TryGetValue(id, out var creature) || creature.GodId != god.Id))
            {
                errors.Add($"God {god.Id} names creature {id}, which is missing or belongs to another god.");
            }
        }
    }

    /// <summary>Checks one Creature's numbers and references.</summary>
    private static void ValidateCreature(CreatureState creature, HashSet<int> settlementIds, List<string> errors)
    {
        var prefix = $"Creature {creature.Id}";
        if (creature.Level is < 1 or > MaxLevel || !(creature.Experience >= 0f))
        {
            errors.Add($"{prefix} has level {creature.Level} or experience {creature.Experience} out of range.");
        }

        if (!IsShare(creature.Health) || !IsShare(creature.Loyalty) || !(creature.Morality is >= -1f and <= 1f))
        {
            errors.Add($"{prefix} has health, loyalty or morality out of range.");
        }

        var needs = creature.Needs;
        var traits = creature.Personality;
        if (!IsShare(needs.Hunger) || !IsShare(needs.Fatigue) || !IsShare(needs.Boredom)
            || !IsShare(traits.Kindness) || !IsShare(traits.Aggression) || !IsShare(traits.Laziness) || !IsShare(traits.Curiosity))
        {
            errors.Add($"{prefix} has a need or personality trait outside 0..1.");
        }

        if (creature.Preferences.Exists(p => p is null || !IsShare(p.Weight)))
        {
            errors.Add($"{prefix} has a preference outside 0..1.");
        }

        if (!float.IsFinite(creature.Position.X) || !float.IsFinite(creature.Position.Z) || !float.IsFinite(creature.Heading))
        {
            errors.Add($"{prefix} has a position or heading that is not a number.");
        }

        if (!settlementIds.Contains(creature.HomeSettlementId)
            || (creature.Activity.TargetSettlementId is { } target && !settlementIds.Contains(target))
            || creature.Activity.TicksLeft < 0)
        {
            errors.Add($"{prefix} names a missing settlement or has a negative tick count.");
        }
    }

    /// <summary>Checks a Creature's combat numbers, its views of the others and its references to them (ADR-017).</summary>
    private static void ValidateBonds(CreatureState creature, Dictionary<int, CreatureState> creatures, List<string> errors)
    {
        var prefix = $"Creature {creature.Id}";
        if (!IsShare(creature.CombatSkill) || creature.Scars < 0 || creature.Relations is null)
        {
            errors.Add($"{prefix} has combat skill or scars out of range, or no relations.");
            return;
        }

        bool Known(int? id) => id is not { } other || (other != creature.Id && creatures.ContainsKey(other));
        if (!Known(creature.Activity.TargetCreatureId) || creature.Memories.Exists(m => m is null || !Known(m.OtherCreatureId)))
        {
            errors.Add($"{prefix} names a missing Creature, or itself, in its activity or memories.");
        }

        var seen = new HashSet<int>();
        foreach (var view in creature.Relations)
        {
            if (view is null || view.Encounters is null || !Known(view.OtherCreatureId) || !seen.Add(view.OtherCreatureId))
            {
                errors.Add($"{prefix} has a missing, duplicate or self relation.");
                continue;
            }

            if (!IsShare(view.Friendship) || !IsShare(view.Fear) || !IsShare(view.Rivalry) || !IsShare(view.Respect) || !IsShare(view.Trust)
                || view.LastMetTick < -1 || view.Encounters.Exists(e => e is null || !Enum.IsDefined(e.Kind)))
            {
                errors.Add($"{prefix} has a view of Creature {view.OtherCreatureId} out of range.");
            }
        }
    }

    /// <summary>Whether a value lies in 0..1.</summary>
    private static bool IsShare(float value) => value is >= 0f and <= 1f;
}
