using System;
using System.Collections.Generic;
using Pantheon.Domain.World.Generation;

namespace Pantheon.Simulation.WorldGen;

/// <summary>
/// Validates data-driven <see cref="WorldGenSettings"/> before generation, so content mistakes
/// surface as one readable error instead of a broken world. Biome rules are validated by
/// <see cref="BiomeClassifier"/>.
/// </summary>
public static class WorldGenSettingsValidator
{
    private const int MaxOctaves = 12;

    /// <summary>Throws when any setting is out of range.</summary>
    /// <param name="settings">Settings to check.</param>
    /// <exception cref="ArgumentException">One or more settings are invalid; the message lists them all.</exception>
    public static void Validate(WorldGenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<string>();

        ValidateGrid(settings, errors);
        ValidateShape(settings.Shape, errors);
        ValidateNoise(nameof(settings.ElevationNoise), settings.ElevationNoise, errors);
        ValidateNoise(nameof(settings.RidgeNoise), settings.RidgeNoise, errors);
        ValidateNoise(nameof(settings.MoistureNoise), settings.MoistureNoise, errors);
        ValidateNoise(nameof(settings.TemperatureNoise), settings.TemperatureNoise, errors);
        Require(settings.Climate.CoastalMoistureRange > 0f, "Climate.CoastalMoistureRange must be positive.", errors);
        ValidateStartingSettlement(settings.StartingSettlement, errors);
        if (settings.Hydrology is { } hydrology)
        {
            ValidateHydrology(settings, hydrology, errors);
        }

        if (settings.Resources is { } resources)
        {
            ValidateResources(resources, errors);
        }

        if (settings.Landforms is { } landforms)
        {
            ValidateLandforms(settings, landforms, errors);
        }

        if (errors.Count > 0)
        {
            throw new ArgumentException("Invalid world generation settings:" + Environment.NewLine + " - " +
                string.Join(Environment.NewLine + " - ", errors), nameof(settings));
        }
    }

    /// <summary>Checks heightfield and region grid dimensions.</summary>
    private static void ValidateGrid(WorldGenSettings settings, List<string> errors)
    {
        Require(settings.HeightmapResolution >= 2, "HeightmapResolution must be at least 2.", errors);
        Require(settings.VertexSpacing > 0f, "VertexSpacing must be positive.", errors);
        Require(settings.RegionsPerSide >= 1, "RegionsPerSide must be at least 1.", errors);
        if (settings.RegionsPerSide >= 1)
        {
            Require(settings.HeightmapResolution % settings.RegionsPerSide == 0,
                "HeightmapResolution must be divisible by RegionsPerSide.", errors);
        }
    }

    /// <summary>Checks the island mask and elevation mapping.</summary>
    private static void ValidateShape(TerrainShapeSettings shape, List<string> errors)
    {
        Require(shape.IslandFalloffStart < shape.IslandFalloffEnd, "Shape.IslandFalloffStart must be below IslandFalloffEnd.", errors);
        Require(shape.RidgeMaskStart < shape.RidgeMaskEnd, "Shape.RidgeMaskStart must be below RidgeMaskEnd.", errors);
        Require(shape.RidgeWeight >= 0f, "Shape.RidgeWeight must not be negative.", errors);
        Require(shape.SeaLevel > 0f && shape.SeaLevel < 1f, "Shape.SeaLevel must lie strictly between 0 and 1.", errors);
        Require(shape.LandExponent > 0f, "Shape.LandExponent must be positive.", errors);
        Require(shape.MaxElevation > 0f, "Shape.MaxElevation must be positive.", errors);
        Require(shape.MaxDepth > 0f, "Shape.MaxDepth must be positive.", errors);
    }

    /// <summary>Checks one noise layer's fractal parameters.</summary>
    private static void ValidateNoise(string name, NoiseLayerSettings noise, List<string> errors)
    {
        Require(noise.Frequency > 0f, $"{name}.Frequency must be positive.", errors);
        Require(noise.Octaves is >= 1 and <= MaxOctaves, $"{name}.Octaves must be between 1 and {MaxOctaves}.", errors);
        Require(noise.Lacunarity > 0f, $"{name}.Lacunarity must be positive.", errors);
        Require(noise.Gain > 0f, $"{name}.Gain must be positive.", errors);
    }

