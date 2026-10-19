using System;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Noise;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.WorldGen;

/// <summary>
/// Lays the forests and stone on the land (spec §93, ADR-033): each region's forest is its biome's
/// factor times its vegetation, and its stone its biome's, both varied in patches of noise drawn from
/// the seed. The forests start whole.
/// </summary>
public static class ResourceFounder
{
    /// <summary>Purpose name of the forests' noise seed.</summary>
    public const string ForestNoise = "forests";

    /// <summary>Purpose name of the stone's noise seed.</summary>
    public const string StoneNoise = "stone";

    /// <summary>Sets every region's forest, timber and stone.</summary>
    /// <param name="regions">Region grid.</param>
    /// <param name="seed">World seed.</param>
    /// <param name="settings">Resource settings; <c>null</c> lays none.</param>
    public static void Found(RegionGrid regions, WorldSeed seed, ResourceSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(regions);
        if (settings is null)
        {
            return;
        }

        var frequency = 1f / Math.Max(1f, settings.PatchSize);
        var forests = Layer(seed, ForestNoise, frequency);
        var stones = Layer(seed, StoneNoise, frequency);
        foreach (var region in regions.Cells)
        {
            var traits = Find(settings, region.Biome);
            if (traits is null)
            {
                continue;
            }

            var (column, row) = regions.GetCoordinates(region.Id);
            var (x, z) = ((column + 0.5f) * regions.RegionSize, (row + 0.5f) * regions.RegionSize);
            region.Forest = SimMath.Clamp01(traits.Forest * region.Vegetation * Patch(settings, forests.GetNoise(x, z)));
            region.Timber = region.Forest;
            region.Stone = SimMath.Clamp01(traits.Stone * Patch(settings, stones.GetNoise(x, z)));
        }
    }

    /// <summary>A patch's factor from noise in -1..1.</summary>
    private static float Patch(ResourceSettings settings, float noise) => Math.Max(0f, 1f + (settings.PatchStrength * noise));

    /// <summary>A biome's traits, if listed.</summary>
    private static BiomeResources? Find(ResourceSettings settings, Domain.Ecology.Biome biome)
    {
        foreach (var traits in settings.Biomes)
        {
            if (traits.Biome == biome)
            {
                return traits;
            }
        }

        return null;
    }

    /// <summary>A smooth noise layer seeded for a purpose.</summary>
    private static FastNoiseLite Layer(WorldSeed seed, string purpose, float frequency)
    {
        var noise = new FastNoiseLite(SeedMixer.DeriveNoiseSeed(seed, purpose));
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        noise.SetFractalOctaves(2);
        return noise;
    }
}
