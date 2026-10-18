using Pantheon.Persistence;

namespace Pantheon.Game.Presentation;

/// <summary>Validated presentation content loaded from <c>game/Data</c>. The simulation never reads it.</summary>
/// <param name="Palette">Terrain, water and overlay colours.</param>
/// <param name="SettlementView">How settlements are drawn.</param>
public sealed record PresentationContent(BiomePalette Palette, SettlementViewSettings SettlementView)
{
    /// <summary>Parses and validates the presentation content files.</summary>
    /// <param name="paletteJson">Text of <c>biome_palette.json</c>.</param>
    /// <param name="settlementViewJson">Text of <c>settlement_view.json</c>.</param>
    /// <returns>The validated content.</returns>
    /// <exception cref="ContentFormatException">A file is malformed or holds out-of-range values.</exception>
    public static PresentationContent Parse(string paletteJson, string settlementViewJson) =>
        new(BiomePalette.Parse(paletteJson), SettlementViewSettings.Parse(settlementViewJson));
}