    /// <summary>Checks the starting settlement parameters.</summary>
    private static void ValidateStartingSettlement(StartingSettlementSettings settlement, List<string> errors)
    {
        Require(settlement.Count >= 1, "StartingSettlement.Count must be at least 1.", errors);
        Require(settlement.MinimumSpacing >= 1, "StartingSettlement.MinimumSpacing must be at least 1.", errors);
        Require(settlement.Population >= 1, "StartingSettlement.Population must be at least 1.", errors);
        Require(settlement.PopulationVariation is >= 0f and < 1f, "StartingSettlement.PopulationVariation must be at least 0 and below 1.", errors);
        Require(
            settlement.Housing >= MathF.Ceiling(settlement.Population * (1f + settlement.PopulationVariation)),
            "StartingSettlement.Housing must house the largest starting population.",
            errors);
        Require(settlement.Food >= 0f, "StartingSettlement.Food must not be negative.", errors);
        Require(settlement.Water >= 0f, "StartingSettlement.Water must not be negative.", errors);
        Require(settlement.WaterPreference >= 0f, "StartingSettlement.WaterPreference must not be negative.", errors);
        Require(settlement.CenterPreference >= 0f, "StartingSettlement.CenterPreference must not be negative.", errors);
        Require(settlement.Names.Count >= settlement.Count, "StartingSettlement.Names must hold a name for every settlement.", errors);
        Require(
            new HashSet<string>(settlement.Names, StringComparer.Ordinal).Count == settlement.Names.Count,
            "StartingSettlement.Names must not repeat a name.",
            errors);
        foreach (var name in settlement.Names)
        {
            Require(!string.IsNullOrWhiteSpace(name), "StartingSettlement.Names must not contain blank names.", errors);
        }
    }

    /// <summary>
    /// Checks the landforms (ADR-038): distances and areas not negative; traits for coast, river
    /// valley, island and volcanic land and for nothing else; no climate rule for them; and coast and
    /// valleys replacing only land.
    /// </summary>
    private static void ValidateLandforms(WorldGenSettings settings, LandformSettings landforms, List<string> errors)
    {
        Domain.Ecology.Biome[] kinds = [Domain.Ecology.Biome.Coast, Domain.Ecology.Biome.RiverValley, Domain.Ecology.Biome.Island, Domain.Ecology.Biome.Volcanic];
        Require(landforms.IslandMaxArea >= 0f && landforms.CoastMetres >= 0f && landforms.CoastMaxHeight >= 0f && landforms.ValleyMetres >= 0f
            && landforms.ValleyMaxHeight >= 0f && landforms.VolcanoMinElevation >= 0f && landforms.VolcanicMetres >= 0f,
            "Landforms areas, distances and heights must not be negative.", errors);
        var traits = landforms.Traits ?? [];
        Require(traits.Count == kinds.Length && Array.TrueForAll(kinds, k => System.Linq.Enumerable.Count(traits, t => t is not null && t.Biome == k) == 1),
            "Landforms.Traits must list Coast, RiverValley, Island and Volcanic, each once.", errors);
        foreach (var trait in traits)
        {
            Require(trait is null || (trait.BaseFertility is >= 0f and <= 1f && trait.BaseVegetation is >= 0f and <= 1f),
                "Landforms.Traits fertility and vegetation must be between 0 and 1.", errors);
        }

        Require(!System.Linq.Enumerable.Any(settings.Biomes, rule => rule is not null && Array.IndexOf(kinds, rule.Biome) >= 0),
            "Biomes must not give climate rules to the landforms Coast, RiverValley, Island or Volcanic.", errors);
        foreach (var (name, over) in new[] { ("CoastOver", landforms.CoastOver), ("ValleyOver", landforms.ValleyOver) })
        {
            Require(over is not null && System.Linq.Enumerable.All(over, b => Enum.IsDefined(b) && b != Domain.Ecology.Biome.Sea),
                $"Landforms.{name} must list known land biomes.", errors);
        }
    }

