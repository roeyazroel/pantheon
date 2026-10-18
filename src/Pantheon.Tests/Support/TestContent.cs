using System;
using System.IO;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World.Generation;
using Pantheon.Game;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;

namespace Pantheon.Tests.Support;

/// <summary>Loads the shipped <c>game/Data</c> content (copied next to the test assembly) and derived variants.</summary>
internal static class TestContent
{
    private static readonly Lazy<GameContent> ShippedContent = new(LoadShipped);
    private static readonly Lazy<PresentationContent> ShippedPresentationContent = new(LoadPresentation);

    /// <summary>Directory holding the copied content files.</summary>
    public static string DataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>The shipped, validated content.</summary>
    public static GameContent Shipped => ShippedContent.Value;

    /// <summary>The shipped palette.</summary>
    public static BiomePalette Palette => ShippedPresentationContent.Value.Palette;

    /// <summary>The shipped settlement view settings.</summary>
    public static SettlementViewSettings SettlementView => ShippedPresentationContent.Value.SettlementView;

    /// <summary>
    /// Shipped generation settings at a quarter of the resolution. The island keeps its physical
    /// size, so tests run fast on a world shaped like the real one.
    /// </summary>
    public static WorldGenSettings SmallWorldGen => Shipped.WorldGen with
    {
        HeightmapResolution = 256,
        VertexSpacing = 8f,
        RegionsPerSide = 16,
    };

    /// <summary>
    /// <see cref="SmallWorldGen"/> founding one settlement of exactly the starting population, so
    /// tests written for the single settlement of Milestone 2 keep their numbers.
    /// </summary>
    public static WorldGenSettings SingleSettlementWorldGen => SmallWorldGen with
    {
        StartingSettlement = SmallWorldGen.StartingSettlement with { Count = 1, PopulationVariation = 0f },
    };

    /// <summary>Shipped rules with high birth and death rates, so every settlement step uses the random stream.</summary>
    public static SettlementRules BusyRules => Shipped.SettlementRules with
    {
        BirthChancePerPersonPerStep = 0.04,
        DeathChancePerPersonPerStep = 0.03,
    };

    /// <summary>Content with the small world and busy rules.</summary>
    public static GameContent SmallBusy => Shipped with { WorldGen = SmallWorldGen, SettlementRules = BusyRules };

    /// <summary>The shipped content with the small world.</summary>
    public static GameContent Small => Shipped with { WorldGen = SmallWorldGen };

    /// <summary>The shipped weather view settings.</summary>
    public static WeatherViewSettings WeatherView => ShippedPresentationContent.Value.WeatherView;

    /// <summary>The shipped divine view settings.</summary>
    public static DivineViewSettings DivineView => ShippedPresentationContent.Value.DivineView;

    /// <summary>Reads one content file as text.</summary>
    /// <param name="fileName">File name inside <see cref="DataDirectory"/>.</param>
    /// <returns>File text.</returns>
    public static string ReadText(string fileName) => File.ReadAllText(Path.Combine(DataDirectory, fileName));

    /// <summary>Parses and validates the shipped simulation content.</summary>
    private static GameContent LoadShipped() => GameContent.Parse(GameContentFiles.Read(ReadText));

    /// <summary>Parses and validates the shipped presentation content.</summary>
    private static PresentationContent LoadPresentation() =>
        PresentationContent.Parse(ReadText(BiomePalette.FileName), ReadText(SettlementViewSettings.FileName), ReadText(DivineViewSettings.FileName), ReadText(WeatherViewSettings.FileName));
}
