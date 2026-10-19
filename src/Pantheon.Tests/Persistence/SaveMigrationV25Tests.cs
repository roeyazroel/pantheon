using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 14's build (format version 25) still load, their beast's lair yet to be found.</summary>
public sealed class SaveMigrationV25Tests
{
    /// <summary>A save written by part 14's build at commit 87dd345: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version25SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v25.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion25Save_WithoutABeast()
    {
        var root = ReadFixture();

        Assert.Equal(25, root["SaveVersion"]!.GetValue<int>());
        Assert.False(root["World"]!["GreatEvents"]!.AsObject().ContainsKey("Beast"));
        Assert.True(root["World"]!["GreatEvents"]!.AsObject().ContainsKey("NextEclipseTick"));
    }

    [Fact]
    public void LoadVersion25_HasNoBeastYet_AndNoAnomalySet()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version25SavePath);

        Assert.Null(world.GreatEvents.Beast);
        Assert.Equal(0L, world.GreatEvents.NextAnomalyTick);
        Assert.True(world.GreatEvents.NextEclipseTick > 0);
    }

    [Fact]
    public void LoadVersion25_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version26Expectations.Apply(expected);
        Version27Expectations.Apply(expected);
        Version28Expectations.Apply(expected);
        Version29Expectations.Apply(expected);
        Version30Expectations.Apply(expected);
        Version31Expectations.Apply(expected);
        Version32Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version25SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion25_KeepsPlayingTheSameWay_FindingItsBeastsLair()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version25SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version25SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        Assert.Equal(BeastPhase.Sleeping, straight.GreatEvents.Beast!.Phase);
        Assert.True(straight.GreatEvents.NextAnomalyTick > 4800);
        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.GreatEvents), JsonSerializer.Serialize(resumed.GreatEvents));
    }

    [Fact]
    public void Load_AVersion25SaveThatAlreadyHasABeast_Throws()
    {
        var root = ReadFixture();
        root["World"]!["GreatEvents"]!["Beast"] = null;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Beast'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheBeast_AndAPlacedEvent()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version25SavePath);
        var god = world.Gods[0].Id;
        world.GreatEvents.Beast = new BeastState
        {
            Name = "Gharos the Ancient", LairRegionId = 3, Phase = BeastPhase.Awake, Position = world.Settlements[0].Position, Health = 60f, MaxHealth = 100f,
            TargetSettlementId = world.Settlements[0].Id, WokeTick = world.Tick, RavagedTicks = 20,
        };
        world.GreatEvents.Beast.HarmByGod[god] = 40f;
        world.GreatEvents.Beast.Path.Add(world.Settlements[1].Position);
        world.GreatEvents.Events.Add(new GreatEventState { Id = world.GreatEvents.NextId++, Kind = GreatEventKind.Anomaly, Name = "the Shimmering", StartTick = world.Tick, EndTick = world.Tick + 1800, Position = new GroundPosition(500f, 600f) });

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(JsonSerializer.Serialize(world.GreatEvents), JsonSerializer.Serialize(loaded.GreatEvents));
        world.GreatEvents.Beast.Health = 120f;
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version25SavePath))!.AsObject();
}
