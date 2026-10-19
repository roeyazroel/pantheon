using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.History;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 13 build (format version 11) still load: a world that remembers nothing yet.</summary>
public sealed class SaveMigrationV11Tests
{
    /// <summary>A save written by the Milestone 13 build at commit dbfef2b: seed 77, four gods, three Creatures that met, 2400 ticks.</summary>
    private static readonly string Version11SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v11.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion11SaveWithCreatureViews_AndNoHistory()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(11, root["SaveVersion"]!.GetValue<int>());
        Assert.Contains(world["Creatures"]!.AsArray(), c => c!["Relations"]!.AsArray().Count > 0);
        Assert.False(world.ContainsKey("History"));
    }

    [Fact]
    public void LoadVersion11_RemembersNothingYet()
    {
        Assert.Empty(WorldSaveSerializer.LoadFromFile(Version11SavePath).History);
    }

    [Fact]
    public void LoadVersion11_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version12Expectations.Apply(expected);
        Version13Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version11SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion11_KeepsPlayingTheSameWay_AndStartsItsHistory()
    {
        var engine = TestContent.Shipped.CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version11SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version11SavePath);

        engine.Run(straight, 1800);
        engine.Run(resumed, 900);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 900);

        WorldAssert.Equal(straight, resumed);
        Assert.NotEmpty(straight.History);
        Assert.Equal(JsonSerializer.Serialize(straight.History), JsonSerializer.Serialize(resumed.History));
    }

    [Fact]
    public void Load_WithABrokenHistory_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version11SavePath);
        world.History.Add(new HistoryRecord { Tick = 0, Kind = HistoryKind.Alliance, GodId = 99, OtherGodId = 2, SettlementId = null, CreatureId = null, OtherCreatureId = null, Count = 0 });
        world.History.Add(new HistoryRecord { Tick = world.Tick + 5, Kind = HistoryKind.Famine, GodId = null, OtherGodId = null, SettlementId = 1, CreatureId = null, OtherCreatureId = null, Count = 0 });

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains("History record 0 names a missing god", exception.Message, StringComparison.Ordinal);
        Assert.Contains("History record 1 is of an unknown kind, out of order or out of time", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadVersion11_WithAHistoryAlready_Throws()
    {
        var root = ReadFixture();
        root["World"]!["History"] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'History'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version11SavePath))!.AsObject();
}
