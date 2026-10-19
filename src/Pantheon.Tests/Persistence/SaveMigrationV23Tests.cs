using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Roads;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 12's build (format version 23) still load, with forests, stores and no roads yet.</summary>
public sealed class SaveMigrationV23Tests
{
    /// <summary>A save written by part 12's build at commit c866c1e: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version23SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v23.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion23Save_WithoutRoads()
    {
        var root = ReadFixture();

        Assert.Equal(23, root["SaveVersion"]!.GetValue<int>());
        Assert.False(root["World"]!.AsObject().ContainsKey("Roads"));
        Assert.True(root["World"]!.AsObject().ContainsKey("Notables"));
    }

    [Fact]
    public void LoadVersion23_GrowsForestsByBiome_StocksTheVillages_AndHasNoRoads()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version23SavePath);

        Assert.Empty(world.Roads);
        Assert.Equal(1, world.NextRoadId);
        Assert.All(world.Settlements, s => Assert.Equal((30f, 15f), (s.Wood, s.Stone)));
        Assert.All(world.Regions.Cells, r => Assert.Equal(r.Forest, r.Timber));
        Assert.All(world.Regions.Cells.Where(r => r.Biome == Biome.Sea), r => Assert.Equal((0f, 0f), (r.Forest, r.Stone)));
        var forest = world.Regions.Cells.First(r => r.Biome == Biome.Forest);
        Assert.Equal(Math.Clamp(1.1f * forest.Vegetation, 0f, 1f), forest.Forest, 4);
        Assert.Contains(world.Regions.Cells, r => r.Biome == Biome.Highland && r.Stone == 0.75f);
    }

    [Fact]
    public void LoadVersion23_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version24Expectations.Apply(expected);
        Version25Expectations.Apply(expected);
        Version26Expectations.Apply(expected);
        Version27Expectations.Apply(expected);
        Version28Expectations.Apply(expected);
        Version29Expectations.Apply(expected);
        Version30Expectations.Apply(expected);
        Version31Expectations.Apply(expected);
        Version32Expectations.Apply(expected);
        Version33Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version23SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion23_KeepsPlayingTheSameWay_ItsVillagesGatheringAndBuildingRoads()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version23SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version23SavePath);

        engine.Run(straight, 6000);
        engine.Run(resumed, 3000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 3000);

        Assert.NotEmpty(straight.Roads);
        Assert.Contains(straight.Regions.Cells, r => r.Timber < r.Forest - 0.01f);
        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Roads), JsonSerializer.Serialize(resumed.Roads));
    }

    [Fact]
    public void Load_AVersion23SaveThatAlreadyHasRoads_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Roads"] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Roads'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheRoads_AndABrokenOneIsRefused()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version23SavePath);
        var (from, to) = (world.Settlements[0], world.Settlements[1]);
        world.Roads.Add(new RoadState { Id = 1, FromSettlementId = from.Id, ToSettlementId = to.Id, Path = [from.Position, to.Position], Length = 300f, Built = 120f, Wear = 0.2f });
        world.NextRoadId = 2;

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(JsonSerializer.Serialize(world.Roads), JsonSerializer.Serialize(loaded.Roads));
        Assert.Equal(2, loaded.NextRoadId);

        world.Roads[0].Built = 400f;
        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
        Assert.Contains("A road is null", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version23SavePath))!.AsObject();
}
