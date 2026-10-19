using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Gods;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 15's build (format version 26) still load, every god knowing every other and holding no goal yet.</summary>
public sealed class SaveMigrationV26Tests
{
    /// <summary>A save written by part 15's build at commit 4fdfa84: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version26SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v26.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion26Save_WithoutKnownGodsOrGoals()
    {
        var root = ReadFixture();
        var god = root["World"]!["Gods"]![0]!.AsObject();

        Assert.Equal(26, root["SaveVersion"]!.GetValue<int>());
        Assert.False(god.ContainsKey("KnownGods"));
        Assert.False(god.ContainsKey("Goals"));
        Assert.True(root["World"]!["GreatEvents"]!.AsObject().ContainsKey("Beast"));
    }

    [Fact]
    public void LoadVersion26_EveryGodKnowsEveryOther_AndHoldsNoGoal()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version26SavePath);

        Assert.All(world.Gods, god => Assert.Equal(world.Gods.Where(g => g.Id != god.Id).Select(g => g.Id).Order(), god.KnownGods));
        Assert.All(world.Gods, god => Assert.Empty(god.Goals));
        Assert.All(world.Gods, god => Assert.Equal(0L, god.NextPlanTick));
    }

    [Fact]
    public void LoadVersion26_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version27Expectations.Apply(expected);
        Version28Expectations.Apply(expected);
        Version29Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version26SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion26_KeepsPlayingTheSameWay_TheAiGodsMakingTheirPlans()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version26SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version26SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        Assert.Contains(straight.Gods, g => g.Controller == GodController.Ai && g.NextPlanTick > 4800);
        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Gods), JsonSerializer.Serialize(resumed.Gods));
    }

    [Fact]
    public void Load_AVersion26SaveThatAlreadyKnowsItsGods_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Gods"]![0]!["KnownGods"] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'KnownGods'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheGoals_AndRejectsAGoalAboutItself()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version26SavePath);
        var (god, other) = (world.Gods[0], world.Gods[1]);
        god.Goals.Add(new GodGoal { Kind = GodGoalKind.Weaken, GodId = other.Id, SinceTick = world.Tick, Weight = 0.6f });
        god.Goals.Add(new GodGoal { Kind = GodGoalKind.Convert, SettlementId = world.Settlements[1].Id, SinceTick = world.Tick - 10, Weight = 0.4f });
        god.NextPlanTick = world.Tick + 300;

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(JsonSerializer.Serialize(world.Gods), JsonSerializer.Serialize(loaded.Gods));
        god.Goals.Add(new GodGoal { Kind = GodGoalKind.Befriend, GodId = god.Id, SinceTick = world.Tick, Weight = 0.5f });
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
        god.Goals.RemoveAt(2);
        god.KnownGods.Add(god.Id);
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version26SavePath))!.AsObject();
}
