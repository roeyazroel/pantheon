using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Disasters;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 8's build (format version 20) still load, with no hazards under way.</summary>
public sealed class SaveMigrationV20Tests
{
    /// <summary>A save written by part 8's build at commit 57416e2: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version20SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v20.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion20SaveWithSeasons_AndNoHazards()
    {
        var root = ReadFixture();

        Assert.Equal(20, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!.AsObject().ContainsKey("Seasons"));
        Assert.False(root["World"]!["Disasters"]!.AsObject().ContainsKey("Tornadoes"));
        Assert.False(root["World"]!["Regions"]!["Cells"]![0]!.AsObject().ContainsKey("Blight"));
    }

    [Fact]
    public void LoadVersion20_HasNoBlightRubbleOrHazards_AndNoVolcanoYet()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version20SavePath);

        Assert.All(world.Regions.Cells, r => Assert.Equal((0f, 0f), (r.Blight, r.Rubble)));
        Assert.Empty(world.Disasters.Tornadoes);
        Assert.Empty(world.Disasters.Swarms);
        Assert.Empty(world.Disasters.Marks);
        Assert.Empty(world.Disasters.BlightedSettlementIds);
        Assert.Null(world.Disasters.Volcano);
        Assert.Equal(1, world.Disasters.NextHazardId);
    }

    [Fact]
    public void LoadVersion20_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
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
        Version34Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version20SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion20_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version20SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version20SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Disasters), JsonSerializer.Serialize(resumed.Disasters));
    }

    [Fact]
    public void Load_AVersion20SaveThatAlreadyHasTornadoes_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Disasters"]!["Tornadoes"] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Tornadoes'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheHazards()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version20SavePath);
        world.Disasters.Tornadoes.Add(new Tornado { Id = 1, X = 10f, Z = 20f, VelocityX = 1f, VelocityZ = 0f, EndsAtTick = world.Tick + 100 });
        world.Disasters.Swarms.Add(new LocustSwarm { Id = 2, X = 30f, Z = 40f, Size = 0.7f, EndsAtTick = world.Tick + 200 });
        world.Disasters.Volcano = new Volcano { RegionId = 5, Phase = VolcanoPhase.Rumbling, PhaseEndsTick = world.Tick + 50, NextTremorTick = world.Tick };
        world.Disasters.Marks.Add(new TerrainMark { Kind = TerrainMarkKind.Crater, X = 1f, Z = 2f, Radius = 80f, Tick = world.Tick });
        world.Disasters.NextHazardId = 3;
        world.Disasters.BlightedSettlementIds.Add(world.Settlements[0].Id);
        (world.Regions.Cells[7].Blight, world.Regions.Cells[7].Rubble) = (0.4f, 0.9f);

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(JsonSerializer.Serialize(world.Disasters), JsonSerializer.Serialize(loaded.Disasters));
        Assert.Equal((0.4f, 0.9f), (loaded.Regions.Cells[7].Blight, loaded.Regions.Cells[7].Rubble));
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version20SavePath))!.AsObject();
}
