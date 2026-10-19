using System;
using System.Collections.Generic;
using Pantheon.Domain.Roads;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Navigation;

namespace Pantheon.Game.Presentation;

/// <summary>
/// The Navigation overlay (spec §106's navmesh, ADR-044): the land the walkers' pathfinder may
/// cross, the roads that speed them, the land it must go round, and the ways every walker is on:
/// migrants, prophets, followed villagers and Creatures, in their god's colour. Pure.
/// </summary>
public static class NavigationOverlay
{
    /// <summary>The overlay's legend.</summary>
    public const string Legend = "Navigation: land walkers may cross in green, open roads in tan, land buried in rubble they must go round in red; the ways of migrants, prophets, followed villagers and Creatures in their god's colour";

    private static readonly RgbColor Walkable = RgbColor.ParseHex("#4caf50");
    private static readonly RgbColor Road = RgbColor.ParseHex("#d9b26f");
    private static readonly RgbColor Blocked = RgbColor.ParseHex("#d0342c");

    /// <summary>Paints the overlay.</summary>
    /// <param name="world">World state.</param>
    /// <param name="roads">Road rules, for which roads are open.</param>
    /// <param name="view">God colours and overlay opacity.</param>
    /// <param name="region">Paints one region.</param>
    public static void Paint(WorldState world, RoadRules roads, DivineViewSettings view, Action<int, RgbColor, float> region)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(roads);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(region);
        var grid = world.Regions;
        foreach (var cell in grid.Cells)
        {
            if (cell.Biome == Domain.Ecology.Biome.Sea)
            {
                continue;
            }

            var walkable = RegionPathfinder.IsWalkable(cell);
            region(cell.Id, walkable ? Walkable : Blocked, (walkable ? 0.3f : 0.7f) * view.OverlayOpacity);
        }

        foreach (var road in world.Roads)
        {
            if (Simulation.Roads.RoadModel.IsOpen(roads, road) && road.Path.Count > 0)
            {
                Way(grid, road.Path[0], road.Path, Road, 0.85f * view.OverlayOpacity, region);
            }
        }

        foreach (var (from, path, godId) in Walkers(world))
        {
            Way(grid, from, path, godId is { } god ? view.GodColor(god) : view.UnaffiliatedColor, 1f, region);
        }
    }

    /// <summary>Every walker on a way: where they are, the way left and their god.</summary>
    /// <param name="world">World state.</param>
    /// <returns>The walkers.</returns>
    public static IEnumerable<(GroundPosition From, IReadOnlyList<GroundPosition> Path, int? GodId)> Walkers(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var group in world.Migration.Groups)
        {
            yield return (group.Position, group.Path, Simulation.Migration.MigrationModel.MainGod(group.Beliefs));
        }

        foreach (var notable in world.Notables)
        {
            if (notable.Path.Count > 0)
            {
                yield return (notable.Position, notable.Path, notable.GodId);
            }
        }

        foreach (var villager in world.Villagers)
        {
            if (villager.Path.Count > 0)
            {
                yield return (villager.Position, villager.Path, villager.FollowerGodId);
            }
        }

        foreach (var creature in world.Creatures)
        {
            if (creature.Activity.Path.Count > 0)
            {
                yield return (creature.Position, creature.Activity.Path, creature.GodId);
            }
        }
    }

    /// <summary>Paints the regions a way crosses, from a point through its waypoints.</summary>
    private static void Way(RegionGrid grid, GroundPosition start, IReadOnlyList<GroundPosition> path, RgbColor color, float alpha, Action<int, RgbColor, float> region)
    {
        var from = start;
        foreach (var to in path)
        {
            var (dx, dz) = (to.X - from.X, to.Z - from.Z);
            var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt((dx * dx) + (dz * dz)) / (grid.RegionSize * 0.5f)));
            for (var i = 0; i <= steps; i++)
            {
                if (grid.TryGetRegionIdAt(from.X + (dx * i / steps), from.Z + (dz * i / steps), out var id))
                {
                    region(id, color, alpha);
                }
            }

            from = to;
        }
    }
}
