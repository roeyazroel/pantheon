using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>A place for a house on a village's plan.</summary>
/// <param name="Ground">Its centre.</param>
/// <param name="Yaw">The yaw that turns its door to its lane.</param>
/// <param name="Order">How soon it is built; lower is sooner.</param>
/// <param name="Size">A value in 0..1 for its size and its variant.</param>
public readonly record struct VillagePlot(Vector2 Ground, float Yaw, float Order, float Size);

/// <summary>
/// How a village is laid out (spec §61, ADR-077): a green around the shrine, lanes running out from
/// it along the eight ways a road can leave a region, and cross lanes linking them as it grows. Roads
/// run from region centre to region centre, and a village stands at one, so a road built later lands
/// on a lane and no house has to move. Houses line the lanes with their doors to them; each village
/// builds on its own sides of its lanes, so no two look alike. Everything comes from the village's
/// id, never the land, so growth only adds houses. Pure.
/// </summary>
public static class VillagePlan
{
    /// <summary>The lanes running out from the green.</summary>
    public const int Spokes = 8;

    // Hash salts, so sides, plots and sizes draw unrelated values.
    private const ulong SideSalt = 11;
    private const ulong PlotSalt = 12;
    private const ulong AlongSalt = 13;
    private const ulong BackSalt = 14;
    private const ulong TurnSalt = 15;
    private const ulong OrderSalt = 16;

    // A plot's turn from facing its lane, in radians, and how much sooner or later it is built.
    private const float MostTurn = 0.14f;
    private const float OrderSpread = 0.2f;

    // Share of plots left empty, for gardens and gaps.
    private const float Gap = 0.14f;

    // The cross lanes keep this share of a plot's frontage clear of a lane running out.
    private const float CornerClearance = 0.75f;

    // Metres between the points of a cross lane, and how far a lane runs past its last house.
    private const float ArcStep = 3f;
    private const float LaneTail = 3f;

    /// <summary>The direction of a lane running out from the green: a compass point, as roads leave regions.</summary>
    /// <param name="spoke">Which, 0 to <see cref="Spokes"/> - 1.</param>
    /// <returns>A unit (X, Z) direction.</returns>
    public static Vector2 Direction(int spoke)
    {
        var angle = spoke * MathF.Tau / Spokes;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    /// <summary>The radius of a cross lane.</summary>
    /// <param name="ring">Which, the green's own being 0.</param>
    /// <param name="settings">Layout settings.</param>
    /// <returns>Metres from the shrine.</returns>
    public static float RingRadius(int ring, SettlementLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.GreenRadius + (ring * settings.CrossLaneSpacing);
    }

    /// <summary>
    /// The plots whose order is at most <paramref name="reach"/>, soonest first. A longer reach only
    /// adds plots after these, so the first plots of any reach are the same.
    /// </summary>
    /// <param name="settlementId">The village.</param>
    /// <param name="center">The shrine.</param>
    /// <param name="settings">Layout settings.</param>
    /// <param name="reach">How far out to look, in metres.</param>
    /// <returns>The plots.</returns>
    public static List<VillagePlot> Plots(int settlementId, Vector2 center, SettlementLayoutSettings settings, float reach)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var plots = new List<VillagePlot>();
        for (var ring = 0; RingRadius(ring, settings) <= reach; ring++)
        {
            RingPlots(settlementId, center, settings, ring, reach, plots);
        }

        for (var spoke = 0; spoke < Spokes; spoke++)
        {
            foreach (var side in new[] { -1, 1 })
            {
                if (PresentationHash.Unit(SideSalt, (ulong)(uint)settlementId, (ulong)spoke, (ulong)(side + 1)) < settings.LaneShare)
                {
                    SpokePlots(settlementId, center, settings, spoke, side, reach, plots);
                }
            }
        }

        // Ties cannot reorder the plots: their order depends on their place alone.
        return [.. plots.Where(plot => plot.Order <= reach).OrderBy(plot => plot.Order).ThenBy(plot => plot.Ground.X).ThenBy(plot => plot.Ground.Y)];
    }

    /// <summary>Plots along one side of a lane running out, between the cross lanes.</summary>
    private static void SpokePlots(int id, Vector2 center, SettlementLayoutSettings settings, int spoke, int side, float reach, List<VillagePlot> plots)
    {
        var (way, across) = (Direction(spoke), new Vector2(-Direction(spoke).Y, Direction(spoke).X) * side);
        var start = settings.GreenRadius + settings.Setback + (settings.PlotFrontage * 0.5f);
        var count = (int)MathF.Ceiling((reach - start) / settings.PlotFrontage) + 2;
        for (var i = 0; i < count; i++)
        {
            var along = start + (i * settings.PlotFrontage);
            if (NearRing(along, settings))
            {
                continue;
            }

            var key = (ulong)(uint)((spoke * 1000) + (side * 500) + i);
            var jitter = (PresentationHash.Unit(AlongSalt, (ulong)(uint)id, key) - 0.5f) * settings.PlotJitter;
            var back = (PresentationHash.Unit(BackSalt, (ulong)(uint)id, key) - 0.5f) * settings.PlotJitter;
            var foot = center + (way * (along + jitter));
            Add(id, key, foot + (across * (settings.Setback + back)), foot, center, settings, plots);
        }
    }

