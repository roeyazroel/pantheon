using System;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Conflict;

/// <summary>How an AI god chooses what to fight for, and whether to accept a victor's terms (spec §44, §47–49, ADR-026).</summary>
public static class WarAims
{
    /// <summary>
    /// The aim an AI god declares war with: a holy site the enemy keeps, for a god that cares for
    /// sites; humbling the enemy's Creature, for a warlike god with a strong Creature; tribute from a
    /// much weaker enemy; otherwise conquest of its villages.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god declaring war.</param>
    /// <param name="enemyId">Its enemy.</param>
    /// <param name="profile">Its profile.</param>
    /// <returns>The aim.</returns>
    public static WarAim For(WorldState world, GodState god, int enemyId, GodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.HolySites >= 0.5f && world.HolySites.Find(s => s.KeeperGodId == enemyId) is { } site)
        {
            return new WarAim(WarGoal.HolySite, site.Id);
        }

        var mine = world.Creatures.Find(c => c.GodId == god.Id);
        var theirs = world.Creatures.Find(c => c.GodId == enemyId);
        if (profile.Aggression >= 0.7f && mine is not null && theirs is not null && mine.Level > theirs.Level)
        {
            return new WarAim(WarGoal.Humiliation);
        }

        var ours = GodStanding.CountFollowers(world, god.Id);
        var enemy = GodStanding.CountFollowers(world, enemyId);
        return new WarAim(ours > 2 * Math.Max(1, enemy) ? WarGoal.Tribute : WarGoal.Conquest);
    }

    /// <summary>How much a god wants to accept its enemy's terms: positive to accept. The war's score, its weariness and fear argue for; grudges against.</summary>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god asked to surrender.</param>
    /// <param name="victorId">The god demanding it.</param>
    /// <param name="view">How it sees the victor.</param>
    /// <param name="profile">Its profile.</param>
    /// <returns>The utility of accepting.</returns>
    public static float SurrenderUtility(ConflictRules rules, WorldState world, int godId, int victorId, GodRelation view, GodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(profile);
        if (WarBook.Find(world, godId, victorId) is not { } war)
        {
            return -1f;
        }

        var behind = (war.ScoreOf(victorId) - war.ScoreOf(godId)) / Math.Max(1f, rules.GoalScore);
        var weariness = Math.Min(1f, (world.Tick - war.StartedTick) / (float)SimulationTime.TicksPerSecond / 600f);
        return behind + weariness + view.Fear + (0.3f * profile.Cooperation) - (0.3f * DiplomacyModel.Grudge(view) * profile.Grudge) - 0.6f;
    }
}
