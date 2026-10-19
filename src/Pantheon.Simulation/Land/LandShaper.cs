using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Land;
using Pantheon.Domain.Water;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Simulation.Land;

/// <summary>
/// The Divine Acts that reshape the land (spec §14, ADR-045). <see cref="Plan"/> finds what an act
/// would do where the god points: an island in the sea, a gorge through a mountain to the low
/// ground on either side, a river's new course down to where the god points. <see cref="Apply"/>
/// keeps the reshaping in the world, makes the terrain again with it and settles the regions and the
/// water to the new land.
/// </summary>
public static class LandShaper
{
    // Directions tried for a gorge, over half a turn.
    private const int GorgeDirections = 8;

    // Metres between the river points a dam is sought along.
    private const float DamStep = 4f;

    /// <summary>What an act would do where the god points, or <c>null</c> when it cannot be done there.</summary>
    /// <param name="rules">Land rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="terrain">The current terrain.</param>
    /// <param name="kind">The act.</param>
    /// <param name="target">Where the god points.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The reshaping, not yet kept, or <c>null</c>.</returns>
    public static TerrainReshape? Plan(LandRules rules, WorldState world, TerrainField terrain, ReshapeKind kind, GroundPosition target, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(terrain);
        if (!rules.Enabled || !Inside(terrain, target, 0f))
        {
            return null;
        }

        return kind switch
        {
            ReshapeKind.Island => PlanIsland(rules, world, terrain, target, godId),
            ReshapeKind.Gorge => PlanGorge(rules, world, terrain, target, godId),
            ReshapeKind.Channel => PlanChannel(rules, world, terrain, target, godId),
        };
    }