    /// <summary>Checks the forests' and stone's settings (ADR-033): each biome once, values not negative.</summary>
    private static void ValidateResources(ResourceSettings resources, List<string> errors)
    {
        Require(resources.PatchSize > 0f, "Resources.PatchSize must be positive.", errors);
        Require(resources.PatchStrength is >= 0f and <= 1f, "Resources.PatchStrength must be between 0 and 1.", errors);
        var seen = new HashSet<Domain.Ecology.Biome>();
        foreach (var biome in resources.Biomes)
        {
            Require(biome is not null && Enum.IsDefined(biome.Biome) && seen.Add(biome.Biome), "Resources.Biomes must list known biomes, each once.", errors);
            Require(biome is null || (biome.Forest >= 0f && biome.Stone is >= 0f and <= 1f), "Resources.Biomes forest must not be negative, and stone must be between 0 and 1.", errors);
        }
    }

    /// <summary>Checks the rivers' and lakes' settings (ADR-022).</summary>
    private static void ValidateHydrology(WorldGenSettings settings, HydrologySettings hydrology, List<string> errors)
    {
        Require(hydrology.CellSize > 0f, "Hydrology.CellSize must be positive.", errors);
        if (hydrology.CellSize > 0f && settings.VertexSpacing > 0f)
        {
            var factor = HydrologyGenerator.CellFactor(hydrology, settings.VertexSpacing);
            Require(settings.HeightmapResolution % factor == 0 && settings.HeightmapResolution / factor >= 3,
                "Hydrology.CellSize must divide the heightfield into whole cells, at least three per side.", errors);
        }

        Require(hydrology.RiverCatchment > 0f, "Hydrology.RiverCatchment must be positive.", errors);
        Require(hydrology.HeadwaterCatchment > 0f && hydrology.HeadwaterCatchment <= hydrology.RiverCatchment,
            "Hydrology.HeadwaterCatchment must be positive and at most RiverCatchment.", errors);
        Require(hydrology.MaxRivers >= 0, "Hydrology.MaxRivers must not be negative.", errors);
        Require(hydrology.MaxMainRivers >= 0 && hydrology.MaxMainRivers <= hydrology.MaxRivers, "Hydrology.MaxMainRivers must be between 0 and MaxRivers.", errors);
        Require(hydrology.MinRiverLength >= 0f, "Hydrology.MinRiverLength must not be negative.", errors);
        Require(hydrology.MinChannelWidth > 0f && hydrology.MaxChannelWidth >= hydrology.MinChannelWidth,
            "Hydrology channel widths must be positive, the largest at least the smallest.", errors);
        Require(hydrology.MinChannelDepth > 0f && hydrology.MaxChannelDepth >= hydrology.MinChannelDepth,
            "Hydrology channel depths must be positive, the largest at least the smallest.", errors);
        Require(hydrology.BankWidth > 0f, "Hydrology.BankWidth must be positive.", errors);
        Require(hydrology.Freeboard >= 0f, "Hydrology.Freeboard must not be negative.", errors);
        Require(hydrology.SmoothingPasses >= 0, "Hydrology.SmoothingPasses must not be negative.", errors);
        Require(hydrology.MinLakeDepth > 0f, "Hydrology.MinLakeDepth must be positive.", errors);
        Require(hydrology.MinLakeArea > 0f, "Hydrology.MinLakeArea must be positive.", errors);
        Require(hydrology.FullWaterShare is > 0f and <= 1f, "Hydrology.FullWaterShare must be above 0 and at most 1.", errors);
        Require(hydrology.RiverNames.Count >= Math.Max(1, hydrology.MaxRivers), "Hydrology.RiverNames must hold a name for every river.", errors);
        Require(hydrology.LakeNames.Count >= 1, "Hydrology.LakeNames must hold at least one name.", errors);
        foreach (var name in hydrology.RiverNames)
        {
            Require(!string.IsNullOrWhiteSpace(name), "Hydrology names must not be blank.", errors);
        }

        foreach (var name in hydrology.LakeNames)
        {
            Require(!string.IsNullOrWhiteSpace(name), "Hydrology names must not be blank.", errors);
        }
    }

    /// <summary>Records <paramref name="message"/> when <paramref name="condition"/> is false.</summary>
    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition && !errors.Contains(message))
        {
            errors.Add(message);
        }
    }
}
