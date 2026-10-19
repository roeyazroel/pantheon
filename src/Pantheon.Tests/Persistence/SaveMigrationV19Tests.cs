using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Weather;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 7's build (format version 19) still load, with an even climate and the land's animals at its cover.</summary>
public sealed class SaveMigrationV19Tests
{
    /// <summary>A save written by part 7's build at commit b02da54: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version19SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v19.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion19SaveWithVictories_AndNoSeasonsOrEcology()
    {
        var root = ReadFixture();

        Assert.Equal(19, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!.AsObject().ContainsKey("Victory"));
        Assert.False(root["World"]!.AsObject().ContainsKey("Seasons"));
        Assert.False(root["World"]!["Regions"]!["Cells"]![0]!.AsObject().ContainsKey("Wildlife"));
    }

    [Fact]
    public void LoadVersion19_HasNoSeasons_NoSnowOrHarm_AndTheLandsAnimals()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version19SavePath);

        Assert.Null(world.Seasons);
        Assert.All(world.Regions.Cells, r => Assert.Equal((0f, 0f, 0f, 0f), (r.Snow, r.Damage, r.Scar, r.Pollution)));
        Assert.All(world.Regions.Cells, r => Assert.Equal(r.Biome == Biome.Sea ? 0f : Math.Clamp(r.Vegetation, 0f, 1f), r.Wildlife));
        Assert.Contains(world.Regions.Cells, r => r.Wildlife > 0f);
    }

    [Fact]
    public void LoadVersion19_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version20Expectations.Apply(expected);
        Version21Expectations.Apply(expected);
        Version22Expectations.Apply(expected);
        Version23Expectations.Apply(expected);
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

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version19SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion19_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version19SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version19SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Regions.Cells.Select(r => (r.Wildlife, r.Damage, r.Pollution))), JsonSerializer.Serialize(resumed.Regions.Cells.Select(r => (r.Wildlife, r.Damage, r.Pollution))));
    }

    [Fact]
    public void Load_AVersion19SaveThatAlreadyHasSeasons_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Seasons"] = new JsonObject { ["OriginTick"] = 0 };

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Seasons'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheSeasons_AndTheLandsLife()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version19SavePath);
        world.Seasons = new SeasonState { OriginTick = world.Tick - 3000 };
        var region = world.Regions.Cells.First(r => r.Biome != Biome.Sea);
        (region.Snow, region.Wildlife, region.Damage, region.Scar, region.Pollution) = (0.4f, 0.3f, 0.6f, 0.2f, 0.1f);

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));
        var back = loaded.Regions.Cells[region.Id];

        Assert.Equal(world.Tick - 3000, loaded.Seasons!.OriginTick);
        Assert.Equal((0.4f, 0.3f, 0.6f, 0.2f, 0.1f), (back.Snow, back.Wildlife, back.Damage, back.Scar, back.Pollution));
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version19SavePath))!.AsObject();
}
