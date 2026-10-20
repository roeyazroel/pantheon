using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Multiplayer;
using Pantheon.Networking.Protocol;
using Pantheon.Persistence;
using Pantheon.Simulation.Belief;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Multiplayer;

/// <summary>Each part of the world reaching players at its own rate (spec §95–96, §128, ADR-019).</summary>
public sealed class MultiplayerReplicationTests
{
    [Fact]
    public void EverySection_CarriesThatPartOfTheWorld_IntoAReplica()
    {
        var host = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var replica = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(host.World));
        host.RunTicks(900);
        var sent = new Dictionary<int, RegionChange>();
        foreach (var region in replica.Regions.Cells)
        {
            sent[region.Id] = RegionChange.Of(region);
        }

        foreach (var section in Enum.GetValues<WorldSection>())
        {
            var data = section == WorldSection.Regions ? WorldSections.WriteRegions(host.World, sent) : WorldSections.Write(host.World, section);
            if (data is not null)
            {
                WorldSections.Apply(replica, section, data);
            }
        }

        // Regions arrive within the tolerance (ADR-056); everything else exactly.
        Assert.All(host.World.Regions.Cells, region => Assert.True(RegionChange.Of(region).IsNear(RegionChange.Of(replica.Regions.Cells[region.Id]), WorldSections.RegionTolerance)));
        foreach (var region in host.World.Regions.Cells)
        {
            RegionChange.Of(region).ApplyTo(replica.Regions.Cells[region.Id]);
        }

