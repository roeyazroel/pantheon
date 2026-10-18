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

    /// <summary>Shipped rules with high birth and death rates, so every settlement step uses the random stream.</summary>
    public static SettlementRules BusyRules => Shipped.SettlementRules with
    {
        BirthChancePerPersonPerStep = 0.04,
        DeathChancePerPersonPerStep = 0.03,
    };

    /// <summary>Content with the small world and busy rules.</summary>
    public static GameContent SmallBusy => new(SmallWorldGen, BusyRules);

    /// <summary>Reads one content file as text.</summary>
    /// <param name="fileName">File name inside <see cref="DataDirectory"/>.</param>
    /// <returns>File text.</returns>
    public static string ReadText(string fileName) => File.ReadAllText(Path.Combine(DataDirectory, fileName));

    /// <summary>Parses and validates the shipped simulation content.</summary>
    private static GameContent LoadShipped() =>
        GameContent.Parse(ReadText(ContentJson.WorldGenFileName), ReadText(ContentJson.SettlementRulesFileName));

    /// <summary>Parses and validates the shipped presentation content.</summary>
    private static PresentationContent LoadPresentation() =>
        PresentationContent.Parse(ReadText(BiomePalette.FileName), ReadText(SettlementViewSettings.FileName));
}
