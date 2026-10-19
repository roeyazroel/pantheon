using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.WorldGen;

/// <summary>Aggregates terrain samples into the region grid stored in <see cref="WorldState"/>.</summary>
public static class RegionBuilder
{
    /// <summary>Fertility of completely dry land relative to saturated land.</summary>
    private const float DryFertilityFactor = 0.5f;

    /// <summary>Fertility of freezing land relative to hot land.</summary>
    private const float ColdFertilityFactor = 0.6f;

    /// <summary>Vegetation cover of completely dry land relative to saturated land.</summary>
    private const float DryVegetationFactor = 0.4f;

    private static readonly int BiomeCount = Enum.GetValues<Biome>().Length;

    /// <summary>Builds every region from the samples it covers.</summary>
    /// <param name="field">Generated terrain.</param>
    /// <param name="settings">Generation settings (grid size and biome traits).</param>
    /// <returns>The populated region grid.</returns>
    public static RegionGrid Build(TerrainField field, WorldGenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(settings);

        var classifier = new BiomeClassifier(settings.Biomes);
        var regionsPerSide = settings.RegionsPerSide;
        var samplesPerRegion = field.Resolution / regionsPerSide;
        var cells = new List<RegionState>(regionsPerSide * regionsPerSide);

        for (var row = 0; row < regionsPerSide; row++)
        {
            for (var column = 0; column < regionsPerSide; column++)
            {
                var id = (row * regionsPerSide) + column;
                cells.Add(BuildRegion(field, classifier, settings.Landforms, id, column * samplesPerRegion, row * samplesPerRegion, samplesPerRegion, settings.Hydrology?.FullWaterShare ?? 1f));
            }
        }

        return new RegionGrid
        {
            Columns = regionsPerSide,
            Rows = regionsPerSide,
            RegionSize = samplesPerRegion * field.Spacing,
            Cells = cells,
        };
    }

    /// <summary>
    /// Averages the climate of one block of samples and derives fertility and vegetation. Ground
    /// under a river or lake is not land; the share of it waters the region (ADR-022).
    /// </summary>
    private static RegionState BuildRegion(TerrainField field, BiomeClassifier classifier, LandformSettings? landforms, int id, int startX, int startZ, int size, float fullWaterShare)
    {
        var heights = field.Heights;
        var moisture = field.Moisture;
        var temperature = field.Temperature;
        var biomes = field.Biomes;

        double heightSum = 0;
        double moistureSum = 0;
        double temperatureSum = 0;
        var landSamples = 0;
        var waterSamples = 0;
        Span<int> biomeCounts = stackalloc int[BiomeCount];

        for (var z = startZ; z < startZ + size; z++)
        {
            for (var x = startX; x < startX + size; x++)
            {
                var index = (z * field.Resolution) + x;
                heightSum += heights[index];
                moistureSum += moisture[index];
                temperatureSum += temperature[index];
                biomeCounts[(int)biomes[index]]++;
                if (field.IsUnderFreshWater(x, z))
                {
                    waterSamples++;
                }
                else if (heights[index] >= 0f)
                {
                    landSamples++;
                }
            }
        }

        var sampleCount = (double)(size * size);
        var meanMoisture = (float)(moistureSum / sampleCount);
        var meanTemperature = (float)(temperatureSum / sampleCount);
        var landFraction = (float)(landSamples / sampleCount);
        var biome = landforms is null ? MajorityBiome(biomeCounts) : MajorityLandBiome(biomeCounts);
        var rule = Traits(classifier, landforms, biome);
        var vegetation = ComputeVegetation(rule, meanMoisture, landFraction);

        return new RegionState
        {
            Id = id,
            Biome = biome,
            Elevation = (float)(heightSum / sampleCount),
            Temperature = meanTemperature,
            Moisture = meanMoisture,
            Fertility = ComputeFertility(rule, meanMoisture, meanTemperature, landFraction),
            Vegetation = vegetation,
            Blessing = 0f,

            // A new world starts after ordinary weather: the soil holds as much water as the climate gives it.
            SoilMoisture = SimMath.Clamp01(meanMoisture),
            Fire = 0f,
            Scorch = 0f,
            FreshWater = SimMath.Clamp01((float)(waterSamples / sampleCount) / fullWaterShare),
            Flood = 0f,

            // The land starts with as many wild animals as its cover holds (ADR-028).
            Wildlife = biome == Biome.Sea ? 0f : SimMath.Clamp01(vegetation),
        };
    }

    /// <summary>
    /// With landforms, the land is split among many biomes, so the sea would win mixed coastal regions:
    /// a region is sea only when most of its samples are sea, and land takes its most common land
    /// biome (ADR-038). Ties go to the lowest enum value.
    /// </summary>
    private static Biome MajorityLandBiome(ReadOnlySpan<int> counts)
    {
        var sea = counts[(int)Biome.Sea];
        var land = 0;
        var best = -1;
        for (var i = 0; i < counts.Length; i++)
        {
            if (i == (int)Biome.Sea)
            {
                continue;
            }

            land += counts[i];
            if (best < 0 || counts[i] > counts[best])
            {
                best = i;
            }
        }

        return LandformMarker.IsLand(sea, land) && best >= 0 && counts[best] > 0 ? (Biome)best : Biome.Sea;
    }

    /// <summary>The base traits of a biome: its climate rule, or its landform traits (ADR-038).</summary>
    /// <param name="classifier">The climate rules.</param>
    /// <param name="landforms">Landform settings, if any.</param>
    /// <param name="biome">The biome.</param>
    /// <returns>A rule with its base fertility and vegetation.</returns>
    public static BiomeRule Traits(BiomeClassifier classifier, LandformSettings? landforms, Biome biome)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        foreach (var traits in landforms?.Traits ?? [])
        {
            if (traits.Biome == biome)
            {
                return new BiomeRule { Biome = biome, BaseFertility = traits.BaseFertility, BaseVegetation = traits.BaseVegetation };
            }
        }

        return classifier.GetRule(biome);
    }

    /// <summary>Most common biome. Ties go to the lowest enum value, which keeps results deterministic.</summary>
    private static Biome MajorityBiome(ReadOnlySpan<int> counts)
    {
        var best = 0;
        for (var i = 1; i < counts.Length; i++)
        {
            if (counts[i] > counts[best])
            {
                best = i;
            }
        }

        return (Biome)best;
    }

    /// <summary>Base biome fertility scaled by moisture, warmth and the share of dry land.</summary>
    private static float ComputeFertility(BiomeRule rule, float moisture, float temperature, float landFraction)
    {
        var moistureFactor = SimMath.Lerp(DryFertilityFactor, 1f, moisture);
        var warmthFactor = SimMath.Lerp(ColdFertilityFactor, 1f, temperature);
        return SimMath.Clamp01(rule.BaseFertility * moistureFactor * warmthFactor * landFraction);
    }

    /// <summary>Base biome vegetation scaled by moisture and the share of dry land.</summary>
    private static float ComputeVegetation(BiomeRule rule, float moisture, float landFraction) =>
        SimMath.Clamp01(rule.BaseVegetation * SimMath.Lerp(DryVegetationFactor, 1f, moisture) * landFraction);
}
