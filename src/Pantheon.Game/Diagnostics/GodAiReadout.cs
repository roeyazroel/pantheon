using System.Collections.Generic;
using System.Linq;
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
            if (god.Persona is { } persona)
            {
                // Its mind (ADR-065): who wrote it, and how its temper leans from its personality's.
                lines.Add(Format("  persona: {0}, by {1}; temper: {2}", persona.Epithet, persona.Author, Minds.PersonaText.Numbers(persona.Temper)));
            }

            AppendPlan(lines, snapshot, god);
            for (var i = 0; i < candidates.Count && i < CandidatesShown; i++)
            {
                var candidate = candidates[i];
                lines.Add(Format("  {0:0.00}  {1}  [{2}]", candidate.Utility, Describe(candidate, snapshot.World), candidate.Reason));
            }

            foreach (var (diplomatId, moves) in snapshot.GodDiplomacyCandidates)
            {
                if (diplomatId != godId)
                {
                    continue;
                }

                for (var i = 0; i < moves.Count && i < CandidatesShown - 1; i++)
                {
                    var move = moves[i];
                    var other = snapshot.World.Gods.Find(g => g.Id == move.OtherGodId)?.Name ?? "?";
                    lines.Add(Format("  {0:+0.00;-0.00;0.00}  {1} {2} (acts above +0.25)  [{3}]", move.Utility, move.Kind, other, move.Reason));
                }
            }
        }
    }

    /// <summary>The god's plan (ADR-036): the goals it holds, the next plan, the best options for it, and the gods it knows.</summary>
    private static void AppendPlan(List<string> lines, DebugSnapshot snapshot, Domain.Gods.GodState god)
    {
        var world = snapshot.World;
        var held = god.Goals.ConvertAll(g => Format("{0} {1:0.00} for {2:0}s", Gods.GoalText.Goal(g, world, null), g.Weight, (world.Tick - g.SinceTick) / (float)SimulationTime.TicksPerSecond));
        lines.Add(Format(
            "  plans: {0}; next plan in {1:0}s; {2}",
            held.Count > 0 ? string.Join(", ", held) : "none",
            System.Math.Max(0L, god.NextPlanTick - world.Tick) / (float)SimulationTime.TicksPerSecond,
            Gods.GoalText.Known(god, world)));
        foreach (var (plannerId, options) in snapshot.GodGoalOptions)
        {
            if (plannerId == god.Id && options.Count > 0)
            {
                lines.Add("  could: " + string.Join("; ", options.Take(CandidatesShown).Select(o => Format("{0:0.00} {1}", o.Weight, o.Reason))));
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
            AiActionKind.SetCreatureOn => "set its Creature on an enemy's (" + candidate.Reason + ")",
            AiActionKind.SendCreature when candidate.SettlementId is null => "send its Creature to a holy site (" + candidate.Reason + ")",
            AiActionKind.SendCreature => Format("send its Creature to {0}", place),
        };
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
