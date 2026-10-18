using System;
using System.Collections.Generic;

namespace Pantheon.Domain.World;

/// <summary>
/// Uniform grid of square regions covering the world, starting at world origin (0, 0).
/// <see cref="Cells"/> is row-major, and <c>Cells[i].Id == i</c>.
/// </summary>
public sealed class RegionGrid
{
    /// <summary>Number of regions along the world X axis.</summary>
    public required int Columns { get; init; }

    /// <summary>Number of regions along the world Z axis.</summary>
    public required int Rows { get; init; }

    /// <summary>Side length of one region in metres.</summary>
    public required float RegionSize { get; init; }

    /// <summary>Region states in row-major order.</summary>
    public required List<RegionState> Cells { get; init; }

    /// <summary>Finds the region containing a world position.</summary>
    /// <param name="x">World X coordinate in metres.</param>
    /// <param name="z">World Z coordinate in metres.</param>
    /// <param name="regionId">The region id, or -1 when the position is outside the grid.</param>
    /// <returns><c>true</c> when the position lies inside the grid.</returns>
    public bool TryGetRegionIdAt(float x, float z, out int regionId)
    {
        regionId = -1;
        if (RegionSize <= 0f || !float.IsFinite(x) || !float.IsFinite(z))
        {
            return false;
        }

        var column = (int)MathF.Floor(x / RegionSize);
        var row = (int)MathF.Floor(z / RegionSize);
        if (column < 0 || row < 0 || column >= Columns || row >= Rows)
        {
            return false;
        }

        regionId = (row * Columns) + column;
        return true;
    }

    /// <summary>Returns the region with the given id.</summary>
    /// <param name="regionId">Row-major region id.</param>
    /// <returns>The region state.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The id is outside the grid.</exception>
    public RegionState GetRegion(int regionId)
    {
        if (regionId < 0 || regionId >= Cells.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(regionId), regionId, "Region id is outside the grid.");
        }

        return Cells[regionId];
    }

    /// <summary>Converts a region id into grid coordinates.</summary>
    /// <param name="regionId">Row-major region id.</param>
    /// <returns>The zero-based column and row.</returns>
    public (int Column, int Row) GetCoordinates(int regionId) => (regionId % Columns, regionId / Columns);

    /// <summary>Returns the world-space centre of a region.</summary>
    /// <param name="regionId">Row-major region id.</param>
    /// <returns>The centre position in metres.</returns>
    public GroundPosition GetRegionCenter(int regionId)
    {
        var (column, row) = GetCoordinates(regionId);
        return new GroundPosition((column + 0.5f) * RegionSize, (row + 0.5f) * RegionSize);
    }
}
