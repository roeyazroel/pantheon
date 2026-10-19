using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Gods;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 4 build (format version 4) still load, as single-god worlds without a winner.</summary>
public sealed class SaveMigrationV4Tests
{
    /// <summary>A save written by the Milestone 4 build: seed 77, a Food miracle, then 1500 ticks.</summary>
    private static readonly string Version4SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v4.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion4Save()
    {
        var root = ReadFixture();

        Assert.Equal(4, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!.AsObject().ContainsKey("Creatures"));
        Assert.False(root["World"]!.AsObject().ContainsKey("WinnerGodId"));
    }

    [Fact]
    public void LoadVersion4_GodsHaveNoPersonalityAndNobodyHasWon()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version4SavePath);

        var god = Assert.Single(world.Gods);
        Assert.Equal(GodPersonality.None, god.Personality);
        Assert.Null(god.DominantSinceTick);
        Assert.Empty(world.WinnerGodIds);
    }

    [Fact]
    public void LoadVersion4_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version5Expectations.Apply(expected);
        Version6Expectations.Apply(expected);
        Version7Expectations.Apply(expected);
        Version8Expectations.Apply(expected);
        Version9Expectations.Apply(expected);
        Version10Expectations.Apply(expected);
        Version11Expectations.Apply(expected);
        Version12Expectations.Apply(expected);
        Version13Expectations.Apply(expected);
        Version14Expectations.Apply(expected);
        Version15Expectations.Apply(expected);
        Version16Expectations.Apply(expected);
        Version17Expectations.Apply(expected);
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
        Version31Expectations.Apply(expected);
        Version32Expectations.Apply(expected);
        Version33Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version4SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion4_WithAWinner_Throws()
    {
        var root = ReadFixture();
        root["World"]!["WinnerGodId"] = 1;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("WinnerGodId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WithAMissingWinner_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version4SavePath);
        world.WinnerGodIds.Add(9);

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
        Assert.Contains("missing or repeated god 9", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version4SavePath))!.AsObject();
}
