using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Roads;
using Pantheon.Game.Presentation.Nature;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>A cross-section of a road: its edges and its middle on the ground, and how far along it lies.</summary>
/// <param name="Left">Its left edge.</param>
/// <param name="Centre">Its middle.</param>
/// <param name="Right">Its right edge.</param>
/// <param name="Along">Metres from the road's start.</param>
public readonly record struct RoadSection(Vector3 Left, Vector3 Centre, Vector3 Right, float Along);

/// <summary>
/// The worn track a road is drawn as (ADR-033, ADR-073): its way smoothed into curves (the villages
/// plan it from region to region, in straight legs), a cross-section every <see cref="Step"/> metres
/// along the part built so far, its width wandering a little about <see cref="Width"/>, and its edges
/// and middle each laid on the ground so it follows the land across as well as along. Presentation
/// only: the roads' rules see the way as planned.
/// </summary>
public static class RoadLayout
{
    /// <summary>A road's width in metres, about which it wanders.</summary>
    public const float Width = 3.6f;

    /// <summary>Metres between the track's cross-sections.</summary>
    public const float Step = 1.5f;

    /// <summary>How far above the ground the track lies; the road shader also draws it towards the eye.</summary>
    public const float Lift = 0.06f;

    // How many times the way's corners are cut; each cut rounds them further.
    private const int Smoothing = 3;

    /// <summary>The cross-sections of the road's built part.</summary>
    /// <param name="road">The road.</param>
    /// <param name="terrain">Terrain, for the ground's height.</param>
    /// <returns>Cross-sections from the first village; none when nothing is built.</returns>
    public static List<RoadSection> Ribbon(RoadState road, TerrainField terrain)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(terrain);
        var centre = Centre(road);
        var sections = new List<RoadSection>(centre.Count);
        for (var i = 0; i < centre.Count; i++)
        {
            var (point, along) = centre[i];
            var ahead = centre[Math.Min(centre.Count - 1, i + 1)].Point - centre[Math.Max(0, i - 1)].Point;
            var width = WidthAt(road.Id, along);
            var side = ahead.LengthSquared() > 0f ? Vector2.Normalize(new Vector2(-ahead.Y, ahead.X)) * (width * 0.5f) : Vector2.Zero;
            sections.Add(new RoadSection(Ground(terrain, point + side), Ground(terrain, point), Ground(terrain, point - side), along));
        }

        return sections;
    }

    /// <summary>The road's width at a place along it: within a tenth of <see cref="Width"/> either way, wandering slowly.</summary>
    /// <param name="roadId">The road.</param>
    /// <param name="along">Metres from its start.</param>
    /// <returns>The width, in metres.</returns>
    public static float WidthAt(int roadId, float along) =>
        Width * (0.9f + (0.2f * NatureNoise.Fbm(along / 18f, roadId * 7.3f, 4096, 2, 7300 + roadId)));

    /// <summary>The way's middle every <see cref="Step"/> metres along its built share, its corners rounded.</summary>
    private static List<(Vector2 Point, float Along)> Centre(RoadState road)
    {
        var points = new List<(Vector2, float)>();
        if (road.Built <= 0f || road.Path.Count < 2 || road.Length <= 0f)
        {
            return points;
        }

        var way = Smooth(road);
        var total = 0f;
        for (var i = 1; i < way.Count; i++)
        {
            total += Vector2.Distance(way[i - 1], way[i]);
        }

        // The rounded way is a little shorter than the planned one; its built share is the same.
        var built = total * Math.Clamp(road.Built / road.Length, 0f, 1f);
        points.Add((way[0], 0f));
        var (walked, next) = (0f, Step);
        for (var i = 1; i < way.Count; i++)
        {
            var (a, b) = (way[i - 1], way[i]);
            var length = Vector2.Distance(a, b);
            while (next <= walked + length && next <= built)
            {
                points.Add((Vector2.Lerp(a, b, (next - walked) / length), next));
                next += Step;
            }

            if (built <= walked + length)
            {
                if (built > points[^1].Item2 + 0.01f)
                {
                    points.Add((Vector2.Lerp(a, b, (built - walked) / length), built));
                }

                break;
            }

            walked += length;
        }

        return points;
    }

    /// <summary>The way with its corners cut <see cref="Smoothing"/> times (Chaikin's), its ends kept.</summary>
    private static List<Vector2> Smooth(RoadState road)
    {
        var way = new List<Vector2>(road.Path.Count);
        foreach (var point in road.Path)
        {
            way.Add(new Vector2(point.X, point.Z));
        }

        for (var pass = 0; pass < Smoothing && way.Count > 2; pass++)
        {
            var cut = new List<Vector2>((way.Count * 2) + 1) { way[0] };
            for (var i = 0; i < way.Count - 1; i++)
            {
                cut.Add(Vector2.Lerp(way[i], way[i + 1], 0.25f));
                cut.Add(Vector2.Lerp(way[i], way[i + 1], 0.75f));
            }

            cut.Add(way[^1]);
            way = cut;
        }

        return way;
    }

    /// <summary>A point on the ground (or the sea's surface), lifted.</summary>
    private static Vector3 Ground(TerrainField terrain, Vector2 point) => new(point.X, Math.Max(terrain.SampleHeight(point.X, point.Y), 0f) + Lift, point.Y);
}
