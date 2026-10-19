using System;
using Pantheon.Domain.HolySites;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.HolySites;

/// <summary>What holy sites give their keepers (spec §51, ADR-021). Pure functions of the rules and the world.</summary>
public static class HolySiteModel
{
    /// <summary>The definition of a kind of site, if the rules have it.</summary>
    /// <param name="rules">Holy site rules.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The definition, or <c>null</c>.</returns>
    public static HolySiteDefinition? Find(HolySiteRules rules, HolySiteKind kind)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var definition in rules.Sites)
        {
            if (definition.Kind == kind)
            {
                return definition;
            }
        }

        return null;
    }

    /// <summary>The share of every miracle's cost a god pays thanks to the sites it keeps.</summary>
    /// <param name="rules">Holy site rules, or <c>null</c> where there are none.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The factor (0..1].</returns>
    public static float CostFactor(HolySiteRules? rules, WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var factor = 1f;
        if (rules is null)
        {
            return factor;
        }

        foreach (var site in world.HolySites)
        {
            if (site.KeeperGodId == godId && Find(rules, site.Kind) is { Boon: HolySiteBoon.CheaperMiracles } definition)
            {
                factor *= 1f - definition.BoonAmount;
            }
        }

        return factor;
    }

    /// <summary>How many sites a god keeps.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The count.</returns>
    public static int CountKept(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.HolySites.FindAll(site => site.KeeperGodId == godId).Count;
    }

    /// <summary>Distance between two ground positions in metres.</summary>
    /// <param name="a">One position.</param>
    /// <param name="b">The other.</param>
    /// <returns>The distance.</returns>
    public static float Distance(GroundPosition a, GroundPosition b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
