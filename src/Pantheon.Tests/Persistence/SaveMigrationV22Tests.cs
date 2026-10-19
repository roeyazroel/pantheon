using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Notables;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 11's build (format version 22) still load, with no notables yet.</summary>
public sealed class SaveMigrationV22Tests
{
    /// <summary>A save written by part 11's build at commit 4fc4cd2: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version22SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v22.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion22Save_WithoutNotables()
    {
        var root = ReadFixture();

        Assert.Equal(22, root["SaveVersion"]!.GetValue<int>());
        Assert.False(root["World"]!.AsObject().ContainsKey("Notables"));
        Assert.True(root["World"]!["Gods"]![0]!.AsObject().ContainsKey("Held"));
    }

    [Fact]
    public void LoadVersion22_HasNoNotables_AndNamesNoOneInItsHistory()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version22SavePath);

        Assert.Empty(world.Notables);
        Assert.Equal(1, world.NextNotableId);
        Assert.All(world.History, record => Assert.Null(record.Name));
    }

    [Fact]
    public void LoadVersion22_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version23Expectations.Apply(expected);
        Version24Expectations.Apply(expected);
        Version25Expectations.Apply(expected);
        Version26Expectations.Apply(expected);
        Version27Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version22SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion22_KeepsPlayingTheSameWay_ItsVillagesChoosingElders()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version22SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version22SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        Assert.Contains(straight.Notables, n => n.Kind == NotableKind.Elder);
        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Notables), JsonSerializer.Serialize(resumed.Notables));
    }

    [Fact]
    public void Load_AVersion22SaveThatAlreadyHasNotables_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Notables"] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Notables'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheNotables()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version22SavePath);
        var village = world.Settlements[0];
        world.Notables.Add(new NotableState
        {
            Id = 1, Kind = NotableKind.Prophet, Name = "Asha", GodId = world.Gods[0].Id, SettlementId = village.Id, Position = village.Position,
            TargetSettlementId = world.Settlements[1].Id, BornTick = world.Tick, DiesAtTick = world.Tick + 9000, TicksLeft = 0,
        });
        world.Notables[0].Path.Add(world.Settlements[1].Position);
        world.Notables[0].Memories.Add(new NotableMemory { Tick = world.Tick, Deed = NotableDeed.Preached, SettlementId = village.Id, Count = 4 });
        world.NextNotableId = 2;

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(JsonSerializer.Serialize(world.Notables), JsonSerializer.Serialize(loaded.Notables));
        Assert.Equal(2, loaded.NextNotableId);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version22SavePath))!.AsObject();
}
