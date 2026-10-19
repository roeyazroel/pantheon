using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Hand;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 9's build (format version 21) still load, with no paths walked, empty hands and no hallowed land.</summary>
public sealed class SaveMigrationV21Tests
{
    /// <summary>A save written by part 9's build at commit 825c94a: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version21SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v21.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion21SaveWithHazards_AndNoPaths()
    {
        var root = ReadFixture();

        Assert.Equal(21, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!["Disasters"]!.AsObject().ContainsKey("Tornadoes"));
        Assert.False(root["World"]!["Creatures"]![0]!.AsObject().ContainsKey("Specialisations"));
        Assert.False(root["World"]!["Gods"]![0]!.AsObject().ContainsKey("Held"));
    }

    [Fact]
    public void LoadVersion21_HasNoPathsNoHeldThingsAndNoHallowedLand()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version21SavePath);

        Assert.All(world.Creatures, c => Assert.Empty(c.Specialisations));
        Assert.All(world.Creatures, c => Assert.Equal(0f, c.Callings.Healer + c.Callings.Warrior + c.Callings.Guardian));
        Assert.All(world.Gods, g => Assert.Null(g.Held));
        Assert.All(world.Regions.Cells, r => Assert.Equal(0f, r.Hallowed));
    }

    [Fact]
    public void LoadVersion21_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version22Expectations.Apply(expected);
        Version23Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version21SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion21_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version21SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version21SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Creatures), JsonSerializer.Serialize(resumed.Creatures));
    }

    [Fact]
    public void Load_AVersion21SaveThatAlreadyHasCallings_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Creatures"]![0]!["Callings"] = new JsonObject();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Callings'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsPathsHeldThingsAndHallowedLand()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version21SavePath);
        var creature = world.Creatures[0];
        creature.Specialisations.Add(CreatureSpecialisation.Healer);
        creature.Callings.Healer = 4.5f;
        var god = world.Gods.First(g => g.Id == creature.GodId);
        var village = world.Settlements[0];
        god.Held = new HeldObject { Kind = HeldKind.Villagers, Amount = 5, SettlementId = village.Id, FromDanger = true, Health = 0.8f, Beliefs = [], Tick = world.Tick };
        (world.Regions.Cells[9].Blessing, world.Regions.Cells[9].Hallowed) = (0.7f, 0.6f);

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal([CreatureSpecialisation.Healer], loaded.Creatures[0].Specialisations);
        Assert.Equal(4.5f, loaded.Creatures[0].Callings.Healer);
        Assert.Equal(JsonSerializer.Serialize(god.Held), JsonSerializer.Serialize(loaded.Gods.First(g => g.Id == god.Id).Held));
        Assert.Equal(0.6f, loaded.Regions.Cells[9].Hallowed);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version21SavePath))!.AsObject();
}
