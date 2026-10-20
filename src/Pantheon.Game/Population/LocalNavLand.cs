using System;
using System.Numerics;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Population;

/// <summary>
/// The land under a village's walkers' grid (ADR-056): which cells lie in the sea or in fresh water.
/// A village keeps it between bakes of its grid while the grid keeps its frame and the terrain is the
/// same, as sampling the terrain under every cell cost most of a rebuild.
/// </summary>
public sealed class LocalNavLand
{
    private TerrainField? _terrain;

    /// <summary>The grid's corner on the ground.</summary>
    public Vector2 Origin { get; private set; }

    /// <summary>The cell size, in metres.</summary>
    public float Cell { get; private set; }

    /// <summary>Cells a side.</summary>
    public int Side { get; private set; }

    /// <summary>Whether each cell lies in water, row by row.</summary>
    public bool[] Water { get; private set; } = [];

    /// <summary>Whether this land was sampled for a frame over a terrain.</summary>
    /// <param name="terrain">The terrain.</param>
    /// <param name="origin">The grid's corner.</param>
    /// <param name="cell">The cell size.</param>
    /// <param name="side">Cells a side.</param>
    /// <returns><c>true</c> when it can be reused.</returns>
    public bool Fits(TerrainField terrain, Vector2 origin, float cell, int side) =>
        ReferenceEquals(terrain, _terrain) && Water.Length > 0 && Origin == origin && Cell == cell && Side == side;

    /// <summary>Samples the land for a frame over a terrain.</summary>
    /// <param name="terrain">The terrain.</param>
    /// <param name="origin">The grid's corner.</param>
    /// <param name="cell">The cell size.</param>
    /// <param name="side">Cells a side.</param>
    public void Sample(TerrainField terrain, Vector2 origin, float cell, int side)
    {
        (_terrain, Origin, Cell, Side) = (terrain, origin, cell, side);
        Water = SampleWater(terrain, origin, cell, side);
    }

    /// <summary>Which cells of a frame lie in the sea or in fresh water.</summary>
    /// <param name="terrain">The terrain.</param>
    /// <param name="origin">The grid's corner.</param>
    /// <param name="cell">The cell size.</param>
    /// <param name="side">Cells a side.</param>
    /// <returns>The water, row by row.</returns>
    public static bool[] SampleWater(TerrainField terrain, Vector2 origin, float cell, int side)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        var water = new bool[side * side];
        for (var z = 0; z < side; z++)
        {
            for (var x = 0; x < side; x++)
            {
                var (wx, wz) = (origin.X + ((x + 0.5f) * cell), origin.Y + ((z + 0.5f) * cell));
                water[(z * side) + x] = terrain.SampleHeight(wx, wz) < 0f || terrain.IsUnderFreshWater(wx, wz);
            }
        }

        return water;
    }
}
