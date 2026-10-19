using System;
using Pantheon.Domain.Ecology;

namespace Pantheon.Simulation.Terrain;

/// <summary>
/// Derived, read-only heightfield of the world: one sample every <see cref="Spacing"/> metres,
/// stored row-major (<c>index = z * Resolution + x</c>). It is regenerated from the seed and
/// generation settings and never saved (ADR-006). Presentation and picking read it.
/// </summary>
public sealed class TerrainField
{
    private readonly float[] _heights;
    private readonly float[] _moisture;
    private readonly float[] _temperature;
    private readonly Biome[] _biomes;

    /// <summary>Wraps generated sample arrays. The arrays are owned by the field from now on.</summary>
    /// <param name="resolution">Samples per side.</param>
    /// <param name="spacing">Metres between samples.</param>
    /// <param name="heights">Heights in metres (sea level = 0).</param>
    /// <param name="moisture">Normalised moisture per sample.</param>
    /// <param name="temperature">Normalised temperature per sample.</param>
    /// <param name="biomes">Biome per sample.</param>
    /// <param name="hydrology">The rivers and lakes cut into the heights (ADR-022); <c>null</c> for none.</param>
    public TerrainField(int resolution, float spacing, float[] heights, float[] moisture, float[] temperature, Biome[] biomes, HydrologyMap? hydrology = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(resolution, 2);
        if (!(spacing > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "Spacing must be positive.");
        }

        var count = resolution * resolution;
        EnsureLength(heights, count, nameof(heights));
        EnsureLength(moisture, count, nameof(moisture));
        EnsureLength(temperature, count, nameof(temperature));
        EnsureLength(biomes, count, nameof(biomes));

        Resolution = resolution;
        Spacing = spacing;
        _heights = heights;
        _moisture = moisture;
        _temperature = temperature;
        _biomes = biomes;
        Hydrology = hydrology ?? HydrologyMap.Dry(count);
        if (Hydrology.WaterSurface.Length != count)
        {
            throw new ArgumentException($"Expected {count} water samples but got {Hydrology.WaterSurface.Length}.", nameof(hydrology));
        }
    }

    /// <summary>Samples per side.</summary>
    public int Resolution { get; }

    /// <summary>Metres between neighbouring samples.</summary>
    public float Spacing { get; }

    /// <summary>World-space coordinate of the last sample row/column: <c>(Resolution - 1) * Spacing</c>.</summary>
    public float MaxCoordinate => (Resolution - 1) * Spacing;

    /// <summary>Side length of the area covered by the field, in metres.</summary>
    public float WorldSize => Resolution * Spacing;

    /// <summary>Heights in metres, row-major.</summary>
    public ReadOnlySpan<float> Heights => _heights;

    /// <summary>Normalised moisture, row-major.</summary>
    public ReadOnlySpan<float> Moisture => _moisture;

    /// <summary>Normalised temperature, row-major.</summary>
    public ReadOnlySpan<float> Temperature => _temperature;

    /// <summary>Biome per sample, row-major.</summary>
    public ReadOnlySpan<Biome> Biomes => _biomes;

    /// <summary>The rivers and lakes of the field (ADR-022).</summary>
    public HydrologyMap Hydrology { get; }

    /// <summary>Whether fresh water stands over a sample: a river or a lake.</summary>
    /// <param name="x">Sample column.</param>
    /// <param name="z">Sample row.</param>
    /// <returns><c>true</c> under a river or lake.</returns>
    public bool IsUnderFreshWater(int x, int z)
    {
        var index = IndexOf(x, z);
        var surface = Hydrology.WaterSurface[index];
        return !float.IsNaN(surface) && surface > _heights[index];
    }