    /// <summary>Plots along the outer side of a cross lane, or round the green, facing in, clear of the lanes running out.</summary>
    private static void RingPlots(int id, Vector2 center, SettlementLayoutSettings settings, int ring, float reach, List<VillagePlot> plots)
    {
        var radius = RingRadius(ring, settings);
        var step = settings.PlotFrontage / radius;
        var count = (int)(MathF.Tau / step);
        var clear = settings.Setback + (settings.PlotFrontage * CornerClearance);
        for (var i = 0; i < count; i++)
        {
            var angle = (i + (ring * 0.5f)) * MathF.Tau / count;
            var fromLane = MathF.Abs((angle * Spokes / MathF.Tau) - MathF.Round(angle * Spokes / MathF.Tau)) * MathF.Tau / Spokes;
            if (fromLane * radius < clear)
            {
                continue;
            }

            var key = (ulong)(uint)(100000 + (ring * 1000) + i);
            var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var back = (PresentationHash.Unit(BackSalt, (ulong)(uint)id, key) - 0.5f) * settings.PlotJitter;
            var foot = center + (outward * radius);
            Add(id, key, foot + (outward * (settings.Setback + back)), foot, center, settings, plots);
        }
    }

    /// <summary>Adds a plot, unless the hash leaves it a gap.</summary>
    private static void Add(int id, ulong key, Vector2 ground, Vector2 foot, Vector2 center, SettlementLayoutSettings settings, List<VillagePlot> plots)
    {
        if (PresentationHash.Unit(PlotSalt, (ulong)(uint)id, key) < Gap)
        {
            return;
        }

        var turn = (PresentationHash.Unit(TurnSalt, (ulong)(uint)id, key) - 0.5f) * 2f * MostTurn;
        var yaw = MathF.Atan2(foot.X - ground.X, foot.Y - ground.Y) + turn;
        var order = Vector2.Distance(center, ground) * (1f + (PresentationHash.Unit(OrderSalt, (ulong)(uint)id, key) * OrderSpread));
        plots.Add(new VillagePlot(ground, yaw, order, PresentationHash.Unit(PlotSalt + 100, (ulong)(uint)id, key)));
    }

    /// <summary>Whether a place along a lane lies where a cross lane meets it.</summary>
    private static bool NearRing(float along, SettlementLayoutSettings settings)
    {
        var ring = MathF.Round((along - settings.GreenRadius) / settings.CrossLaneSpacing);
        return ring >= 1f && MathF.Abs(along - RingRadius((int)ring, settings)) < settings.Setback;
    }

    /// <summary>
    /// The village's lanes as they stand: each lane out as far as its last house, each stretch of a
    /// cross lane that has houses on it, and a short path from every door.
    /// </summary>
    /// <param name="site">The village's layout.</param>
    /// <param name="settings">Layout settings.</param>
    /// <returns>Each lane's points on the ground, from the shrine outwards.</returns>
    public static List<List<Vector2>> Lanes(SettlementSite site, SettlementLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(settings);
        var center = new Vector2(site.Center.X, site.Center.Z);
        var lanes = new List<List<Vector2>>();
        var far = new float[Spokes];
        var rung = new HashSet<(int Ring, int Sector)>();
        var line = settings.Setback + settings.PlotJitter;
        foreach (var house in site.Houses)
        {
            var ground = new Vector2(house.Position.X, house.Position.Z) - center;
            var onLane = false;
            for (var spoke = 0; spoke < Spokes; spoke++)
            {
                var along = Vector2.Dot(ground, Direction(spoke));
                if (along > 0f && MathF.Abs(Vector2.Dot(ground, new Vector2(-Direction(spoke).Y, Direction(spoke).X))) <= line)
                {
                    (far[spoke], onLane) = (MathF.Max(far[spoke], along), true);
                }
            }

            if (!onLane)
            {
                var ring = (int)MathF.Round((ground.Length() - settings.Setback - settings.GreenRadius) / settings.CrossLaneSpacing);
                var turn = MathF.Atan2(ground.Y, ground.X);
                rung.Add((Math.Max(0, ring), (int)MathF.Floor(((turn < 0f ? turn + MathF.Tau : turn) * Spokes / MathF.Tau) % Spokes)));
            }
        }

        for (var spoke = 0; spoke < Spokes; spoke++)
        {
            if (far[spoke] > 0f)
            {
                var way = Direction(spoke);
                lanes.Add([center + (way * (settings.GreenRadius - 2f)), center + (way * (far[spoke] + LaneTail))]);
            }
        }

        foreach (var (ring, sector) in rung.OrderBy(r => r.Ring).ThenBy(r => r.Sector))
        {
            lanes.Add(Arc(center, RingRadius(ring, settings), sector));
        }

        return lanes;
    }

    /// <summary>A stretch of a cross lane, from one lane out to the next.</summary>
    private static List<Vector2> Arc(Vector2 center, float radius, int sector)
    {
        var (from, sweep) = (sector * MathF.Tau / Spokes, MathF.Tau / Spokes);
        var steps = Math.Max(2, (int)MathF.Ceiling(sweep * radius / ArcStep));
        var points = new List<Vector2>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var angle = from + (sweep * i / steps);
            points.Add(center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius));
        }

        return points;
    }

    /// <summary>The short path from each house's door to its lane.</summary>
    /// <param name="site">The village's layout.</param>
    /// <param name="settings">Layout settings.</param>
    /// <returns>Each path's two ends.</returns>
    public static List<List<Vector2>> Paths(SettlementSite site, SettlementLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(settings);
        var paths = new List<List<Vector2>>(site.Houses.Count);
        foreach (var house in site.Houses)
        {
            var (ground, facing) = (new Vector2(house.Position.X, house.Position.Z), SettlementSite.Facing(house.Yaw));
            paths.Add([ground + (facing * (settings.DoorDistance * 0.6f)), ground + (facing * settings.Setback)]);
        }

        return paths;
    }
}
