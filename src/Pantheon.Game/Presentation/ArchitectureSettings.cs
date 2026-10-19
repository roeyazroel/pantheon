using System;
using System.Collections.Generic;

namespace Pantheon.Game.Presentation;

/// <summary>The colours of one house style: a village picks one of its walls and one of its roofs.</summary>
public sealed record StyleColors
{
    /// <summary>Wall colours.</summary>
    public required IReadOnlyList<RgbColor> Walls { get; init; }

    /// <summary>Roof colours.</summary>
    public required IReadOnlyList<RgbColor> Roofs { get; init; }
}

/// <summary>How villages build (spec §61, ADR-052), from <c>settlement_view.json</c>. Presentation only.</summary>
public sealed record ArchitectureSettings
{
    /// <summary>Each style's colours.</summary>
    public required IReadOnlyDictionary<HouseStyle, StyleColors> Styles { get; init; }

    /// <summary>Timber: posts, beams, stakes.</summary>
    public required RgbColor Trim { get; init; }

    /// <summary>Doors.</summary>
    public required RgbColor Door { get; init; }

    /// <summary>Windows.</summary>
    public required RgbColor Window { get; init; }

    /// <summary>Stone footings, chimneys and walls.</summary>
    public required RgbColor Stone { get; init; }

    /// <summary>Turf roofs of villages whose god is loved for nature and growth.</summary>
    public required RgbColor GardenRoof { get; init; }

    /// <summary>Dark roofs of villages that fear a god of war and death.</summary>
    public required RgbColor MonumentRoof { get; init; }

    /// <summary>Dark timber of those villages.</summary>
    public required RgbColor MonumentWall { get; init; }

    /// <summary>Whitewash of villages whose god is one of learning.</summary>
    public required RgbColor LoreWall { get; init; }

    /// <summary>Their blue roofs.</summary>
    public required RgbColor LoreRoof { get; init; }

    /// <summary>How strongly a god's style must show (0..1) before the houses take it.</summary>
    public required float MotifStrength { get; init; }

    /// <summary>Houses at which a village of round huts builds longhouses.</summary>
    public required int LonghouseHouses { get; init; }

    /// <summary>Houses at which a village builds a second storey.</summary>
    public required int TwoStoreyHouses { get; init; }

    /// <summary>How far each house's colours vary from the village's, as a share of their brightness.</summary>
    public required float HouseVariation { get; init; }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        foreach (var style in Enum.GetValues<HouseStyle>())
        {
            if (!Styles.TryGetValue(style, out var colors) || colors.Walls.Count == 0 || colors.Roofs.Count == 0)
            {
                throw new ArgumentException($"Architecture.Styles needs wall and roof colours for {style}.");
            }
        }

        if (!(MotifStrength is >= 0f and <= 1f) || LonghouseHouses < 1 || TwoStoreyHouses < 1 || !(HouseVariation is >= 0f and <= 0.5f))
        {
            throw new ArgumentException("Architecture needs MotifStrength in 0..1, positive house counts and HouseVariation in 0..0.5.");
        }
    }
}
