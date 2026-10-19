using System;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>
/// A faith reforming itself (spec §52's religious reformation, ADR-034): when a god's followers doubt
/// it and their villages have learned enough, its faith reforms. Most of the doubt goes and faith
/// grows, but some of the doubters leave the god rather than reform.
/// </summary>
public static class ReformationModel
{
    /// <summary>A god's followers' mean doubt and their villages' mean learning, weighted by followers, and how many villages follow it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The mean doubt and learning, and the villages.</returns>
    public static (float Doubt, float Learning, int Villages) Measure(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var (weight, doubt, learning, villages) = (0f, 0f, 0f, 0);
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population <= 0 || BeliefModel.Find(settlement, godId) is not { Adherence: >= 0.05f } belief)
            {
                continue;
            }

            var followers = settlement.Population * belief.Adherence;
            (weight, doubt, learning, villages) = (weight + followers, doubt + (followers * belief.Doubt), learning + (followers * settlement.Society.Education), villages + 1);
        }

        return weight <= 0f ? (0f, 0f, villages) : (doubt / weight, learning / weight, villages);
    }

    /// <summary>Whether a god's faith is ripe to reform now.</summary>
    /// <param name="rules">Reformation rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns><c>true</c> when it reforms.</returns>
    public static bool Ripe(ReformationRules rules, WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        if (world.GreatEvents.LastReformations.TryGetValue(godId, out var last) && world.Tick - last < (long)(rules.CooldownSeconds * SimulationTime.TicksPerSecond))
        {
            return false;
        }

        var (doubt, learning, villages) = Measure(world, godId);
        return villages >= rules.MinVillages && doubt >= rules.Doubt && learning >= rules.Learning;
    }

    /// <summary>Reforms a god's faith in every village that follows it.</summary>
    /// <param name="rules">Reformation rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The followers who left, in people.</returns>
    public static int Reform(ReformationRules rules, WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var left = 0f;
        foreach (var settlement in world.Settlements)
        {
            if (BeliefModel.Find(settlement, godId) is not { } belief)
            {
                continue;
            }

            var leaving = SimMath.Clamp01(rules.Leaving * belief.Doubt) * belief.Adherence;
            belief.Adherence -= leaving;
            belief.Doubt *= SimMath.Clamp01(rules.DoubtKept);
            belief.Faith = BeliefModel.ApplyChange(belief.Faith, rules.Faith);
            left += leaving * Math.Max(0, settlement.Population);
        }

        world.GreatEvents.LastReformations[godId] = world.Tick;
        return (int)MathF.Round(left);
    }
}
