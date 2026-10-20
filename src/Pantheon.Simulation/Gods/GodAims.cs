using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Gods;

/// <summary>An aim a god's own mind sets itself (ADR-072): a goal the planner knows, and the village or god it concerns.</summary>
/// <param name="Kind">The goal.</param>
/// <param name="SettlementId">The village it concerns, if any.</param>
/// <param name="GodId">The god it concerns, if any.</param>
public readonly record struct GodAim(GodGoalKind Kind, int? SettlementId, int? GodId);

/// <summary>
/// A god's own mind sets its aims (ADR-072): goals from the planner's own kinds (protect its home, convert
/// a village, befriend or weaken a god, prepare its people, slay the beast, win back its doubters), each
/// judged by the planner and refused when it is moot, as converting a village the god already leads. The
/// god's strategy and diplomacy then favour them as they favour the planner's own (<see cref="GoalFocus"/>),
/// at least at the mind's weight, and the planner leaves them be for a while before it plans again.
/// </summary>
public static class GodAims
{
    /// <summary>Sets the god's aims, those that are not moot, in place of its goals.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <param name="planner">The planner, which judges each aim.</param>
    /// <param name="profile">The god's own profile.</param>
    /// <param name="aims">The aims, the weightiest first; one of each kind is kept, at most the planner's <c>MaxGoals</c>.</param>
    /// <param name="minWeight">The least weight the mind's aims carry, so they count even where the planner would weigh them lightly (0..1).</param>
    /// <param name="holdTicks">How long the planner leaves the aims be before it plans again.</param>
    /// <param name="events">Receives <see cref="GodGoalTaken"/> for every new goal.</param>
    /// <returns>For each aim, whether it was taken.</returns>
    public static IReadOnlyList<bool> Set(WorldState world, GodState god, GodPlanner planner, GodProfile profile, IReadOnlyList<GodAim> aims, float minWeight, long holdTicks, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(aims);
        ArgumentNullException.ThrowIfNull(events);
        var taken = new bool[aims.Count];
        var chosen = new List<GodGoal>();
        for (var i = 0; i < aims.Count; i++)
        {
            var aim = aims[i];
            if (chosen.Count >= planner.Rules.MaxGoals || chosen.Exists(g => g.Kind == aim.Kind)
                || planner.Score(world, god, profile, aim.Kind, aim.SettlementId, aim.GodId) is not { } option)
            {
                continue;
            }

            var weight = Math.Clamp(Math.Max(option.Weight, minWeight), 0f, 1f);
            if (god.Goals.Find(g => g.Kind == aim.Kind && g.SettlementId == option.SettlementId && g.GodId == option.GodId) is { } held)
            {
                held.Weight = weight;
                chosen.Add(held);
            }
            else
            {
                chosen.Add(new GodGoal { Kind = aim.Kind, SettlementId = option.SettlementId, GodId = option.GodId, SinceTick = world.Tick, Weight = weight });
                events.Add(new GodGoalTaken { Tick = world.Tick, GodId = god.Id, Kind = aim.Kind, SettlementId = option.SettlementId, OtherGodId = option.GodId });
            }

            taken[i] = true;
        }

        if (chosen.Count > 0)
        {
            god.Goals.Clear();
            god.Goals.AddRange(chosen);
            god.NextPlanTick = world.Tick + Math.Max(1L, holdTicks);
        }

        return taken;
    }
}
