using System;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Game;

/// <summary>How a new world is made: the player's Creature species, its size and how it may be won (ADR-027).</summary>
/// <param name="Species">The species of the player's Creature; <c>gods.json</c>'s when <c>null</c>.</param>
/// <param name="Size">The world size's name; the first (the world <c>worldgen.json</c> describes) when <c>null</c>.</param>
/// <param name="Victory">How it may be won; ascendancy only when <c>null</c>.</param>
/// <param name="Seasons">Whether the year turns through four seasons (spec §17, ADR-028).</param>
public sealed record NewWorldOptions(string? Species = null, string? Size = null, VictorySettings? Victory = null, bool Seasons = true);

/// <summary>Makes new worlds at a size, with their gods and victories (spec §6, §53, ADR-027).</summary>
public static class WorldMaker
{
    /// <summary>Generates a world.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="options">How to make it.</param>
    /// <returns>The world and its terrain.</returns>
    public static GeneratedWorld Generate(GameContent content, WorldSeed seed, NewWorldOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);
        var (worldGen, pantheon) = AtSize(content, options.Size);
        if (options.Species is { } species && pantheon.Gods.ToList().FindIndex(g => g.Controller == GodController.Human) is var player and >= 0)
        {
            // Every god keeps an animal of its own (ADR-061): a rival with the animal the player chose takes the player's.
            var gods = pantheon.Gods.ToList();
            var given = gods[player].CreatureSpecies;
            for (var i = 0; i < gods.Count; i++)
            {
                if (i != player && string.Equals(gods[i].CreatureSpecies, species, StringComparison.Ordinal))
                {
                    gods[i] = gods[i] with { CreatureSpecies = given };
                }
            }

            gods[player] = gods[player] with { CreatureSpecies = species };
            pantheon = pantheon with { Gods = gods };
        }

        var generated = WorldGenerator.Generate(seed, worldGen, pantheon, content.Creatures, content.HolySites);
        var victory = options.Victory ?? VictorySettings.Default;
        var preset = Find(content, options.Size) ?? (content.Sizes.Sizes.Count > 0 ? content.Sizes.Sizes[0] : null);
        generated.State.Victory = victory with { PantheonMaxGods = victory.PantheonMaxGods ?? preset?.PantheonMaxGods };
        generated.State.Seasons = options.Seasons ? new Domain.Weather.SeasonState { OriginTick = 0 } : null;
        return generated;
    }

    /// <summary>A world size in words.</summary>
    /// <param name="preset">The size.</param>
    /// <returns>For example "4 km across, 20 villages and 6 gods".</returns>
    public static string Describe(WorldSizePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:0} km across, {1} villages and {2} gods",
            preset.HeightmapResolution * preset.VertexSpacing / 1000f,
            preset.Settlements,
            preset.Gods);
    }

    /// <summary>The size preset of a name, if the content offers it.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="size">The name, ignoring case.</param>
    /// <returns>The preset, or <c>null</c>.</returns>
    public static WorldSizePreset? Find(GameContent content, string? size)
    {
        ArgumentNullException.ThrowIfNull(content);
        return size is null ? null : content.Sizes.Sizes.FirstOrDefault(s => string.Equals(s.Name, size, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The generation settings and gods of a world size: the content's own for the first size or
    /// none; for a larger one, the island, regions, settlements (with the extra names), rivers
    /// (with the extra names) and gods (with the extra gods) it names.
    /// </summary>
    /// <param name="content">Validated content.</param>
    /// <param name="size">The size's name.</param>
    /// <returns>The settings and gods.</returns>
    public static (WorldGenSettings WorldGen, PantheonSetup Pantheon) AtSize(GameContent content, string? size)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Find(content, size) is { } preset && !ReferenceEquals(preset, content.Sizes.Sizes[0])
            ? AtSize(content.Sizes, preset, content.WorldGen, content.Pantheon)
            : (content.WorldGen, content.Pantheon);
    }

    /// <summary>The generation settings and gods of a size preset, from the default ones.</summary>
    private static (WorldGenSettings WorldGen, PantheonSetup Pantheon) AtSize(WorldSizes sizes, WorldSizePreset preset, WorldGenSettings defaults, PantheonSetup gods)
    {
        var settlement = defaults.StartingSettlement;
        var worldGen = defaults with
        {
            HeightmapResolution = preset.HeightmapResolution,
            VertexSpacing = preset.VertexSpacing,
            RegionsPerSide = preset.RegionsPerSide,
            StartingSettlement = settlement with { Count = preset.Settlements, Names = [.. settlement.Names, .. sizes.ExtraSettlementNames] },
            Hydrology = defaults.Hydrology is { } hydrology
                ? hydrology with { MaxRivers = preset.Rivers, MaxMainRivers = preset.MainRivers, CellSize = preset.HydrologyCellSize, RiverNames = [.. hydrology.RiverNames, .. sizes.ExtraRiverNames] }
                : null,
        };
        var pantheon = gods with { Gods = [.. gods.Gods, .. gods.ExtraGods.Take(Math.Max(0, preset.Gods - gods.Gods.Count))] };
        return (worldGen, pantheon);
    }

    /// <summary>Checks that every size can be made: the first as the content describes it, valid generation settings, names enough, gods enough, distinct names.</summary>
    /// <param name="sizes">The sizes.</param>
    /// <param name="worldGen">The default generation settings.</param>
    /// <param name="pantheon">The gods.</param>
    /// <exception cref="ArgumentException">A size cannot be made.</exception>
    public static void Validate(WorldSizes sizes, WorldGenSettings worldGen, PantheonSetup pantheon)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        ArgumentNullException.ThrowIfNull(worldGen);
        ArgumentNullException.ThrowIfNull(pantheon);
        if (sizes.Sizes.Select(s => s.Name.ToUpperInvariant()).Distinct().Count() != sizes.Sizes.Count)
        {
            throw new ArgumentException("Every world size needs its own name.");
        }

        for (var i = 0; i < sizes.Sizes.Count; i++)
        {
            var preset = sizes.Sizes[i];
            if (preset.Gods < 2 || preset.Gods > pantheon.Gods.Count + pantheon.ExtraGods.Count)
            {
                throw new ArgumentException($"World size {preset.Name} needs between 2 and {pantheon.Gods.Count + pantheon.ExtraGods.Count} gods.");
            }

            if (preset.PantheonMaxGods < 2 || preset.PantheonMaxGods > preset.Gods)
            {
                throw new ArgumentException($"World size {preset.Name} lets between 2 and its {preset.Gods} gods win together.");
            }

            if (i == 0 && (preset.HeightmapResolution != worldGen.HeightmapResolution || preset.VertexSpacing != worldGen.VertexSpacing
                || preset.RegionsPerSide != worldGen.RegionsPerSide || preset.Settlements != worldGen.StartingSettlement.Count || preset.Gods != pantheon.Gods.Count))
            {
                throw new ArgumentException($"The first world size, {preset.Name}, must describe the world worldgen.json and gods.json make.");
            }

            var (atSize, gods) = i == 0 ? (worldGen, pantheon) : AtSize(sizes, preset, worldGen, pantheon);
            WorldGenSettingsValidator.Validate(atSize);
            PantheonFounder.Validate(gods);
        }
    }
}
