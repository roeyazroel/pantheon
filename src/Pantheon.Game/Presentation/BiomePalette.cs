using System;
using Pantheon.Domain.Ecology;
using Pantheon.Persistence;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Presentation colours and thresholds, loaded from <c>game/Data/biome_palette.json</c>. Only
/// visuals use it; the simulation never does.
/// </summary>
public sealed record BiomePalette
{
    /// <summary>File name of the palette content file.</summary>
    public const string FileName = "biome_palette.json";

    /// <summary>Grassland ground colour.</summary>
    public required RgbColor Grassland { get; init; }

    /// <summary>Forest floor colour.</summary>
    public required RgbColor Forest { get; init; }

    /// <summary>Highland ground colour.</summary>
    public required RgbColor Highland { get; init; }

    /// <summary>Desert sand colour (ADR-038).</summary>
    public required RgbColor Desert { get; init; }

    /// <summary>Tundra moss colour (ADR-038).</summary>
    public required RgbColor Tundra { get; init; }

    /// <summary>Swamp colour (ADR-038).</summary>
    public required RgbColor Swamp { get; init; }

    /// <summary>Coastal dune grass colour (ADR-038).</summary>
    public required RgbColor Coast { get; init; }

    /// <summary>River valley colour (ADR-038).</summary>
    public required RgbColor RiverValley { get; init; }

    /// <summary>Island grass colour (ADR-038).</summary>
    public required RgbColor Island { get; init; }

    /// <summary>Volcanic ash colour (ADR-038).</summary>
    public required RgbColor Volcanic { get; init; }

    /// <summary>Colour of deep sea floor.</summary>
    public required RgbColor SeaFloor { get; init; }

    /// <summary>Sand colour of beaches and the shallow sea floor.</summary>
    public required RgbColor Beach { get; init; }

    /// <summary>Colour of steep slopes.</summary>
    public required RgbColor Rock { get; init; }

    /// <summary>Colour of snow on the highest peaks.</summary>
    public required RgbColor Snow { get; init; }

    /// <summary>Water surface colour where the sea is shallow.</summary>
    public required RgbColor ShallowWater { get; init; }

    /// <summary>Water surface colour of the open sea.</summary>
    public required RgbColor DeepWater { get; init; }

    /// <summary>Colour of region boundary lines.</summary>
    public required RgbColor RegionBoundary { get; init; }

    /// <summary>Colour of the outline around the region under the cursor.</summary>
    public required RgbColor RegionHighlight { get; init; }

    /// <summary>Land below this height (metres) is drawn as beach.</summary>
    public required float BeachMaxHeight { get; init; }

    /// <summary>Snow starts at this height (metres).</summary>
    public required float SnowStartHeight { get; init; }

    /// <summary>Height range (metres) over which snow fades in.</summary>
    public required float SnowBlendRange { get; init; }

    /// <summary>Slope (rise over run) at which rock starts to show.</summary>
    public required float RockSlopeStart { get; init; }

    /// <summary>Slope at which the ground is fully rock.</summary>
    public required float RockSlopeFull { get; init; }

    /// <summary>Water depth (metres) at which the sea floor reaches <see cref="SeaFloor"/>.</summary>
    public required float DeepWaterDepth { get; init; }

    /// <summary>Metres over which neighbouring land biomes' colours blend into each other (ADR-038); 0 draws hard borders.</summary>
    public float BiomeBlendMetres { get; init; }

    /// <summary>Strength of the per-sample brightness variation (0 = flat colour).</summary>
    public required float ColorVariation { get; init; }

    /// <summary>How the ground is textured (ADR-051).</summary>
    public required GroundSettings Ground { get; init; }

    /// <summary>Parses and validates the palette content file.</summary>
    /// <param name="json">Text of <c>biome_palette.json</c>.</param>
    /// <returns>The validated palette.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or holds out-of-range values.</exception>
    public static BiomePalette Parse(string json)
    {
        var palette = ContentJson.Parse<BiomePalette>(json, FileName);
        try
        {
            palette.Validate();
        }
        catch (ArgumentException exception)
        {
            throw new ContentFormatException($"{FileName}: {exception.Message}", exception);
        }

        return palette;
    }

    /// <summary>Base ground colour of a biome.</summary>
    /// <param name="biome">Biome.</param>
    /// <returns>The biome's colour.</returns>
    public RgbColor GetBiomeColor(Biome biome) => biome switch
    {
        Biome.Sea => SeaFloor,
        Biome.Grassland => Grassland,
        Biome.Forest => Forest,
        Biome.Highland => Highland,
        Biome.Desert => Desert,
        Biome.Tundra => Tundra,
        Biome.Swamp => Swamp,
        Biome.Coast => Coast,
        Biome.RiverValley => RiverValley,
        Biome.Island => Island,
        Biome.Volcanic => Volcanic,
    };

    /// <summary>Throws when a threshold is out of range.</summary>
    /// <exception cref="ArgumentException">A threshold is invalid.</exception>
    public void Validate()
    {
        Ground.Validate();
        if (!(BeachMaxHeight > 0f) || !(SnowBlendRange > 0f) || !(DeepWaterDepth > 0f))
        {
            throw new ArgumentException("BeachMaxHeight, SnowBlendRange and DeepWaterDepth must be positive.");
        }

        if (!(RockSlopeStart >= 0f && RockSlopeStart < RockSlopeFull))
        {
            throw new ArgumentException("RockSlopeStart must be non-negative and below RockSlopeFull.");
        }

        if (!(BiomeBlendMetres is >= 0f and <= 64f))
        {
            throw new ArgumentException("BiomeBlendMetres must lie between 0 and 64.");
        }

        if (!(ColorVariation is >= 0f and <= 0.5f))
        {
            throw new ArgumentException("ColorVariation must lie between 0 and 0.5.");
        }
    }
}
