using System;
using System.Collections.Generic;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Disasters;

/// <summary>
/// The hazards of ADR-029 as pure functions and state changes: where the volcano stands and which
/// way its lava runs, the coast a wave comes ashore on, and starting a tornado, a swarm, blight, a
/// landslide or a meteor's impact. <see cref="HazardSystem"/> runs them and strikes the villages they
/// reach; the debug commands start them directly.
/// </summary>
public static class HazardModel
{
    /// <summary>The island's highest land region, if it rises high enough to be a volcano.</summary>
    /// <param name="rules">Volcano rules.</param>
    /// <param name="grid">Region grid.</param>
    /// <returns>Its region, or <c>null</c>.</returns>
    public static int? FindVolcano(VolcanoRules rules, RegionGrid grid)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(grid);
        RegionState? highest = null;
        foreach (var region in grid.Cells)
        {
            if (region.Biome != Biome.Sea && (highest is null || region.Elevation > highest.Elevation))
            {
                highest = region;
            }
        }

        return highest is not null && highest.Elevation >= rules.MinElevation ? highest.Id : null;
    }

    /// <summary>The lowest of a region's eight neighbours, if any lies below it.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="regionId">The region.</param>
    /// <returns>The neighbour and how far below it lies, or <c>null</c>.</returns>
    public static (int RegionId, float Drop)? Downhill(RegionGrid grid, int regionId)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var here = grid.Cells[regionId];
        var (column, row) = grid.GetCoordinates(regionId);
        (int, float)? lowest = null;
        for (var r = Math.Max(0, row - 1); r <= Math.Min(grid.Rows - 1, row + 1); r++)
        {
            for (var c = Math.Max(0, column - 1); c <= Math.Min(grid.Columns - 1, column + 1); c++)
            {
                var id = (r * grid.Columns) + c;
                var drop = here.Elevation - grid.Cells[id].Elevation;
                if (id != regionId && drop > 0f && (lowest is null || drop > lowest.Value.Item2))
                {
                    lowest = (id, drop);
                }
            }
        }

        return lowest;
    }

    /// <summary>The regions lava runs through from a crater: down the steepest slope, at most a number of them, stopping at the sea.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="craterId">The crater's region.</param>
    /// <param name="length">How many regions at most, the crater included.</param>
    /// <returns>The regions, crater first.</returns>
    public static List<int> LavaPath(RegionGrid grid, int craterId, int length)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var path = new List<int> { craterId };
        var current = craterId;
        while (path.Count < length && Downhill(grid, current) is { } next && grid.Cells[next.RegionId].Biome != Biome.Sea)
        {
            current = next.RegionId;
            path.Add(current);
        }

        return path;
    }

    /// <summary>The middle of the sea region nearest a point, if one lies within a distance.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="point">The point.</param>
    /// <param name="distance">How far to look, in metres.</param>
    /// <returns>The sea's position, or <c>null</c>.</returns>
    public static GroundPosition? NearestSea(RegionGrid grid, GroundPosition point, float distance)
    {
        ArgumentNullException.ThrowIfNull(grid);
        GroundPosition? nearest = null;
        var best = distance * distance;
        foreach (var region in grid.Cells)
        {
            if (region.Biome != Biome.Sea)
            {
                continue;
            }

            var centre = grid.GetRegionCenter(region.Id);
            var squared = ((centre.X - point.X) * (centre.X - point.X)) + ((centre.Z - point.Z) * (centre.Z - point.Z));
            if (squared <= best)
            {
                (nearest, best) = (centre, squared);
            }
        }

        return nearest;
    }

    /// <summary>Raises a wave from an earthquake near the coast: it floods the low land around the shore.</summary>
    /// <param name="rules">Tsunami rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="epicentre">The earthquake's epicentre.</param>
    /// <param name="magnitude">Its magnitude.</param>
    /// <returns>Where it came ashore, how far it reached and the deepest flooded region; <c>null</c> when the quake is too weak, too far inland or floods nothing.</returns>
    public static (GroundPosition Shore, float Radius, int RegionId)? RaiseTsunami(TsunamiRules rules, WorldState world, GroundPosition epicentre, float magnitude)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        if (magnitude < rules.MinMagnitude || NearestSea(world.Regions, epicentre, rules.CoastDistance) is not { } shore)
        {
            return null;
        }

        var strength = magnitude - rules.MinMagnitude + 1f;
        var radius = rules.RadiusPerMagnitude * strength;
        return Water.WaterModel.Inundate(world, shore, radius, rules.HeightPerMagnitude * strength) is { } deepest ? (shore, radius, deepest) : null;
    }

    /// <summary>A slope gives way: it and the land below it are buried in rubble and harmed, and it leaves its mark.</summary>
    /// <param name="rules">Landslide rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="regionId">The region that slides.</param>
    /// <param name="belowId">The region below it.</param>
    public static void Slide(LandslideRules rules, WorldState world, int regionId, int belowId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var grid = world.Regions;
        var slid = grid.Cells[regionId];
        var buried = grid.Cells[belowId];
        slid.Rubble = Math.Max(slid.Rubble, 0.5f);
        buried.Rubble = 1f;
        slid.Damage = SimMath.Clamp01(slid.Damage + rules.Damage);
        buried.Damage = SimMath.Clamp01(buried.Damage + rules.Damage);
        AddMark(world, TerrainMarkKind.Slide, grid.GetRegionCenter(belowId), grid.RegionSize);
    }

    /// <summary>A meteor strikes: the land around it is harmed and scarred, the impact sets it alight and leaves a crater.</summary>
    /// <param name="rules">Disaster rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="point">Where it strikes.</param>
    /// <returns>The earthquake it sets off.</returns>
    public static Earthquake Impact(DisasterRules rules, WorldState world, GroundPosition point)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var meteor = rules.Hazards.Meteor;
        foreach (var region in world.Regions.Cells)
        {
            var reach = Falloff(world.Regions.GetRegionCenter(region.Id), point, meteor.Radius);
            if (reach > 0f && region.Biome != Biome.Sea)
            {
                region.Damage = Math.Max(region.Damage, reach);
                region.Scar = Math.Min(region.Damage, region.Scar + (meteor.Scar * reach));
                region.Wildlife *= 1f - reach;
            }
        }

        if (world.Regions.TryGetRegionIdAt(point.X, point.Z, out var struck))
        {
            DisasterModel.Ignite(rules, world.Regions.Cells[struck], 1f);
        }

        AddMark(world, TerrainMarkKind.Crater, point, meteor.Radius * 0.5f);
        return DisasterModel.StartQuake(rules, world.Disasters, point, meteor.Magnitude, world.Tick, godId: null);
    }

    /// <summary>A tornado touches down.</summary>
    /// <param name="rules">Tornado rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="point">Where.</param>
    /// <param name="heading">Its heading in radians.</param>
    /// <param name="seconds">How long it lasts.</param>
    /// <returns>The tornado.</returns>
    public static Tornado SpawnTornado(TornadoRules rules, WorldState world, GroundPosition point, float heading, float seconds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var tornado = new Tornado
        {
            Id = world.Disasters.NextHazardId++,
            X = point.X,
            Z = point.Z,
            VelocityX = MathF.Cos(heading) * rules.Speed,
            VelocityZ = MathF.Sin(heading) * rules.Speed,
            EndsAtTick = world.Tick + Ticks(seconds),
        };
        world.Disasters.Tornadoes.Add(tornado);
        return tornado;
    }

    /// <summary>A swarm of locusts rises.</summary>
    /// <param name="world">World state.</param>
    /// <param name="point">Where.</param>
    /// <param name="seconds">How long it lives.</param>
    /// <returns>The swarm.</returns>
    public static LocustSwarm SpawnSwarm(WorldState world, GroundPosition point, float seconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        var swarm = new LocustSwarm { Id = world.Disasters.NextHazardId++, X = point.X, Z = point.Z, Size = 1f, EndsAtTick = world.Tick + Ticks(seconds) };
        world.Disasters.Swarms.Add(swarm);
        return swarm;
    }

    /// <summary>The volcano wakes and begins to rumble now; it is found first if it was not yet.</summary>
    /// <param name="rules">Volcano rules.</param>
    /// <param name="world">World state.</param>
    /// <returns>The volcano, or <c>null</c> on an island without one.</returns>
    public static Volcano? Wake(VolcanoRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        if (Find(rules, world) is not { } volcano)
        {
            return null;
        }

        volcano.Phase = VolcanoPhase.Rumbling;
        volcano.PhaseEndsTick = world.Tick + Ticks(rules.RumbleSeconds);
        volcano.NextTremorTick = world.Tick;
        return volcano;
    }

    /// <summary>The island's volcano, found and recorded the first time it is asked for.</summary>
    /// <param name="rules">Volcano rules.</param>
    /// <param name="world">World state.</param>
    /// <returns>The volcano, or <c>null</c> on an island without one.</returns>
    public static Volcano? Find(VolcanoRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Disasters.Volcano is null && FindVolcano(rules, world.Regions) is { } crater)
        {
            world.Disasters.Volcano = new Volcano { RegionId = crater, Phase = VolcanoPhase.Dormant, PhaseEndsTick = 0, NextTremorTick = 0 };
        }

        return world.Disasters.Volcano;
    }

    /// <summary>How fully a reach covers a point: 1 at its centre, falling to 0 at its radius.</summary>
    /// <param name="point">The point.</param>
    /// <param name="centre">The reach's centre.</param>
    /// <param name="radius">Its radius in metres.</param>
    /// <returns>Coverage (0..1).</returns>
    public static float Falloff(GroundPosition point, GroundPosition centre, float radius)
    {
        var dx = point.X - centre.X;
        var dz = point.Z - centre.Z;
        return radius <= 0f ? 0f : SimMath.Clamp01(1f - (MathF.Sqrt((dx * dx) + (dz * dz)) / radius));
    }

    /// <summary>Leaves a mark on the land.</summary>
    /// <param name="world">World state.</param>
    /// <param name="kind">What left it.</param>
    /// <param name="point">Where.</param>
    /// <param name="radius">Its radius in metres.</param>
    public static void AddMark(WorldState world, TerrainMarkKind kind, GroundPosition point, float radius)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.Disasters.Marks.Add(new TerrainMark { Kind = kind, X = point.X, Z = point.Z, Radius = radius, Tick = world.Tick });
    }

    /// <summary>Seconds as whole ticks, at least one.</summary>
    internal static long Ticks(float seconds) => Math.Max(1L, (long)MathF.Round(seconds * SimulationTime.TicksPerSecond));
}
