using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Creatures;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 10 build (format version 10) still load: Creatures that have never dealt with each other.</summary>
public sealed class SaveMigrationV10Tests
{
    /// <summary>A save written by the Milestone 10 build: seed 77, four gods, three Creatures, Seren and Kaio allied, 20 migrants on the road, 2430 ticks.</summary>
    private static readonly string Version10SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v10.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion10SaveWithCreaturesAndMigrants_AndNoRelations()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(10, root["SaveVersion"]!.GetValue<int>());
        Assert.Equal(3, world["Creatures"]!.AsArray().Count);
        Assert.NotEmpty(world["Migration"]!["Groups"]!.AsArray());
        Assert.All(world["Creatures"]!.AsArray(), creature => Assert.False(creature!.AsObject().ContainsKey("Relations")));
    }

    [Fact]
    public void LoadVersion10_CreaturesHaveNoSkillScarsOrViews_AndNothingConcernsAnotherCreature()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version10SavePath);

        Assert.All(world.Creatures, creature =>
        {
            Assert.Equal(0f, creature.CombatSkill);
            Assert.Equal(0, creature.Scars);
            Assert.Empty(creature.Relations);
            Assert.Null(creature.Activity.TargetCreatureId);
            Assert.All(creature.Memories, memory => Assert.Null(memory.OtherCreatureId));
        });
        Assert.NotEmpty(world.Migration.Groups);
    }

    [Fact]
    public void LoadVersion10_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version11Expectations.Apply(expected);
        Version12Expectations.Apply(expected);
        Version13Expectations.Apply(expected);
        Version14Expectations.Apply(expected);
        Version15Expectations.Apply(expected);
        Version16Expectations.Apply(expected);
        Version17Expectations.Apply(expected);
        Version18Expectations.Apply(expected);
        Version19Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version10SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion10_KeepsPlayingTheSameWay_TheCreaturesComingToKnowEachOther()
    {
        var engine = TestContent.Shipped.CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version10SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version10SavePath);
        var (a, b) = (straight.Creatures[0], straight.Creatures[1]);
        b.Position = new Domain.World.GroundPosition(a.Position.X + 20f, a.Position.Z);
        resumed.Creatures[1].Position = b.Position;

        engine.Run(straight, 1200);
        engine.Run(resumed, 600);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 600);

        WorldAssert.Equal(straight, resumed);
        Assert.Contains(straight.Creatures, creature => creature.Relations.Count > 0);
    }

    [Fact]
    public void Load_WithABrokenRelation_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version10SavePath);
        var creature = world.Creatures[0];
        creature.Scars = -1;
        world.Creatures[1].Relations.Add(new CreatureRelation
        {
            OtherCreatureId = 99,
            Friendship = 2f,
            Fear = 0f,
            Rivalry = 0f,
            Respect = 0f,
            Trust = 0.3f,
            LastMetTick = 0,
            Encounters = [],
        });
        world.Creatures[2].Activity.TargetCreatureId = world.Creatures[2].Id;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains($"Creature {creature.Id} has combat skill or scars out of range", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"Creature {world.Creatures[1].Id} has a missing, duplicate or self relation", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"Creature {world.Creatures[2].Id} names a missing Creature, or itself", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadVersion10_WithRelationsAlready_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Creatures"]![0]!["Relations"] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Relations'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version10SavePath))!.AsObject();
}