    /// <summary>Keeps a reshaping, makes the terrain again with every reshaping, and settles the regions and water to it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="terrain">The current terrain.</param>
    /// <param name="reshape">The reshaping, from <see cref="Plan"/>.</param>
    /// <returns>The new terrain.</returns>
    public static TerrainField Apply(WorldState world, TerrainField terrain, TerrainReshape reshape)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(reshape);
        world.Land.Reshapes.Add(reshape);
        world.Land.NextReshapeId = Math.Max(world.Land.NextReshapeId, reshape.Id + 1);
        var reshaped = TerrainGenerator.Reshape(terrain, world.Seed, world.Generation, world.Land.Reshapes);
        Settle(world, reshaped);
        return reshaped;
    }

    /// <summary>
    /// Settles the regions and the water to a reshaped terrain: a region whose ground changed takes
    /// its new height, climate, biome and fresh water; new land takes the soil, plants and stores of
    /// new land; and the rivers and lakes are found again, each river keeping the level of the one it
    /// shares most land with.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="terrain">The reshaped terrain.</param>
    public static void Settle(WorldState world, TerrainField terrain)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(terrain);
        var fresh = RegionBuilder.Build(terrain, world.Generation);
        ResourceFounder.Found(fresh, world.Seed, world.Generation.Resources);
        for (var id = 0; id < world.Regions.Cells.Count && id < fresh.Cells.Count; id++)
        {
            var (old, now) = (world.Regions.Cells[id], fresh.Cells[id]);
            if (old.Biome == now.Biome && MathF.Abs(old.Elevation - now.Elevation) < 0.01f && old.FreshWater == now.FreshWater)
            {
                continue;
            }

            var risen = old.Biome == Biome.Sea && now.Biome != Biome.Sea;
            var changed = old.Biome != now.Biome;
            (old.Elevation, old.Temperature, old.Moisture, old.FreshWater, old.Biome) = (now.Elevation, now.Temperature, now.Moisture, now.FreshWater, now.Biome);
            if (changed)
            {
                (old.Fertility, old.Vegetation) = (now.Fertility, now.Vegetation);
            }

            if (risen)
            {
                (old.SoilMoisture, old.Forest, old.Timber, old.Stone) = (now.SoilMoisture, now.Forest, now.Timber, now.Stone);
            }
        }

        var water = WaterFounder.Found(terrain.Hydrology, world.Regions);
        foreach (var river in water.Rivers)
        {
            var before = world.Water.Rivers.OrderByDescending(r => r.Regions.Count(river.Regions.Contains)).ThenBy(r => r.Id).FirstOrDefault();
            if (before is not null && before.Regions.Exists(river.Regions.Contains))
            {
                river.Level = before.Level;
            }
        }

        world.Water.Rivers.Clear();
        world.Water.Rivers.AddRange(water.Rivers);
        world.Water.Lakes.Clear();
        world.Water.Lakes.AddRange(water.Lakes);
    }

    /// <summary>An island where the god points in the open sea: not in a lake.</summary>
    private static TerrainReshape? PlanIsland(LandRules rules, WorldState world, TerrainField terrain, GroundPosition target, int godId) =>
        Ground(terrain, target) < -0.5f && Inside(terrain, target, rules.IslandRadius)
            && world.Regions.TryGetRegionIdAt(target.X, target.Z, out var region) && world.Regions.Cells[region].Biome == Biome.Sea
            ? New(world, ReshapeKind.Island, godId, target, target, rules.IslandRadius, rules.IslandPeak, 0f, 0f)
            : null;

    /// <summary>
    /// A gorge through the mountain where the god points, along whichever of the directions tried
    /// leads down to the lowest ground on both sides; none when no way through falls far enough.
    /// </summary>
    private static TerrainReshape? PlanGorge(LandRules rules, WorldState world, TerrainField terrain, GroundPosition target, int godId)
    {
        var peak = Ground(terrain, target);
        if (peak < rules.MountainHeight)
        {
            return null;
        }

        var half = rules.GorgeLength / 2f;
        (GroundPosition From, GroundPosition To, float Higher)? best = null;
        for (var i = 0; i < GorgeDirections; i++)
        {
            var angle = MathF.PI * i / GorgeDirections;
            var (dx, dz) = (MathF.Cos(angle) * half, MathF.Sin(angle) * half);
            var (from, to) = (new GroundPosition(target.X - dx, target.Z - dz), new GroundPosition(target.X + dx, target.Z + dz));
            if (!Inside(terrain, from, rules.GorgeWidth) || !Inside(terrain, to, rules.GorgeWidth))
            {
                continue;
            }

            var higher = MathF.Max(Ground(terrain, from), Ground(terrain, to));
            if (best is null || higher < best.Value.Higher)
            {
                best = (from, to, higher);
            }
        }

        if (best is not { } way || way.Higher > peak - rules.PassDrop)
        {
            return null;
        }

        var (a, b) = (MathF.Max(1f, Ground(terrain, way.From)), MathF.Max(1f, Ground(terrain, way.To)));
        return New(world, ReshapeKind.Gorge, godId, way.From, way.To, rules.GorgeWidth, 0f, a, b);
    }

    /// <summary>
    /// A new course for the river nearest where the god points, down to that point: a channel from
    /// the river's nearest point inland, and a dam a little downstream across its old course. None
    /// when no river is near enough, the point lies too close or in the sea, or the water would have
    /// to run uphill.
    /// </summary>
    private static TerrainReshape? PlanChannel(LandRules rules, WorldState world, TerrainField terrain, GroundPosition target, int godId)
    {
        (RiverCourse Course, int Index, float Distance)? nearest = null;
        foreach (var course in terrain.Hydrology.Rivers)
        {
            for (var i = 0; i < course.Points.Count; i++)
            {
                var point = new GroundPosition(course.Points[i].X, course.Points[i].Z);
                var distance = Distance(point, target);
                if (distance <= rules.DivertReach && Ground(terrain, point) >= 1f + rules.MinFall && (nearest is null || distance < nearest.Value.Distance))
                {
                    nearest = (course, i, distance);
                }
            }
        }

        if (nearest is not { } river || river.Distance < rules.MinChannel)
        {
            return null;
        }

        var start = new GroundPosition(river.Course.Points[river.Index].X, river.Course.Points[river.Index].Z);
        var (from, to) = (Ground(terrain, start), Ground(terrain, target));
        if (to < 1f || to > from - rules.MinFall || Downstream(terrain, river.Course, river.Index, rules.DamOffset) is not { } dam)
        {
            return null;
        }

        var channel = New(world, ReshapeKind.Channel, godId, start, target, rules.ChannelWidth, rules.ChannelDepth, from, to);
        return new TerrainReshape
        {
            Id = channel.Id, Kind = channel.Kind, GodId = godId, Tick = channel.Tick, X = channel.X, Z = channel.Z, ToX = channel.ToX, ToZ = channel.ToZ,
            Radius = channel.Radius, Peak = channel.Peak, FromHeight = from, ToHeight = to, DamX = dam.X, DamZ = dam.Z, DamTop = from + rules.DamHeight,
        };
    }

    /// <summary>The river's point a distance downstream (towards the lower end) from a point, if the river runs on that far.</summary>
    private static GroundPosition? Downstream(TerrainField terrain, RiverCourse course, int index, float offset)
    {
        var points = course.Points;
        var forward = index + 1 < points.Count && (index == 0 || points[index + 1].Surface <= points[index - 1].Surface);
        var step = forward ? 1 : -1;
        var walked = 0f;
        for (var i = index; i + step >= 0 && i + step < points.Count; i += step)
        {
            walked += Distance(new GroundPosition(points[i].X, points[i].Z), new GroundPosition(points[i + step].X, points[i + step].Z));
            if (walked >= Math.Max(offset, DamStep))
            {
                return new GroundPosition(points[i + step].X, points[i + step].Z);
            }
        }

        return null;
    }

    /// <summary>A reshaping with the world's next id.</summary>
    private static TerrainReshape New(WorldState world, ReshapeKind kind, int godId, GroundPosition from, GroundPosition to, float radius, float peak, float fromHeight, float toHeight) => new()
    {
        Id = world.Land.NextReshapeId,
        Kind = kind,
        GodId = godId,
        Tick = world.Tick,
        X = from.X,
        Z = from.Z,
        ToX = to.X,
        ToZ = to.Z,
        Radius = radius,
        Peak = peak,
        FromHeight = fromHeight,
        ToHeight = toHeight,
    };

    /// <summary>The ground's height as the noise made it, at a point.</summary>
    private static float Ground(TerrainField terrain, GroundPosition point)
    {
        var x = Math.Clamp((int)MathF.Round(point.X / terrain.Spacing), 0, terrain.Resolution - 1);
        var z = Math.Clamp((int)MathF.Round(point.Z / terrain.Spacing), 0, terrain.Resolution - 1);
        return terrain.Ground[(z * terrain.Resolution) + x];
    }

    /// <summary>Whether a point lies a margin inside the terrain's edges.</summary>
    private static bool Inside(TerrainField terrain, GroundPosition point, float margin) =>
        point.X >= margin && point.Z >= margin && point.X <= terrain.MaxCoordinate - margin && point.Z <= terrain.MaxCoordinate - margin;

    /// <summary>Metres between two points on the ground.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
