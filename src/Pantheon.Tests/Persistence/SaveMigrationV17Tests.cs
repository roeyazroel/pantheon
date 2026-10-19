using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Conflict;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 5's build (format version 17) still load: no war records or holy wars yet.</summary>
public sealed class SaveMigrationV17Tests
{
    /// <summary>A save written by part 5's build at commit 3b3ef36: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version17SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v17.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion17SaveWithTreaties_AndNoWarRecords()
    {
        var root = ReadFixture();

        Assert.Equal(17, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!["Diplomacy"]!.AsObject().ContainsKey("Council"));
        Assert.False(root["World"]!["Diplomacy"]!.AsObject().ContainsKey("Wars"));
    }

    [Fact]
    public void LoadVersion17_HasNoWarRecordsOrHolyWars()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version17SavePath);

        Assert.Empty(world.Diplomacy.Wars);
        Assert.All(world.Settlements, s => Assert.Null(s.Society.HolyWar));
    }

    [Fact]
    public void LoadVersion17_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version18Expectations.Apply(expected);
        Version19Expectations.Apply(expected);
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

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version17SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion17_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version17SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version17SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Diplomacy), JsonSerializer.Serialize(resumed.Diplomacy));
        Assert.Equal(JsonSerializer.Serialize(straight.Settlements.Select(s => s.Society)), JsonSerializer.Serialize(resumed.Settlements.Select(s => s.Society)));
    }

    [Fact]
    public void Load_WithABrokenWarOrHolyWar_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version17SavePath);
        world.Diplomacy.Wars.Add(new WarRecord { AggressorGodId = 1, DefenderGodId = 1, Goal = WarGoal.Conquest, SiteId = null, StartedTick = 0, AggressorScore = -1f, DefenderScore = 0f, GoalAnnounced = false });
        world.Settlements[0].Society.HolyWar = new HolyWarState { DefenderSettlementId = 999, GodId = 1, EnemyGodId = 2, SinceTick = 0, Stance = HolyWarStance.Ignore, Killed = 0, Converted = 0 };

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains("A war names a missing god or site, one god twice", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"Settlement {world.Settlements[0].Id} wages a holy war on a missing settlement", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version17SavePath))!.AsObject();
}
