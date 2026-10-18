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

    /// <summary>Records <paramref name="message"/> when <paramref name="condition"/> is false.</summary>
    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition && !errors.Contains(message))
        {
            errors.Add(message);
        }
    }
}
