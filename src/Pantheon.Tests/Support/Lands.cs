using System;
using Pantheon.Domain.Land;
using Pantheon.Domain.World;
using Pantheon.Simulation.Land;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Tests.Support;

/// <summary>Finds where the land can be reshaped in a test world (ADR-045).</summary>
internal static class Lands
{
    /// <summary>The shipped land rules.</summary>
    public static LandRules Rules => TestContent.Shipped.DivineRules.Land;

    /// <summary>The first reshaping of a kind that can be made, searching outwards from a point.</summary>
    /// <param name="world">World state.</param>
    /// <param name="terrain">Its terrain.</param>
    /// <param name="kind">The reshaping.</param>
    /// <param name="near">Where to search from.</param>
    /// <returns>The plan, or <c>null</c>.</returns>
    public static TerrainReshape? Find(WorldState world, TerrainField terrain, ReshapeKind kind, GroundPosition near)
    {
        for (var radius = 50f; radius <= 2500f; radius += 50f)
        {
            for (var i = 0; i < 16; i++)
            {
                var at = new GroundPosition(near.X + (MathF.Cos(i * MathF.PI / 8f) * radius), near.Z + (MathF.Sin(i * MathF.PI / 8f) * radius));
                if (LandShaper.Plan(Rules, world, terrain, kind, at, world.Gods[0].Id) is { } plan)
                {
                    return plan;
                }
            }
        }

        return null;
    }
}
