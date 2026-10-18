using Pantheon.Persistence;

namespace Pantheon.Game.Presentation;

/// <summary>Validated presentation content loaded from <c>game/Data</c>. The simulation never reads it.</summary>
/// <param name="Palette">Terrain, water and overlay colours.</param>
/// <param name="SettlementView">How settlements are drawn.</param>
/// <param name="DivineView">God colours, overlays and miracle effects.</param>
/// <param name="WeatherView">How clouds, rain and lightning are drawn.</param>
public sealed record PresentationContent(BiomePalette Palette, SettlementViewSettings SettlementView, DivineViewSettings DivineView, WeatherViewSettings WeatherView)
{
    /// <summary>Parses and validates the presentation content files.</summary>
    /// <param name="paletteJson">Text of <c>biome_palette.json</c>.</param>
    /// <param name="settlementViewJson">Text of <c>settlement_view.json</c>.</param>
    /// <param name="divineViewJson">Text of <c>divine_view.json</c>.</param>
    /// <param name="weatherViewJson">Text of <c>weather_view.json</c>.</param>
    /// <returns>The validated content.</returns>
    /// <exception cref="ContentFormatException">A file is malformed or holds out-of-range values.</exception>
    public static PresentationContent Parse(string paletteJson, string settlementViewJson, string divineViewJson, string weatherViewJson) =>
        new(BiomePalette.Parse(paletteJson), SettlementViewSettings.Parse(settlementViewJson), DivineViewSettings.Parse(divineViewJson), WeatherViewSettings.Parse(weatherViewJson));
}
