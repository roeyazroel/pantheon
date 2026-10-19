using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Roads;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// The ribbon a road is drawn as (ADR-033): pairs of left and right edge points on the ground every
/// few metres along the part built so far. Presentation only.
/// </summary>
public static class RoadLayout
{
    /// <summary>A road's width in metres.</summary>
    public const float Width = 3.6f;

    /// <summary>Metres between the ribbon's cross-sections.</summary>
    public const float Step = 4f;

    /// <summary>How far above the ground the ribbon lies, so the terrain does not show through.</summary>
    public const float Lift = 0.25f;

    /// <summary>The edges of the road's built part.</summary>
    /// <param name="road">The road.</param>
    /// <param name="terrain">Terrain, for the ground's height.</param>
    /// <returns>Left and right points, from the first village; none when nothing is built.</returns>
    public static List<(Vector3 Left, Vector3 Right)> Ribbon(RoadState road, TerrainField terrain)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(terrain);
        var centre = Centre(road);
        var edges = new List<(Vector3, Vector3)>(centre.Count);
        for (var i = 0; i < centre.Count; i++)
        {
            var ahead = centre[Math.Min(centre.Count - 1, i + 1)] - centre[Math.Max(0, i - 1)];
            var side = ahead.LengthSquared() > 0f ? Vector2.Normalize(new Vector2(-ahead.Y, ahead.X)) * (Width * 0.5f) : Vector2.Zero;
            edges.Add((Ground(terrain, centre[i] + side), Ground(terrain, centre[i] - side)));
        }

        return edges;
    }

    /// <summary>Points every <see cref="Step"/> metres along the built part of the way.</summary>
    private static List<Vector2> Centre(RoadState road)
    {
        var points = new List<Vector2>();
        var left = Math.Min(road.Built, road.Length);
        if (left <= 0f || road.Path.Count < 2)
        {
            return points;
        }

        points.Add(new Vector2(road.Path[0].X, road.Path[0].Z));
        var carried = 0f;
        for (var i = 1; i < road.Path.Count && left > 0f; i++)
        {
            var a = new Vector2(road.Path[i - 1].X, road.Path[i - 1].Z);
            var b = new Vector2(road.Path[i].X, road.Path[i].Z);
            var length = Vector2.Distance(a, b);
            var along = Step - carried;
            while (along <= length && along <= left)
            {
                points.Add(Vector2.Lerp(a, b, along / length));
                along += Step;
            }

            if (left <= length)
            {
                points.Add(Vector2.Lerp(a, b, left / length));
                break;
            }

            carried = length - (along - Step);
            left -= length;
            if (i == road.Path.Count - 1)
            {
                points.Add(b);
            }
        }

        return points;
    }

    /// <summary>A point on the ground (or the sea's surface), lifted.</summary>
    private static Vector3 Ground(TerrainField terrain, Vector2 point) => new(point.X, Math.Max(terrain.SampleHeight(point.X, point.Y), 0f) + Lift, point.Y);
}