    /// <summary>Whether fresh water stands at a world position (the nearest sample).</summary>
    /// <param name="worldX">World X in metres.</param>
    /// <param name="worldZ">World Z in metres.</param>
    /// <returns><c>true</c> in a river or lake.</returns>
    public bool IsUnderFreshWater(float worldX, float worldZ) =>
        IsUnderFreshWater((int)MathF.Round(worldX / Spacing), (int)MathF.Round(worldZ / Spacing));

    /// <summary>Whether fresh water stands within a distance of a world position (the samples in a square around it).</summary>
    /// <param name="worldX">World X in metres.</param>
    /// <param name="worldZ">World Z in metres.</param>
    /// <param name="distance">Distance in metres.</param>
    /// <returns><c>true</c> when a river or lake is that close.</returns>
    public bool IsNearFreshWater(float worldX, float worldZ, float distance)
    {
        if (Hydrology.Rivers.Count == 0 && Hydrology.Lakes.Count == 0)
        {
            return false;
        }

        var reach = Math.Max(0, (int)MathF.Ceiling(distance / Spacing));
        var centreX = (int)MathF.Round(worldX / Spacing);
        var centreZ = (int)MathF.Round(worldZ / Spacing);
        for (var z = centreZ - reach; z <= centreZ + reach; z++)
        {
            for (var x = centreX - reach; x <= centreX + reach; x++)
            {
                if (IsUnderFreshWater(x, z))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Height of a sample, with coordinates clamped to the field.</summary>
    /// <param name="x">Sample column.</param>
    /// <param name="z">Sample row.</param>
    /// <returns>Height in metres.</returns>
    public float GetHeight(int x, int z) => _heights[IndexOf(x, z)];

    /// <summary>Biome of a sample, with coordinates clamped to the field.</summary>
    /// <param name="x">Sample column.</param>
    /// <param name="z">Sample row.</param>
    /// <returns>The sample's biome.</returns>
    public Biome GetBiome(int x, int z) => _biomes[IndexOf(x, z)];

    /// <summary>Bilinearly interpolated height at a world position (clamped to the field).</summary>
    /// <param name="worldX">World X in metres.</param>
    /// <param name="worldZ">World Z in metres.</param>
    /// <returns>Height in metres.</returns>
    public float SampleHeight(float worldX, float worldZ)
    {
        var fx = Math.Clamp(worldX / Spacing, 0f, Resolution - 1);
        var fz = Math.Clamp(worldZ / Spacing, 0f, Resolution - 1);
        var x0 = (int)fx;
        var z0 = (int)fz;
        var tx = fx - x0;
        var tz = fz - z0;

        var top = SimMath.Lerp(GetHeight(x0, z0), GetHeight(x0 + 1, z0), tx);
        var bottom = SimMath.Lerp(GetHeight(x0, z0 + 1), GetHeight(x0 + 1, z0 + 1), tx);
        return SimMath.Lerp(top, bottom, tz);
    }

    /// <summary>Terrain steepness at a sample as rise over run (0 = flat, 1 = 45 degrees).</summary>
    /// <param name="x">Sample column.</param>
    /// <param name="z">Sample row.</param>
    /// <returns>Gradient magnitude from central differences.</returns>
    public float GetSlope(int x, int z)
    {
        var dx = (GetHeight(x + 1, z) - GetHeight(x - 1, z)) / (2f * Spacing);
        var dz = (GetHeight(x, z + 1) - GetHeight(x, z - 1)) / (2f * Spacing);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>Row-major index of a sample after clamping both coordinates into the field.</summary>
    private int IndexOf(int x, int z)
    {
        var cx = Math.Clamp(x, 0, Resolution - 1);
        var cz = Math.Clamp(z, 0, Resolution - 1);
        return (cz * Resolution) + cx;
    }

    /// <summary>Validates that a sample array has exactly one entry per sample.</summary>
    private static void EnsureLength<T>(T[] values, int expected, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        if (values.Length != expected)
        {
            throw new ArgumentException($"Expected {expected} samples but got {values.Length}.", name);
        }
    }
}
