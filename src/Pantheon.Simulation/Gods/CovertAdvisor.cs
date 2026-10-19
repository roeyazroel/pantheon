using System;
using System.Collections.Generic;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Conflict;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// How an AI god wages war by other means (spec §45–46, §47–49, ADR-026): it speaks to its people's
/// holy wars (urging them on at war or by temperament, holding them back from allies and partners),
/// and a treacherous god stirs rebels or spreads propaganda among its rivals' followers.
/// </summary>
public sealed class CovertAdvisor
{
    // A proxy act must be worth this much.
    private const float Threshold = 0.2f;

    private readonly DiplomacyRules _rules;
    private readonly ConflictAuthority _authority;

    /// <summary>Creates the advisor.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="authority">The conflict authority it acts through.</param>
    public CovertAdvisor(DiplomacyRules rules, ConflictAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(authority);
        _rules = rules;
        _authority = authority;
    }

    /// <summary>Speaks to its people's holy wars, then acts through mortals if it is worth it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its profile.</param>
    /// <param name="events">Receives the events of what it does.</param>
    public void Act(WorldState world, GodState god, GodProfile profile, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Society.HolyWar is { } war && war.GodId == god.Id)
            {
                var stance = Stance(world, god.Id, war.EnemyGodId, profile);
                if (stance != war.Stance)
                {
                    _authority.Execute(world, new HolyWarCommand(god.Id, settlement.Id, stance), god.Id);
                }
            }
        }

        if (Best(world, god, profile) is { } act && act.Utility >= Threshold)
        {
            _authority.Execute(world, act.Command, god.Id, events);
        }
    }

    /// <summary>What a god says to its people's holy war on another god's people.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="enemyId">The god of the attacked.</param>
    /// <param name="profile">Its profile.</param>
    /// <returns>Its stance.</returns>
    public static HolyWarStance Stance(WorldState world, int godId, int enemyId, GodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(profile);
        if (DiplomacyModel.AreAtWar(world, godId, enemyId) || profile.Aggression >= 0.6f)
        {
            return HolyWarStance.Encourage;
        }

        return DiplomacyModel.AreAllied(world, godId, enemyId) || world.Diplomacy.Treaties.Exists(t => t.Binds(godId) && t.Binds(enemyId)) || profile.Cooperation >= 0.7f
            ? HolyWarStance.Discourage
            : HolyWarStance.Ignore;
    }

    /// <summary>The proxy act the god wants most, if it can afford one: inciting where its rival's followers already fear or resent it, propaganda elsewhere.</summary>
    private (ProxyCommand Command, float Utility)? Best(WorldState world, GodState god, GodProfile profile)
    {
        var conflict = _rules.Conflict;
        if (god.DivinePower < 5f * Math.Max(conflict.IncitePower, conflict.PropagandaPower))
        {
            return null;
        }

        (ProxyCommand, float)? best = null;
        foreach (var settlement in world.Settlements)
        {
            if (TreatyModel.Leader(settlement) is not { } rival || rival == god.Id || DiplomacyModel.AreAllied(world, god.Id, rival)
                || world.Diplomacy.Treaties.Exists(t => t.Binds(god.Id) && t.Binds(rival)) || BeliefModel.Find(settlement, rival) is not { } theirs)
            {
                continue;
            }

            var view = DiplomacyModel.FindRelation(world, god.Id, rival);
            var enmity = view is null ? 0f : view.Rivalry + (DiplomacyModel.Grudge(view) * profile.Grudge);
            var utility = (profile.Treachery * enmity) + (0.3f * profile.Opportunism) + (DiplomacyModel.AreAtWar(world, god.Id, rival) ? 0.3f : 0f) - 1.2f;
            var kind = theirs.Resentment + theirs.Fear >= 0.5f && settlement.Society.Stability < 0.7f ? ProxyKind.Incite : ProxyKind.Propaganda;
            var command = new ProxyCommand(god.Id, settlement.Id, rival, kind);
            if ((best is null || utility > best.Value.Item2) && _authority.Check(world, command, god.Id) == ConflictRejection.None)
            {
                best = (command, utility);
            }
        }

        return best;
    }
}
