using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 16's build (format version 27) still load, their Creatures' new traits at the middle.</summary>
public sealed class SaveMigrationV27Tests
{
    /// <summary>A save written by part 16's build at commit cb089c5: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version27SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v27.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion27Save_WithCreaturesOfFourTraits()
    {
        var root = ReadFixture();
        var personality = root["World"]!["Creatures"]![0]!["Personality"]!.AsObject();

        Assert.Equal(27, root["SaveVersion"]!.GetValue<int>());
        Assert.True(personality.ContainsKey("Curiosity"));
        Assert.False(personality.ContainsKey("Courage"));
        Assert.True(root["World"]!["Gods"]![0]!.AsObject().ContainsKey("KnownGods"));
    }

    [Fact]
    public void LoadVersion27_GivesEveryCreatureMiddlingCourageProtectivenessJealousyAndPlayfulness()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version27SavePath);

        Assert.NotEmpty(world.Creatures);
        Assert.All(world.Creatures, c => Assert.Equal((0.5f, 0.5f, 0.5f, 0.5f), (c.Personality.Courage, c.Personality.Protectiveness, c.Personality.Jealousy, c.Personality.Playfulness)));
    }

    [Fact]
    public void LoadVersion27_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version28Expectations.Apply(expected);
        Version29Expectations.Apply(expected);
        Version30Expectations.Apply(expected);
        Version31Expectations.Apply(expected);
        Version32Expectations.Apply(expected);
        Version33Expectations.Apply(expected);
        Version34Expectations.Apply(expected);
        Version35Expectations.Apply(expected);
        Version36Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version27SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion27_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version27SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version27SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Creatures), JsonSerializer.Serialize(resumed.Creatures));
    }

    [Fact]
    public void Load_AVersion27SaveWhoseCreatureAlreadyHasCourage_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Creatures"]![0]!["Personality"]!["Courage"] = 0.4;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Courage'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheTraits_AndRejectsOneOutsideZeroToOne()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version27SavePath);
        var traits = world.Creatures[0].Personality;
        (traits.Courage, traits.Protectiveness, traits.Jealousy, traits.Playfulness) = (0.9f, 0.2f, 0.7f, 0.35f);

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(JsonSerializer.Serialize(world.Creatures), JsonSerializer.Serialize(loaded.Creatures));
        traits.Jealousy = 1.2f;
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version27SavePath))!.AsObject();
}
