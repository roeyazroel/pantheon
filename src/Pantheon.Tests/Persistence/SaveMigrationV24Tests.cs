using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.GreatEvents;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 13's build (format version 24) still load, with nobody sick and no great event yet.</summary>
public sealed class SaveMigrationV24Tests
{
    /// <summary>A save written by part 13's build at commit 51c332f: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version24SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v24.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion24Save_WithoutGreatEvents()
    {
        var root = ReadFixture();

        Assert.Equal(24, root["SaveVersion"]!.GetValue<int>());
        Assert.False(root["World"]!.AsObject().ContainsKey("GreatEvents"));
        Assert.True(root["World"]!.AsObject().ContainsKey("Roads"));
    }

    [Fact]
    public void LoadVersion24_HasNobodySick_AndNoGreatEvent()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version24SavePath);

        Assert.All(world.Settlements, s => Assert.Equal((0f, 0f), (s.Plague, s.Immunity)));
        Assert.Empty(world.GreatEvents.Events);
        Assert.Equal((1, 0L), (world.GreatEvents.NextId, world.GreatEvents.NextEclipseTick));
        Assert.Empty(world.GreatEvents.LastReformations);
        Assert.Null(world.GreatEvents.DrySinceTick);
    }

    [Fact]
    public void LoadVersion24_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version25Expectations.Apply(expected);
        Version26Expectations.Apply(expected);
        Version27Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version24SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion24_KeepsPlayingTheSameWay_SettingItsFirstEclipse()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version24SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version24SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        Assert.True(straight.GreatEvents.NextEclipseTick > 4800);
        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.GreatEvents), JsonSerializer.Serialize(resumed.GreatEvents));
    }

    [Fact]
    public void Load_AVersion24SaveThatAlreadyHasGreatEvents_Throws()
    {
        var root = ReadFixture();
        root["World"]!["GreatEvents"] = new JsonObject();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'GreatEvents'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheGreatEvents_AndTheSick()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version24SavePath);
        var god = world.Gods[0].Id;
        world.GreatEvents.Events.Add(new GreatEventState { Id = 1, Kind = GreatEventKind.Reformation, Name = "the New Rite", StartTick = world.Tick, EndTick = world.Tick + 600, GodId = god, Peak = 12 });
        world.GreatEvents.NextId = 2;
        world.GreatEvents.NextEclipseTick = world.Tick + 9000;
        world.GreatEvents.LastReformations[god] = world.Tick;
        (world.Settlements[0].Plague, world.Settlements[0].Immunity) = (0.2f, 0.1f);

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(JsonSerializer.Serialize(world.GreatEvents), JsonSerializer.Serialize(loaded.GreatEvents));
        Assert.Equal((0.2f, 0.1f), (loaded.Settlements[0].Plague, loaded.Settlements[0].Immunity));

        world.Settlements[0].Plague = 1.5f;
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version24SavePath))!.AsObject();
}
