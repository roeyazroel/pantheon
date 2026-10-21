using System;
using System.Collections.Generic;
using System.IO;
using Pantheon.Domain.Ecology;
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
    private static readonly Lazy<Pantheon.Game.Audio.Orchestra.MusicContent> ShippedMusic = new(() => Pantheon.Game.Audio.Orchestra.MusicContent.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", Pantheon.Game.Audio.Orchestra.MusicContent.FileName))));
    private static readonly Lazy<Pantheon.Game.Angel.AngelContent> ShippedAngel = new(() => Pantheon.Game.Angel.AngelContent.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", Pantheon.Game.Angel.AngelContent.FileName))));
    private static readonly Lazy<Pantheon.Game.Minds.MindContent> ShippedMinds = new(() => Pantheon.Game.Minds.MindContent.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", Pantheon.Game.Minds.MindContent.FileName))));

    /// <summary>Directory holding the copied content files.</summary>
    public static string DataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>The shipped, validated content.</summary>
    public static GameContent Shipped => ShippedContent.Value;

    /// <summary>The shipped music (ADR-071).</summary>
    public static Pantheon.Game.Audio.Orchestra.MusicContent Music => ShippedMusic.Value;

    /// <summary>The shipped player's angel (ADR-074).</summary>
    public static Pantheon.Game.Angel.AngelContent Angel => ShippedAngel.Value;

    /// <summary>The shipped gods' minds (ADR-065): the Claude models and the scribes' lore.</summary>
    public static Pantheon.Game.Minds.MindContent Minds => ShippedMinds.Value;

    /// <summary>The shipped palette.</summary>
    public static BiomePalette Palette => ShippedPresentationContent.Value.Palette;

    /// <summary>The shipped settlement view settings.</summary>
    public static SettlementViewSettings SettlementView => ShippedPresentationContent.Value.SettlementView;

    /// <summary>The shipped presentation content, whole.</summary>
    public static PresentationContent ShippedPresentation => ShippedPresentationContent.Value;

    /// <summary>
    /// Shipped generation settings at a quarter of the resolution. The island keeps its physical
    /// size, so tests run fast on a world shaped like the real one.
    /// </summary>
    public static WorldGenSettings SmallWorldGen => Shipped.WorldGen with
    {
        HeightmapResolution = 256,
        VertexSpacing = 8f,
        RegionsPerSide = 16,

        // Test worlds keep the land of the worlds before rivers (ADR-022); RiverWorldGen has them.
        Hydrology = null,

        // And the four biomes of the worlds before §5's others (ADR-038); BiomeWorldGen has them all.
        Biomes = FourBiomes,
        Landforms = null,
    };

    /// <summary>The four biomes every world had before ADR-038, as shipped then.</summary>
    public static IReadOnlyList<BiomeRule> FourBiomes { get; } =
    [
        new() { Biome = Biome.Sea, MaxElevation = 0f, BaseFertility = 0f, BaseVegetation = 0f },
        new() { Biome = Biome.Highland, MinElevation = 70f, BaseFertility = 0.25f, BaseVegetation = 0.2f },
        new() { Biome = Biome.Forest, MinMoisture = 0.55f, BaseFertility = 0.6f, BaseVegetation = 0.9f },
        new() { Biome = Biome.Grassland, BaseFertility = 0.9f, BaseVegetation = 0.5f },
    ];

    /// <summary><see cref="RiverWorldGen"/> with every biome of §5 (ADR-038).</summary>
    public static WorldGenSettings BiomeWorldGen => RiverWorldGen with { Biomes = Shipped.WorldGen.Biomes, Landforms = Shipped.WorldGen.Landforms };

    /// <summary><see cref="SmallWorldGen"/> with the shipped rivers and lakes (ADR-022).</summary>
    public static WorldGenSettings RiverWorldGen => SmallWorldGen with { Hydrology = Shipped.WorldGen.Hydrology };

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
