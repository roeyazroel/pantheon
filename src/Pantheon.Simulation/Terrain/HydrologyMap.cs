using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Terrain;

/// <summary>
/// The rivers and lakes cut into a generated heightfield (ADR-022): derived from the seed and the
/// settings like the terrain itself, never saved. Presentation draws the water from
/// <see cref="WaterSurface"/>; world generation turns the courses into <see cref="Domain.Water.WaterState"/>.
/// </summary>
public sealed class HydrologyMap
{
    private readonly float[] _waterSurface;

    /// <summary>Wraps generated hydrology. The array is owned by the map from now on.</summary>
    /// <param name="waterSurface">Fresh water surface height per height sample, <see cref="float.NaN"/> where there is none.</param>
    /// <param name="rivers">River courses, largest first; tributaries follow the rivers they join.</param>
    /// <param name="lakes">Lakes, largest first.</param>
    public HydrologyMap(float[] waterSurface, IReadOnlyList<RiverCourse> rivers, IReadOnlyList<LakeBasin> lakes)
    {
        ArgumentNullException.ThrowIfNull(waterSurface);
        ArgumentNullException.ThrowIfNull(rivers);
        ArgumentNullException.ThrowIfNull(lakes);
        _waterSurface = waterSurface;
        Rivers = rivers;
        Lakes = lakes;
    }

    /// <summary>Fresh water surface height per height sample, row-major; <see cref="float.NaN"/> where there is none.</summary>
    public ReadOnlySpan<float> WaterSurface => _waterSurface;

    /// <summary>River courses, largest first.</summary>
    public IReadOnlyList<RiverCourse> Rivers { get; }

    /// <summary>Lakes, largest first.</summary>
    public IReadOnlyList<LakeBasin> Lakes { get; }

    /// <summary>A map without fresh water for a field of <paramref name="samples"/> samples.</summary>
    /// <param name="samples">Number of height samples.</param>
    /// <returns>A map whose every sample is dry.</returns>
    public static HydrologyMap Dry(int samples)
    {
        var surface = new float[samples];
        Array.Fill(surface, float.NaN);
        return new HydrologyMap(surface, [], []);
    }
}

/// <summary>A river's course from its source to its mouth or to the river it joins.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Points">Points along its middle, source first.</param>
/// <param name="JoinsRiver">Index in <see cref="HydrologyMap.Rivers"/> of the river it flows into, or <c>null</c> when it reaches the sea.</param>
public sealed record RiverCourse(string Name, IReadOnlyList<RiverPoint> Points, int? JoinsRiver);

/// <summary>One point along a river.</summary>
/// <param name="X">World X in metres.</param>
/// <param name="Z">World Z in metres.</param>
/// <param name="Surface">Height of the water in metres.</param>
/// <param name="Width">Width of the water in metres.</param>
/// <param name="Depth">Depth of the channel below the water in metres.</param>
public readonly record struct RiverPoint(float X, float Z, float Surface, float Width, float Depth);

/// <summary>A lake.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Surface">Height of its surface in metres.</param>
/// <param name="Cells">Centres of the flow-grid cells it covers.</param>
/// <param name="CellSize">Side of a flow-grid cell in metres.</param>
public sealed record LakeBasin(string Name, float Surface, IReadOnlyList<GroundPosition> Cells, float CellSize);
