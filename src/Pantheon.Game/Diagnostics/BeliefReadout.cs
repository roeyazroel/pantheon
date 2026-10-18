using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Game.Diagnostics;

/// <summary>
/// Formats the debug overlay lines about gods and belief: each god's power, income, followers and
/// rank; a settlement's belief in each god it knows; the influence under the cursor; and the
/// one-line summary of a settlement that is not selected. Rates are per simulated second.
/// </summary>
internal static class BeliefReadout
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Adds one line per god.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void AppendGods(List<string> lines, DebugSnapshot snapshot)
    {
        var world = snapshot.World;
        var rules = snapshot.DivineRules;
        var stepsPerSecond = snapshot.TicksPerSecond / rules.StepIntervalTicks;
        foreach (var god in world.Gods)
        {
            var followers = GodStanding.CountFollowers(world, god.Id);
            var income = GodStanding.ComputePowerPerStep(rules, snapshot.SettlementRules, world, god.Id) * stepsPerSecond;
            var next = GodStanding.NextRank(rules, god.Rank);
            lines.Add(Format(
                "God {0}{1} ({2}): {3}   power {4:0} ({5:+0.00;-0.00;0.00}/s)   followers {6}{7}",
                god.Name,
                god.Id == snapshot.LocalGodId ? " [you]" : string.Empty,
                god.Controller,
                DivineText.Rank(god.Rank),
                god.DivinePower,
                income,
                followers,
                next is null ? string.Empty : Format(", {0} at {1}", DivineText.Rank(next.Rank), next.Followers)));
        }
    }

    /// <summary>Adds the influence of every god at the cursor.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="cursor">Ground point under the cursor.</param>
    public static void AppendInfluence(List<string> lines, DebugSnapshot snapshot, GroundPosition cursor)
    {
        if (snapshot.World.Gods.Count == 0)
        {
            return;
        }

        var parts = new List<string>();
        foreach (var god in snapshot.World.Gods)
        {
            var influence = InfluenceField.Build(snapshot.DivineRules, snapshot.World, god.Id).At(cursor);
            parts.Add(Format("{0} {1:0%}", god.Name, influence));
        }

        lines.Add("  influence " + string.Join("   ", parts));
    }

    /// <summary>Adds a settlement's belief in each god it knows of.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="settlement">Settlement.</param>
    public static void AppendBeliefs(List<string> lines, DebugSnapshot snapshot, SettlementState settlement)
    {
        var rules = snapshot.DivineRules;
        var stepsPerSecond = snapshot.TicksPerSecond / rules.StepIntervalTicks;
        var conditions = BeliefModel.AssessConditions(rules, settlement, SettlementEconomy.DroughtOf(snapshot.SettlementRules, snapshot.World.Regions, settlement));
        lines.Add(Format(
            "  belief: {0:0%} follow no god   hardship {1:0.00}   comfort {2:0.00}",
            1f - BeliefModel.TotalAdherence(settlement.Beliefs),
            conditions.Hardship,
            conditions.Comfort));
        foreach (var belief in settlement.Beliefs)
        {
            lines.Add(Format(
                "    {0}: {1} followers ({2:0%})  faith {3:0.00}  love {4:0.00}  fear {5:0.00}  doubt {6:0.00}  worship {7:0.0} -> {8:+0.00}/s",
                GodName(snapshot.World, belief.GodId),
                GodStanding.CountFollowers(settlement, belief.GodId),
                belief.Adherence,
                belief.Faith,
                belief.Love,
                belief.Fear,
                belief.Doubt,
                BeliefModel.ComputeWorshippers(snapshot.SettlementRules, settlement, belief),
                BeliefModel.ComputePower(rules, snapshot.SettlementRules, settlement, belief) * stepsPerSecond));
        }
    }

    /// <summary>One line for a settlement that is not selected: size, health, food and its main god.</summary>
    /// <param name="settlement">Settlement.</param>
    /// <param name="world">World, for god names.</param>
    /// <returns>The line.</returns>
    public static string Summarize(SettlementState settlement, WorldState world)
    {
        GodBelief? main = null;
        foreach (var belief in settlement.Beliefs)
        {
            if (main is null || belief.Adherence > main.Adherence)
            {
                main = belief;
            }
        }

        var faith = main is null
            ? "no god"
            : Format("{0} {1:0%} (faith {2:0.00})", GodName(world, main.GodId), main.Adherence, main.Faith);
        return Format(
            "  {0}: {1} people, health {2:0%}, food {3:0}, {4}",
            settlement.Name,
            settlement.Population,
            settlement.Health,
            settlement.Food,
            faith);
    }

    /// <summary>A god's name, or its id when it is missing.</summary>
    private static string GodName(WorldState world, int godId) => GodStanding.Find(world, godId)?.Name ?? Format("god {0}", godId);

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(Invariant, format, args);
}
