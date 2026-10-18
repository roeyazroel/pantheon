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
    private static readonly Lazy<BiomePalette> ShippedPaletteContent = new(LoadPalette);

    /// <summary>Directory holding the copied content files.</summary>
    public static string DataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>The shipped, validated content.</summary>
    public static GameContent Shipped => ShippedContent.Value;

    /// <summary>The shipped palette.</summary>
    public static BiomePalette Palette => ShippedPaletteContent.Value;

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

    /// <summary>Shipped rules with high birth and death rates, so the random stream is used on most ticks.</summary>
    public static SettlementRules BusyRules => Shipped.SettlementRules with
    {
        BirthChancePerPersonPerTick = 0.004,
        DeathChancePerPersonPerTick = 0.003,
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

    /// <summary>Parses and validates the shipped palette.</summary>
    private static BiomePalette LoadPalette() => BiomePalette.Parse(ReadText(BiomePalette.FileName));
}
