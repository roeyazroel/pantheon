using System;
using System.Text.Json.Nodes;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Gods, beliefs, cooldowns and blessings survive a save and load, and broken ones are rejected.</summary>
public sealed class DivineSaveTests
{
    [Fact]
    public void RoundTrip_PreservesGodsBeliefsCooldownsAndBlessings()
    {
        var world = PlayedWorld();
        Assert.NotEmpty(world.Gods[0].Cooldowns);
        Assert.Contains(world.Regions.Cells, cell => cell.Blessing > 0f);

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        WorldAssert.Equal(world, loaded);
    }

    [Fact]
    public void SaveAndLoad_ContinueTheSameWithBelief()
    {
        var content = TestContent.Small;
        var straight = GameSession.CreateNew(content, new WorldSeed(21));
        var resumed = GameSession.CreateNew(content, new WorldSeed(21));
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pantheon-tests", Guid.NewGuid().ToString("N") + ".json");

        straight.CastMiracle("lightning", straight.World.Settlements[0].Position);
        resumed.CastMiracle("lightning", resumed.World.Settlements[0].Position);
        straight.RunTicks(600);
        resumed.RunTicks(300);
        resumed.SaveTo(path);
        resumed.LoadFrom(path);
        resumed.RunTicks(300);

        WorldAssert.Equal(straight.World, resumed.World);
        System.IO.File.Delete(path);
    }

    [Theory]
    [InlineData("Adherence", 1.5)]
    [InlineData("Faith", -0.1)]
    [InlineData("Doubt", 2.0)]
    public void Load_WithOutOfRangeBelief_Throws(string member, double value)
    {
        var root = SaveAsNode(PlayedWorld());
        root["World"]!["Settlements"]![0]!["Beliefs"]![0]![member] = value;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("outside 0..1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WithBeliefInAMissingGod_Throws()
    {
        var root = SaveAsNode(PlayedWorld());
        root["World"]!["Settlements"]![0]!["Beliefs"]![0]!["GodId"] = 9;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("missing or repeated god 9", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WithFollowersAddingUpPastOne_Throws()
    {
        var root = SaveAsNode(PlayedWorld());
        var beliefs = root["World"]!["Settlements"]![0]!["Beliefs"]!.AsArray();
        beliefs[0]!["Adherence"] = 0.7;
        var second = beliefs[0]!.DeepClone().AsObject();
        second["GodId"] = 2;
        beliefs.Add(second);
        var gods = root["World"]!["Gods"]!.AsArray();
        var god = gods[0]!.DeepClone().AsObject();
        god["Id"] = 2;
        gods.Add(god);

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("must add up to at most 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WithBrokenGod_ListsEveryProblem()
    {
        var root = SaveAsNode(PlayedWorld());
        root["World"]!["Gods"]![0]!["DivinePower"] = -5;
        root["World"]!["Regions"]!["Cells"]![0]!["Blessing"] = 3;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("Divine Power -5", exception.Message, StringComparison.Ordinal);
        Assert.Contains("blessing 3", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A small world that has cast miracles and run for a while.</summary>
    private static WorldState PlayedWorld()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(13));
        SessionCommands.GrantPower(session);
        Assert.True(session.CastMiracle("bless_crops", session.World.Settlements[0].Position).IsAccepted);
        Assert.True(session.CastMiracle("food", session.World.Settlements[0].Position).IsAccepted);
        session.RunTicks(120);
        return session.World;
    }

    /// <summary>A world's save as an editable JSON tree.</summary>
    private static JsonObject SaveAsNode(WorldState world) => JsonNode.Parse(WorldSaveSerializer.Serialize(world))!.AsObject();
}
