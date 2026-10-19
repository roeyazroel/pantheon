using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// How an AI god's goals bend what it does (ADR-036): an action that serves a goal is worth up to
/// <c>Boost</c> times as much, by the goal's weight; it may send its Creature against the beast and smite it; it leans
/// towards an alliance with a god it means to befriend and war on one it means to weaken; and it
/// asks its villages to prepare for the hardship it foresees.
/// </summary>
public static class GoalFocus
{
    /// <summary>Raises the utility of every candidate that serves one of the god's goals, saying which.</summary>
    /// <param name="rules">Planning rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <param name="book">The miracles.</param>
    /// <param name="candidates">The candidates, changed in place.</param>
    public static void Apply(PlanningRules rules, WorldState world, GodState god, MiracleBook book, List<AiCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(candidates);
        if (god.Goals.Count == 0)
        {
            return;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.Kind == AiActionKind.Wait || candidate.SettlementId is not { } id || world.Settlements.Find(s => s.Id == id) is not { } settlement)
            {
                continue;
            }

            var kind = candidate.MiracleId is { } miracleId && book.Find(miracleId) is { } miracle && !Culture.CultureModel.Harms(miracle);
            foreach (var goal in god.Goals)
            {
                if (Serves(goal, god.Id, settlement, candidate.Kind, kind))
                {
                    candidates[i] = candidate with { Utility = candidate.Utility * Factor(rules, goal), Reason = candidate.Reason + ", goal: " + Name(goal, world) };
                    break;
                }
            }
        }
    }

    /// <summary>Adds smiting the awake beast with every smiting miracle the god may cast there, when it means to slay it; less where its own people would be struck too.</summary>
    /// <param name="rules">Planning rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <param name="book">The miracles.</param>
    /// <param name="caster">The miracle authority, for the checks.</param>
    /// <param name="cache">The gods' influence.</param>
    /// <param name="candidates">Receives the candidates.</param>
    public static void AddSmites(PlanningRules rules, WorldState world, GodState god, GodProfile profile, MiracleBook book, MiracleCaster caster, InfluenceCache cache, List<AiCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(candidates);
        if (god.Goals.Find(g => g.Kind == GodGoalKind.SlayBeast) is not { } goal || world.GreatEvents.Beast is not { Phase: BeastPhase.Awake or BeastPhase.Returning } beast)
        {
            return;
        }

        foreach (var miracle in book.Miracles)
        {
            if (!Smites(miracle))
            {
                continue;
            }

            var check = caster.Check(world, new CastMiracleRequest(god.Id, miracle.Id, beast.Position), god.Id, cache);
            if (check.IsAccepted)
            {
                // The bolt strikes the village the beast stands in too: a god spares its own people there by half.
                var mine = 0f;
                foreach (var reached in caster.FindReached(world, miracle, beast.Position))
                {
                    mine = Math.Max(mine, BeliefModel.Find(reached, god.Id)?.Adherence ?? 0f);
                }

                var value = goal.Weight * Factor(rules, goal) * (1f - (0.5f * mine));
                var utility = value * (1f - (profile.Thrift * check.Cost / Math.Max(1f, god.DivinePower)));
                candidates.Add(new AiCandidate(AiActionKind.CastMiracle, miracle.Id, null, beast.Position, value, check.Cost, utility, MiracleValuer.Format("smite {0}, cost {1:0}, goal: slay it", beast.Name, check.Cost)));
            }
        }
    }

    /// <summary>Adds sending a fit Creature against the awake beast, when the god means to slay it.</summary>
    /// <param name="rules">Planning rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <param name="creature">Its Creature, fit to be sent.</param>
    /// <param name="candidates">Receives the candidate.</param>
    public static void AddHunt(PlanningRules rules, WorldState world, GodState god, GodProfile profile, CreatureState creature, List<AiCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(candidates);
        if (god.Goals.Find(g => g.Kind == GodGoalKind.SlayBeast) is { } goal && world.GreatEvents.Beast is { Phase: BeastPhase.Awake or BeastPhase.Returning } beast)
        {
            var value = goal.Weight * Factor(rules, goal) * Math.Max(0.3f, profile.SendCreature);
            candidates.Add(new AiCandidate(AiActionKind.SendCreature, null, null, beast.Position, value, 0f, value, MiracleValuer.Format("hunt {0} {1:0.00}, goal: slay it", beast.Name, value)));
        }
    }

    /// <summary>What a goal adds to the utility of a diplomatic move towards another god: an alliance with one it means to befriend, war on one it means to weaken.</summary>
    /// <param name="rules">Planning rules.</param>
    /// <param name="god">The god.</param>
    /// <param name="otherGodId">The other god.</param>
    /// <param name="kind">The move.</param>
    /// <returns>The bias; 0 when no goal concerns it.</returns>
    public static float Diplomacy(PlanningRules rules, GodState god, int otherGodId, DiplomaticCommandKind kind)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(god);
        foreach (var goal in god.Goals)
        {
            if (goal.GodId != otherGodId)
            {
                continue;
            }

            var bias = rules.DiplomacyBias * goal.Weight;
            return (goal.Kind, kind) switch
            {
                (GodGoalKind.Befriend, DiplomaticCommandKind.OfferAlliance or DiplomaticCommandKind.RequestPeace) => bias,
                (GodGoalKind.Befriend, DiplomaticCommandKind.DeclareWar or DiplomaticCommandKind.BreakAlliance) => -bias,
                (GodGoalKind.Weaken, DiplomaticCommandKind.DeclareWar) => bias,
                (GodGoalKind.Weaken, DiplomaticCommandKind.OfferAlliance or DiplomaticCommandKind.RequestPeace) => -bias,
                _ => 0f,
            };
        }

        return 0f;
    }

    /// <summary>The priority a god preparing for hardship asks of a village it leads: health against the plague, food against the drought.</summary>
    /// <param name="rules">Planning rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <returns>The priority, or <c>null</c> when it is not preparing.</returns>
    public static SettlementPriority? Preparing(PlanningRules rules, WorldState world, GodState god)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(god);
        if (!god.Goals.Exists(g => g.Kind == GodGoalKind.Prepare))
        {
            return null;
        }

        var (plague, drought) = GodPlanner.Hardships(world, rules);
        return plague >= drought ? SettlementPriority.Health : SettlementPriority.Food;
    }

    /// <summary>A goal in a few words, for reasons: for example "convert Brightwater".</summary>
    /// <param name="goal">The goal.</param>
    /// <param name="world">World state, for names.</param>
    /// <returns>The words.</returns>
    public static string Name(GodGoal goal, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(world);
        var place = world.Settlements.Find(s => s.Id == goal.SettlementId)?.Name ?? "its home";
        var other = world.Gods.Find(g => g.Id == goal.GodId)?.Name ?? "a god";
        return goal.Kind switch
        {
            GodGoalKind.ProtectHome => "protect " + place,
            GodGoalKind.Convert => "convert " + place,
            GodGoalKind.Befriend => "befriend " + other,
            GodGoalKind.Weaken => "weaken " + other,
            GodGoalKind.Prepare => "prepare",
            GodGoalKind.SlayBeast => "slay the beast",
            GodGoalKind.Recover => "win back the doubters",
        };
    }

    /// <summary>How much more an action serving a goal is worth: <c>Boost</c> at the goal's full weight, less as it matters less.</summary>
    /// <param name="rules">Planning rules.</param>
    /// <param name="goal">The goal.</param>
    /// <returns>The factor, from 1.</returns>
    public static float Factor(PlanningRules rules, GodGoal goal)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(goal);
        return 1f + ((rules.Boost - 1f) * SimMath.Clamp01(goal.Weight));
    }

    /// <summary>Whether a miracle smites, and so can harm the beast.</summary>
    private static bool Smites(MiracleDefinition miracle)
    {
        foreach (var effect in miracle.Effects)
        {
            if (effect.Kind == MiracleEffectKind.Smite)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether an action at a village serves a goal.</summary>
    private static bool Serves(GodGoal goal, int godId, SettlementState settlement, AiActionKind action, bool kind) => goal.Kind switch
    {
        GodGoalKind.ProtectHome => goal.SettlementId == settlement.Id && (action == AiActionKind.SendCreature || kind),
        GodGoalKind.Convert => goal.SettlementId == settlement.Id,
        GodGoalKind.Weaken => action == AiActionKind.CastMiracle && goal.GodId is { } enemy && TreatyModel.Leader(settlement) == enemy,
        GodGoalKind.Prepare or GodGoalKind.Recover => action == AiActionKind.CastMiracle && kind && TreatyModel.Leader(settlement) == godId,
        GodGoalKind.Befriend or GodGoalKind.SlayBeast => false,
    };
}