        replica.Tick = host.World.Tick;
        WorldAssert.Equal(host.World, replica);
        Assert.Null(WorldSections.WriteRegions(host.World, sent));
    }

    [Fact]
    public void ARegion_IsSentAgain_OnlyWhenAValueMovesPastTheTolerance_OrReachesOrLeavesNothing_InThousandths()
    {
        var world = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234)).World;
        var sent = new Dictionary<int, RegionChange>();
        var region = world.Regions.Cells[3];
        (region.Vegetation, region.Fire) = (0.5f, 0f);
        Assert.NotNull(WorldSections.WriteRegions(world, sent));

        region.Vegetation += WorldSections.RegionTolerance * 0.5f;
        var creep = WorldSections.WriteRegions(world, sent);
        region.Vegetation += WorldSections.RegionTolerance;
        var moved = WorldSections.WriteRegions(world, sent);
        region.Fire = 0.001f;
        var kindled = WorldSections.WriteRegions(world, sent);
        region.Fire = 0f;
        var out_ = WorldSections.WriteRegions(world, sent);

        Assert.Null(creep);
        Assert.Contains("\"Id\":3", moved, StringComparison.Ordinal);
        Assert.NotNull(kindled);
        Assert.NotNull(out_);
        Assert.Equal(RegionChange.Of(region).Rounded(), sent[3]);
        Assert.Equal(0.001f, new RegionChange(0, 0.0004f, 0f, 0f, 0f, 0f, 0f, 0f).Rounded().Fertility);
        Assert.Equal(0.123f, new RegionChange(0, 0.12345f, 0f, 0f, 0f, 0f, 0f, 0f).Rounded().Fertility);
    }

    [Fact]
    public void ThePlayer_GetsEachSectionAtItsRate_AndNoWholeWorldUntilTheCorrection()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");

        Run(shared, 20, ana);

        var got = ana.SectionsReceived;
        Assert.InRange(got[WorldSection.CreatureMotion], 9, 10);
        Assert.InRange(got[WorldSection.Gods], 7, 8);
        Assert.InRange(got[WorldSection.Settlements], 1, 2);
        Assert.Equal(1, got[WorldSection.Creatures]);
        Assert.Equal(shared.Host.World.Tick, ana.HostTick);
    }

    [Fact]
    public void AnUnchangedSection_IsNotSentAgain()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        shared.Host.Scheduler.IsPaused = true;

        for (var i = 0; i < 30; i++)
        {
            shared.Pass(0.1, ana);
        }

        Assert.Equal(1, ana.SectionsReceived[WorldSection.Settlements]);
        Assert.Equal(1, ana.SectionsReceived[WorldSection.Gods]);
        Assert.False(ana.SectionsReceived.ContainsKey(WorldSection.Regions));
    }

    [Fact]
    public void CreaturesNearThePlayersView_MoveFiveTimesASecond_OthersOnce()
    {
        var world = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234)).World;
        var replicator = new Replicator();
        replicator.Add(2, world);
        var near = world.Creatures[0];
        replicator.Focus(2, near.Position);
        Assert.Contains(world.Creatures, c => Math.Abs(c.Position.X - near.Position.X) + Math.Abs(c.Position.Z - near.Position.Z) > 2f * Replicator.InterestRadius);

        var motions = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            var update = replicator.Due(world, 0.2).Updates.Single(u => u.Update.Section == WorldSection.CreatureMotion).Update;
            motions.Add(JsonSerializer.Deserialize<List<CreatureMotion>>(update.Data, PantheonJson.WireOptions)!.Count);
        }

        var nearby = world.Creatures.Count(c => Close(c.Position, near.Position));
        Assert.True(nearby < world.Creatures.Count);
        Assert.Equal([nearby, nearby, nearby, nearby, world.Creatures.Count], motions);
    }

    [Fact]
    public void APlayersIntent_ReachesItsWorldInTheSameUpdateAsTheVerdict()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        shared.Host.World.Gods.Single(g => g.Id == 2).DivinePower = 500f;
        Run(shared, 3, ana);
        var home = shared.Host.World.Settlements.OrderByDescending(s => BeliefModel.Find(s, 2)?.Adherence ?? 0f).First();

        SessionCommands.CastMiracle(ana.Session!, "food", home.Position);
        shared.Exchange(ana);

        var vael = ana.Session!.World.Gods.Single(g => g.Id == 2);
        Assert.Equal(shared.Host.World.Gods.Single(g => g.Id == 2).DivinePower, vael.DivinePower);
        Assert.True(vael.DivinePower < 500f);
        Assert.Contains(vael.Cooldowns, c => c.MiracleId == "food");
    }

    [Fact]
    public void AReplica_StaysWithTheHost_AsBothRun()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");

        Run(shared, 100, ana);

        var replica = ana.Session!.World;
        Assert.InRange(replica.Tick, shared.Host.World.Tick - MultiplayerClient.TickSlack, shared.Host.World.Tick + MultiplayerClient.TickSlack);
        Assert.Equal(Json(shared.Host.World.Gods), Json(replica.Gods));
        Assert.Equal(Json(shared.Host.World.Diplomacy), Json(replica.Diplomacy));
    }

    [Fact]
    public void FourGods_FourPlayers_EachSeesTheSharedWorld_AtAFractionOfWholeSnapshots()
    {
        var shared = SharedWorlds.Start();
        var players = new[] { shared.Join("Ana"), shared.Join("Bo"), shared.Join("Cy") };
        var hostBefore = shared.Server.Stats.BytesSent;

        Run(shared, 300, players);

        Assert.All(players, p => Assert.Equal(ClientPhase.Joined, p.Phase));
        Assert.Equal([2, 3, 4], players.Select(p => p.GodId!.Value));
        Assert.All(players, p => Assert.Equal(Json(shared.Host.World.Gods), Json(p.Session!.World.Gods)));
        var perPlayerPerSecond = (shared.Server.Stats.BytesSent - hostBefore) / 3.0 / 30.0;
        var wholeSnapshot = MessageCodec.Encode(new Snapshot(0, 1, false, WorldSaveSerializer.Serialize(shared.Host.World))).Length;
        Assert.True(perPlayerPerSecond < wholeSnapshot / 2.0, $"{perPlayerPerSecond:0} bytes a second per player against {wholeSnapshot} for one whole snapshot a second");
    }

    [Fact]
    public void APlayerWhoComesBack_GetsItsGodAgain_WhichWaitedForIt()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        Assert.Equal(2, ana.GodId);

        ana.Dispose();
        shared.Exchange();
        var bo = shared.Join("Bo");
        var again = shared.Join("ana");

        Assert.Equal((3, 2), (bo.GodId!.Value, again.GodId!.Value));
    }

    /// <summary>Runs the host and its players a tick at a time, a tenth of a real second each, as a game would.</summary>
    private static void Run(SharedWorlds shared, int ticks, params MultiplayerClient[] players)
    {
        for (var i = 0; i < ticks; i++)
        {
            shared.Host.StepOneTick();
            foreach (var player in players)
            {
                player.Session?.StepOneTick();
            }

            shared.Pass(0.1, players);
        }
    }

    /// <summary>Whether two points lie within the interest radius.</summary>
    private static bool Close(GroundPosition a, GroundPosition b) => MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Z - b.Z, 2) <= Replicator.InterestRadius * Replicator.InterestRadius;

    /// <summary>A value in the save format, for comparing parts of worlds.</summary>
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, PantheonJson.WireOptions);
}
