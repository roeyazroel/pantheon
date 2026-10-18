using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on the diplomacy of a deserialized world (part of <see cref="SaveIntegrity"/>).</summary>
internal static class DiplomacyIntegrity
{
    /// <summary>Records every inconsistency of relations, stances and proposals.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        var diplomacy = world.Diplomacy;
        var pairs = new HashSet<(int, int)>();
        foreach (var relation in diplomacy.Relations)
        {
            if (relation is null || !IsPair(relation.ObserverGodId, relation.SubjectGodId, godIds) || !pairs.Add((relation.ObserverGodId, relation.SubjectGodId)))
            {
                errors.Add("A relation is null, names a missing god, regards its own god or is repeated.");
                continue;
            }

            if (!IsShare(relation.Trust) || !IsShare(relation.Fear) || !IsShare(relation.Respect) || !IsShare(relation.Rivalry) || !IsShare(relation.Gratitude) || !IsShare(relation.Suspicion)
                || relation.Memories.Exists(m => m is null || !IsShare(m.Weight) || !Enum.IsDefined(m.Act)))
            {
                errors.Add($"The relation of god {relation.ObserverGodId} to god {relation.SubjectGodId} has a value outside 0..1 or a broken memory.");
            }
        }

        var stances = new HashSet<(int, int)>();
        foreach (var stance in diplomacy.Stances)
        {
            if (stance is null || stance.GodA >= stance.GodB || !IsPair(stance.GodA, stance.GodB, godIds) || !stances.Add((stance.GodA, stance.GodB))
                || !Enum.IsDefined(stance.Status) || stance.SinceTick < 0 || stance.SinceTick > world.Tick)
            {
                errors.Add("A stance is null, repeated, unordered, names a missing god or has an unknown status or tick.");
            }
        }

        foreach (var proposal in diplomacy.Proposals)
        {
            if (proposal is null || !IsPair(proposal.FromGodId, proposal.ToGodId, godIds) || !Enum.IsDefined(proposal.Kind))
            {
                errors.Add("A proposal is null, names a missing god or proposes to its own god.");
            }
        }
    }

    /// <summary>Two different gods that both exist.</summary>
    private static bool IsPair(int a, int b, HashSet<int> godIds) => a != b && godIds.Contains(a) && godIds.Contains(b);

    /// <summary>Whether a value lies in 0..1.</summary>
    private static bool IsShare(float value) => value is >= 0f and <= 1f;
}
