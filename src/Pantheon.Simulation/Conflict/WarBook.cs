using System;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Conflict;

/// <summary>
/// The wars being fought (spec §44, ADR-026): each has a goal, and each side a score from what it
/// wins. Conversions, a defeated Creature and a taken holy site count for much; strikes on mortals
/// for little, so destruction is not the best road to victory. A side that meets its goal with a
/// lead may dictate peace.
/// </summary>
public static class WarBook
{
    /// <summary>The war between two gods, if one is recorded.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other.</param>
    /// <returns>The record, or <c>null</c>.</returns>
    public static WarRecord? Find(WorldState world, int a, int b)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Wars.Find(w => w.Involves(a) && w.Involves(b) && a != b);
    }

    /// <summary>Records a war's start, unless one is recorded between the two already.</summary>
    /// <param name="world">World state.</param>
    /// <param name="aggressor">The god that declared it.</param>
    /// <param name="defender">The god it was declared on.</param>
    /// <param name="aim">Its aim; conquest when <c>null</c>, or when it names a site the defender does not keep.</param>
    /// <returns>The record.</returns>
    public static WarRecord Start(WorldState world, int aggressor, int defender, WarAim? aim)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (Find(world, aggressor, defender) is { } existing)
        {
            return existing;
        }

        var goal = aim?.Goal ?? WarGoal.Conquest;
        var site = goal == WarGoal.HolySite ? aim?.SiteId ?? world.HolySites.Find(s => s.KeeperGodId == defender)?.Id : null;
        if (goal == WarGoal.HolySite && (site is null || !world.HolySites.Exists(s => s.Id == site)))
        {
            goal = WarGoal.Conquest;
            site = null;
        }

        var record = new WarRecord
        {
            AggressorGodId = aggressor, DefenderGodId = defender, Goal = goal, SiteId = site, StartedTick = world.Tick,
            AggressorScore = 0f, DefenderScore = 0f, GoalAnnounced = false,
        };
        world.Diplomacy.Wars.Add(record);
        return record;
    }

    /// <summary>Scores an act one god did to another, if they are at war.</summary>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="actorGodId">The god that did it.</param>
    /// <param name="victimGodId">The god it was done to.</param>
    /// <param name="act">The act.</param>
    public static void Score(ConflictRules rules, WorldState world, int actorGodId, int victimGodId, DiplomaticAct act)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (Find(world, actorGodId, victimGodId) is not { } war || !DiplomacyModel.AreAtWar(world, actorGodId, victimGodId))
        {
            return;
        }

        var points = act switch
        {
            DiplomaticAct.Converted => rules.ConversionScore,
            DiplomaticAct.Harmed or DiplomaticAct.CreatureAttacked => rules.HarmScore,
            DiplomaticAct.CreatureDefeated => rules.CreatureScore,
            DiplomaticAct.TookHolySite => rules.SiteScore,
            DiplomaticAct.Sheltered => rules.RefugeeScore,
            DiplomaticAct.HolyWar => rules.RaidScore,
            DiplomaticAct.Helped or DiplomaticAct.Courted or DiplomaticAct.Betrayed or DiplomaticAct.DeclaredWar or DiplomaticAct.Allied or DiplomaticAct.MadePeace
                or DiplomaticAct.WrongedAlly or DiplomaticAct.TurnedAway or DiplomaticAct.Rescued or DiplomaticAct.BrokeTreaty or DiplomaticAct.BrokePromise
                or DiplomaticAct.VotedAgainst or DiplomaticAct.DefiedCouncil or DiplomaticAct.KeptWord or DiplomaticAct.SignedTreaty or DiplomaticAct.PlottedAgainst
                or DiplomaticAct.CovertAct or DiplomaticAct.Dictated or DiplomaticAct.Trespassed or DiplomaticAct.Visited => 0f,
        };
        war.Add(actorGodId, points);
    }

    /// <summary>
    /// Whether a side may dictate peace: the aggressor when its goal is met and it leads (a holy
    /// site it now keeps; a defeated Creature; or enough score), the defender when it has repelled
    /// the attack with a clear lead.
    /// </summary>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="war">The war.</param>
    /// <param name="godId">The side.</param>
    /// <returns><c>true</c> when it may.</returns>
    public static bool Achieved(ConflictRules rules, WorldState world, WarRecord war, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(war);
        var mine = war.ScoreOf(godId);
        var theirs = war.ScoreOf(war.AggressorGodId == godId ? war.DefenderGodId : war.AggressorGodId);
        var lead = mine - theirs;
        if (godId != war.AggressorGodId)
        {
            return godId == war.DefenderGodId && mine >= rules.GoalScore && lead >= rules.GoalScore / 2f;
        }

        return war.Goal switch
        {
            WarGoal.HolySite => world.HolySites.Find(s => s.Id == war.SiteId)?.KeeperGodId == godId && lead >= 0f,
            WarGoal.Humiliation => DiplomacyModel.FindRelation(world, war.DefenderGodId, godId) is { } view
                && view.Memories.Exists(m => m.Act == DiplomaticAct.CreatureDefeated && m.Tick >= war.StartedTick) && lead >= 0f,
            WarGoal.Conquest or WarGoal.Tribute => mine >= rules.GoalScore && lead >= rules.GoalScore / 2f,
        };
    }

    /// <summary>Forgets the wars whose gods are no longer at war.</summary>
    /// <param name="world">World state.</param>
    public static void Prune(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.Diplomacy.Wars.RemoveAll(w => !DiplomacyModel.AreAtWar(world, w.AggressorGodId, w.DefenderGodId));
    }
}
