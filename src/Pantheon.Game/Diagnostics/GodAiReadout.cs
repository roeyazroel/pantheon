using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Gods;

namespace Pantheon.Game.Diagnostics;

/// <summary>
/// The god AI debug view (spec §106): each AI god's personality, its current best action (its
/// goal for now) and its top candidate actions with their utilities and reasons.
/// </summary>
internal static class GodAiReadout
{
    private const int CandidatesShown = 3;

    /// <summary>Adds the lines of every AI god the snapshot has candidates for.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        foreach (var (godId, candidates) in snapshot.GodAiCandidates)
        {
            if (snapshot.World.Gods.Find(g => g.Id == godId) is not { } god || candidates.Count == 0)
            {
                continue;
            }

            lines.Add(Format("AI {0} ({1}): goal now: {2}", god.Name, DivineText.Personality(god.Personality), Describe(candidates[0], snapshot.World)));
            for (var i = 0; i < candidates.Count && i < CandidatesShown; i++)
            {
                var candidate = candidates[i];
                lines.Add(Format("  {0:0.00}  {1}  [{2}]", candidate.Utility, Describe(candidate, snapshot.World), candidate.Reason));
            }
        }
    }

    /// <summary>A candidate in a few words.</summary>
    private static string Describe(AiCandidate candidate, WorldState world)
    {
        var place = candidate.SettlementId is { } id ? world.Settlements.Find(s => s.Id == id)?.Name ?? "?" : "-";
        return candidate.Kind switch
        {
            AiActionKind.Wait => "wait and save power",
            AiActionKind.CastMiracle => Format("{0} on {1}", candidate.MiracleId, place),
            AiActionKind.SendCreature => Format("send its Creature to {0}", place),
        };
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
