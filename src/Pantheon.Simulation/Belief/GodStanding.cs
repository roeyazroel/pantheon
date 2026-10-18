using System;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Belief;

/// <summary>A god's following, worship and rank, derived from the world (spec §11, §13).</summary>
public static class GodStanding
{
    /// <summary>People who follow a god across all settlements.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>Followers, rounded down.</returns>
    public static int CountFollowers(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var followers = 0.0;
        foreach (var settlement in world.Settlements)
        {
            if (BeliefModel.Find(settlement, godId) is { } belief)
            {
                followers += Math.Max(0, settlement.Population) * (double)SimMath.Clamp01(belief.Adherence);
            }
        }

        return (int)Math.Floor(followers + 1e-6);
    }

    /// <summary>People in one settlement who follow a god.</summary>
    /// <param name="settlement">Settlement state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>Followers, rounded down.</returns>
    public static int CountFollowers(SettlementState settlement, int godId)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return BeliefModel.Find(settlement, godId) is { } belief
            ? (int)Math.Floor((Math.Max(0, settlement.Population) * (double)SimMath.Clamp01(belief.Adherence)) + 1e-6)
            : 0;
    }

    /// <summary>Divine Power a god raises per belief step across all settlements.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="settlementRules">Settlement rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>Divine Power per step.</returns>
    public static float ComputePowerPerStep(DivineRules rules, SettlementRules settlementRules, WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var power = 0f;
        foreach (var settlement in world.Settlements)
        {
            if (BeliefModel.Find(settlement, godId) is { } belief)
            {
                power += BeliefModel.ComputePower(rules, settlementRules, settlement, belief);
            }
        }

        return power;
    }

    /// <summary>The rank a following earns: the highest threshold the followers reach.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="followers">Followers.</param>
    /// <returns>The rank.</returns>
    public static DivineRank RankFor(DivineRules rules, int followers)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var rank = DivineRank.MinorSpirit;
        foreach (var threshold in rules.Ranks)
        {
            if (followers >= threshold.Followers && threshold.Rank > rank)
            {
                rank = threshold.Rank;
            }
        }

        return rank;
    }

    /// <summary>Followers a god still needs for its next rank, if there is one.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="rank">Current rank.</param>
    /// <returns>The next rank and the followers it needs, or <c>null</c> at the top.</returns>
    public static RankThreshold? NextRank(DivineRules rules, DivineRank rank)
    {
        ArgumentNullException.ThrowIfNull(rules);
        RankThreshold? next = null;
        foreach (var threshold in rules.Ranks)
        {
            if (threshold.Rank > rank && (next is null || threshold.Rank < next.Rank))
            {
                next = threshold;
            }
        }

        return next;
    }

    /// <summary>The god with an id.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The god, or <c>null</c>.</returns>
    public static GodState? Find(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var god in world.Gods)
        {
            if (god.Id == godId)
            {
                return god;
            }
        }

        return null;
    }
}
