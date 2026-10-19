using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Land;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.Land;

/// <summary>The terrain the Divine Acts reshape (ADR-045): set by whoever holds the world's terrain, and replaced by each act.</summary>
public sealed class LandStage
{
    /// <summary>The current terrain, or <c>null</c> where none is held and the land cannot be reshaped.</summary>
    public TerrainField? Terrain { get; set; }

    /// <summary>How many of the world's reshapings <see cref="Terrain"/> holds, for whoever keeps it in step.</summary>
    public int Shaped { get; set; }
}

/// <summary>The miracles that reshape the land (spec §14, ADR-045): Raise an Island, Split the Mountain and Turn the River.</summary>
public static class LandActs
{
    /// <summary>The reshaping a miracle effect makes, if any.</summary>
    /// <param name="kind">The effect.</param>
    /// <returns>The kind of reshaping, or <c>null</c>.</returns>
    public static ReshapeKind? KindOf(MiracleEffectKind kind) =>
        kind is MiracleEffectKind.Raise ? ReshapeKind.Island
        : kind is MiracleEffectKind.Split ? ReshapeKind.Gorge
        : kind is MiracleEffectKind.Divert ? ReshapeKind.Channel
        : null;

    /// <summary>Whether the act can be done where the god points.</summary>
    /// <param name="rules">Land rules.</param>
    /// <param name="stage">The terrain, if held.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="kind">The reshaping.</param>
    /// <param name="target">Where the god points.</param>
    /// <returns><c>true</c> when it can.</returns>
    public static bool CanAct(LandRules rules, LandStage? stage, WorldState world, int godId, ReshapeKind kind, GroundPosition target) =>
        stage?.Terrain is { } terrain && LandShaper.Plan(rules, world, terrain, kind, target, godId) is not null;

    /// <summary>Does the act: the land is reshaped, the terrain made again, and the world remembers it.</summary>
    /// <param name="rules">Land rules.</param>
    /// <param name="stage">The terrain, if held; it takes the new terrain.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="kind">The reshaping.</param>
    /// <param name="target">Where the god points.</param>
    /// <param name="events">Receives <see cref="DivineActDone"/>.</param>
    public static void Act(LandRules rules, LandStage? stage, WorldState world, int godId, ReshapeKind kind, GroundPosition target, ICollection<WorldEvent> events)
    {
        if (stage?.Terrain is not { } terrain || LandShaper.Plan(rules, world, terrain, kind, target, godId) is not { } plan)
        {
            return;
        }

        var river = kind == ReshapeKind.Channel ? RiverAt(terrain, plan.X, plan.Z) : null;
        stage.Terrain = LandShaper.Apply(world, terrain, plan);
        stage.Shaped = world.Land.Reshapes.Count;
        events.Add(new DivineActDone
        {
            Tick = world.Tick,
            GodId = godId,
            Act = kind switch
            {
                ReshapeKind.Island => DivineActKind.Raising,
                ReshapeKind.Gorge => DivineActKind.Splitting,
                ReshapeKind.Channel => DivineActKind.Diverting,
            },
            Position = target,
            Name = river,
        });
    }

    /// <summary>The name of the river through a point.</summary>
    private static string? RiverAt(TerrainField terrain, float x, float z)
    {
        foreach (var course in terrain.Hydrology.Rivers)
        {
            foreach (var point in course.Points)
            {
                if (point.X == x && point.Z == z)
                {
                    return course.Name;
                }
            }
        }

        return null;
    }
}
