using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Builds line-list vertices (consecutive pairs form segments) that follow region borders over
/// the terrain, or over the sea surface where the ground is underwater.
/// </summary>
public static class RegionBoundaryBuilder
{
    /// <summary>Lines along every internal and external region border.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="field">Terrain the lines are draped on.</param>
    /// <param name="lift">Height above the surface, in metres, so lines do not sink into the ground.</param>
    /// <param name="sampleSpacing">Distance between draped points in metres.</param>
    /// <returns>Line-list vertices.</returns>
    public static Vector3[] BuildGridLines(RegionGrid grid, TerrainField field, float lift, float sampleSpacing)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(field);
        ValidateSpacing(sampleSpacing);

        var width = grid.Columns * grid.RegionSize;
        var depth = grid.Rows * grid.RegionSize;
        var vertices = new List<Vector3>();
        for (var column = 0; column <= grid.Columns; column++)
        {
            var x = column * grid.RegionSize;
            AppendDraped(vertices, field, new Vector2(x, 0f), new Vector2(x, depth), lift, sampleSpacing);
        }

        for (var row = 0; row <= grid.Rows; row++)
        {
            var z = row * grid.RegionSize;
            AppendDraped(vertices, field, new Vector2(0f, z), new Vector2(width, z), lift, sampleSpacing);
        }

        return [.. vertices];
    }

    /// <summary>Closed outline around one region.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="regionId">Region to outline.</param>
    /// <param name="field">Terrain the outline is draped on.</param>
    /// <param name="lift">Height above the surface in metres.</param>
    /// <param name="sampleSpacing">Distance between draped points in metres.</param>
    /// <returns>Line-list vertices.</returns>
    public static Vector3[] BuildOutline(RegionGrid grid, int regionId, TerrainField field, float lift, float sampleSpacing)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(field);
        ValidateSpacing(sampleSpacing);

        var (column, row) = grid.GetCoordinates(grid.GetRegion(regionId).Id);
        var min = new Vector2(column * grid.RegionSize, row * grid.RegionSize);
        var max = min + new Vector2(grid.RegionSize);
        var corners = new[] { min, new Vector2(max.X, min.Y), max, new Vector2(min.X, max.Y) };

        var vertices = new List<Vector3>();
        for (var i = 0; i < corners.Length; i++)
        {
            AppendDraped(vertices, field, corners[i], corners[(i + 1) % corners.Length], lift, sampleSpacing);
        }

        return [.. vertices];
    }

    /// <summary>Adds the segments of a straight ground line, with each point lifted above the visible surface.</summary>
    private static void AppendDraped(List<Vector3> vertices, TerrainField field, Vector2 from, Vector2 to, float lift, float sampleSpacing)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(from, to) / sampleSpacing));
        var previous = Drape(field, from, lift);
        for (var step = 1; step <= steps; step++)
        {
            var current = Drape(field, Vector2.Lerp(from, to, step / (float)steps), lift);
            vertices.Add(previous);
            vertices.Add(current);
            previous = current;
        }
    }

    /// <summary>Places a ground point on the terrain or on the sea surface (height 0), whichever is higher.</summary>
    private static Vector3 Drape(TerrainField field, Vector2 point, float lift) =>
        new(point.X, MathF.Max(field.SampleHeight(point.X, point.Y), 0f) + lift, point.Y);

    /// <summary>Rejects non-positive sample spacing.</summary>
    private static void ValidateSpacing(float sampleSpacing)
    {
        if (!(sampleSpacing > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(sampleSpacing), sampleSpacing, "Sample spacing must be positive.");
        }
    }
}
