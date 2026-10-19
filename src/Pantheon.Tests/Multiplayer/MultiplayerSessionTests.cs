using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Multiplayer;
using Pantheon.Networking;
using Pantheon.Networking.Protocol;
using Pantheon.Networking.Transport;
using Pantheon.Persistence;
using Pantheon.Simulation.Belief;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Multiplayer;

/// <summary>A host and its players (spec §67, §71, §73, §127, ADR-018): joining, gods, intents, snapshots, events and leaving.</summary>
public sealed class MultiplayerSessionTests
{
    [Fact]
    public void APlayerJoins_TakesTheFirstGodTheAiPlays_AndGetsTheHostsWorld()
    {
        var shared = SharedWorlds.Start();

        var ana = shared.Join("Ana");

        Assert.Equal(ClientPhase.Joined, ana.Phase);
        Assert.Equal(2, ana.GodId);
        Assert.Equal(GodController.Remote, shared.Host.World.Gods.Single(g => g.Id == 2).Controller);
        var replica = ana.Session!;
        Assert.True(replica.IsReplica);
        Assert.Equal(2, replica.LocalGodId);
        WorldAssert.Equal(shared.Host.World, replica.World);
        Assert.Equal(["Hostess", "Ana"], ana.Seats.Select(s => s.Name));
        Assert.Equal("Ana", shared.Host.Players[2]);
        Assert.Equal("Hostess", replica.Players[1]);
        Assert.Contains("Ana joined and plays Vael", shared.HostStatuses);
    }

    [Fact]
    public void APlayer_MayAskForAGod_AndTwoPlayersShareTheWorld()
    {
        var shared = SharedWorlds.Start();

        var ana = shared.Join("Ana", "seren");
        var bo = shared.Join("Bo");
        var statuses = shared.Exchange(ana, bo);

        Assert.Equal((3, 2), (ana.GodId!.Value, bo.GodId!.Value));
        Assert.Contains("Bo joined", statuses);
        Assert.Equal(3, ana.Seats.Count);
        Assert.Equal("Bo", ana.Session!.Players[2]);
    }

    [Fact]
    public void TheHost_TurnsAwayOtherGameData_AnotherProtocol_AndPlayersBeyondTheGods()
    {
        var shared = SharedWorlds.Start();

        var modded = shared.Join("Mod", content: TestContent.Small with { Fingerprint = "other" });
        Assert.Equal((ClientPhase.Refused, "Your game data differs from the host's"), (modded.Phase, modded.Reason));

        var taken = new[] { shared.Join("A"), shared.Join("B"), shared.Join("C") };
        var late = shared.Join("D");
        Assert.All(taken, p => Assert.Equal(ClientPhase.Joined, p.Phase));
        Assert.Equal((ClientPhase.Refused, "No god is free"), (late.Phase, late.Reason));

        var old = shared.Network.Connect();
        old.Send(NetworkProtocol.HostPeerId, MessageCodec.Encode(new Hello(99, TestContent.Small.Fingerprint, "Old", null)), Delivery.Reliable);
        shared.Exchange();
        var answer = old.Poll().Where(e => e.Kind == TransportEventKind.Received).Select(e => MessageCodec.Decode(e.Data!)).OfType<Refused>().Single();
        Assert.StartsWith($"The host speaks protocol {NetworkProtocol.Version}", answer.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void APlayersMiracle_IsCheckedAndCastByTheHost_AsItsGod()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var vael = shared.Host.World.Gods.Single(g => g.Id == 2);
        vael.DivinePower = 500f;
        var home = shared.Host.World.Settlements.OrderByDescending(s => BeliefModel.Find(s, 2)?.Adherence ?? 0f).First();

        var asked = SessionCommands.CastMiracle(ana.Session!, "food", home.Position);
        var statuses = shared.Exchange(ana);

        Assert.Equal("Asking the host...", asked);
        Assert.StartsWith("Food", Assert.Single(statuses), StringComparison.Ordinal);
        Assert.True(vael.DivinePower < 500f);
        Assert.Contains(vael.Cooldowns, c => c.MiracleId == "food");
        Assert.Contains(ana.Session!.DrainEvents(), e => e is MiracleCast { GodId: 2, MiracleId: "food" });

        vael.DivinePower = 0f;
        SessionCommands.CastMiracle(ana.Session!, "heal", home.Position);
        Assert.StartsWith("Not enough Divine Power", Assert.Single(shared.Exchange(ana)), StringComparison.Ordinal);
    }

    [Fact]
    public void APlayersDiplomacyCreatureAndRefugeeIntents_ActForItsGodOnly()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        Meetings.All(shared.Host.World);

        SessionCommands.CommandDiplomacy(ana.Session!, 1, DiplomaticCommandKind.DeclareWar);
        Pantheon.Game.Migration.MigrationCommands.SetPolicy(ana.Session!, Domain.Migration.RefugeePolicy.TurnAway);
        SessionCommands.CommandCreature(ana.Session!, Domain.Creatures.CreatureCommandKind.GoTo, shared.Host.World.Settlements[0].Position);
        var statuses = shared.Exchange(ana);

        Assert.Equal(DiplomaticStatus.War, Pantheon.Simulation.Diplomacy.DiplomacyModel.StatusOf(shared.Host.World, 1, 2));
        Assert.Equal(Domain.Migration.RefugeePolicy.TurnAway, shared.Host.World.Gods.Single(g => g.Id == 2).RefugeePolicy);
        Assert.Equal(Domain.Migration.RefugeePolicy.Welcome, shared.Host.World.Gods.Single(g => g.Id == 1).RefugeePolicy);
        Assert.Equal(Domain.Creatures.CreatureAction.GoTo, shared.Host.World.Creatures.Single(c => c.GodId == 2).Activity.Action);
        Assert.Equal(3, statuses.Count);
    }

    [Fact]
    public void APlayersDomainIntent_TakesUpADomainForItsGod_AndTheSitesReachItsReplica()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");

        var asking = Pantheon.Game.Domains.DomainCommands.Choose(ana.Session!, Domain.Gods.GodDomain.Storm);
        var statuses = shared.Exchange(ana);
        shared.Pass(3.0, ana);

        Assert.Equal("Asking the host...", asking);
        Assert.Equal([Domain.Gods.GodDomain.Storm], shared.Host.World.Gods.Single(g => g.Id == 2).Domains);
        Assert.Empty(shared.Host.World.Gods.Single(g => g.Id == 1).Domains);
        Assert.Contains("You take up the Storm", statuses);
        Assert.Equal([Domain.Gods.GodDomain.Storm], ana.Session!.World.Gods.Single(g => g.Id == 2).Domains);
        Assert.Equal(shared.Host.World.HolySites.Select(s => s.Name), ana.Session.World.HolySites.Select(s => s.Name));
    }

    [Fact]
    public void OnlyTheHost_SavesLoadsKeepsTheClockOrUsesTheDebugTools()
    {
        var shared = SharedWorlds.Start();
        var replica = shared.Join("Ana").Session!;
        var power = replica.LocalGod!.DivinePower;

        Assert.All(
            new[]
            {
                SessionCommands.GrantPower(replica),
                SessionCommands.TogglePause(replica),
                SessionCommands.SetSpeedPreset(replica, 2),
                SessionCommands.StepTick(replica),
                SessionCommands.SpoilFood(replica),
                SessionCommands.QuickSave(replica, Path.Combine(Path.GetTempPath(), "never.json")),
                Pantheon.Game.Weather.WeatherCommands.NextSpell(replica),
                Pantheon.Game.Disasters.DisasterCommands.StartFire(replica, replica.World.Settlements[0].Position),
                Pantheon.Game.Creatures.CreatureCommands.BringOther(replica),
            },
            status => Assert.Equal(SessionCommands.HostOnlyText, status));
        Assert.Equal(power, replica.LocalGod!.DivinePower);
    }

    [Fact]
    public void Players_ReceiveTheHostsEvents_AndSnapshots_AndPredictInBetween()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var replica = ana.Session!;
        replica.DrainEvents();
        shared.Host.LocalGod!.DivinePower = 500f;
        var home = shared.Host.World.Settlements.OrderByDescending(s => BeliefModel.Find(s, 1)?.Adherence ?? 0f).First();

        SessionCommands.CastMiracle(shared.Host, "food", home.Position);
        shared.Host.RunTicks(30);
        replica.RunTicks(30);
        shared.Pass(Replicator.SnapshotSeconds, ana);

        Assert.Contains(replica.DrainEvents(), e => e is MiracleCast { GodId: 1 });
        Assert.Equal(shared.Host.World.Tick, ana.HostTick);
        WorldAssert.Equal(shared.Host.World, replica.World);
    }

    [Fact]
    public void AReplicaKeepsNoEventsOfItsOwn_AndFollowsTheHostsClock()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var replica = ana.Session!;
        replica.DrainEvents();
        shared.Host.Scheduler.TimeScale = 4.0;
        shared.Host.Scheduler.IsPaused = true;

        replica.RunTicks(600);
        shared.Pass(Replicator.SnapshotSeconds, ana);

        Assert.Empty(replica.DrainEvents());
        Assert.Equal((4.0, true), (replica.Scheduler.TimeScale, replica.Scheduler.IsPaused));
        WorldAssert.Equal(shared.Host.World, replica.World);
    }

    [Fact]
    public void APlayerWhoLeaves_GivesItsGodBackToTheAi()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var bo = shared.Join("Bo");

        ana.Dispose();
        var statuses = shared.Exchange(bo);

        Assert.Equal(GodController.Ai, shared.Host.World.Gods.Single(g => g.Id == 2).Controller);
        Assert.Contains("Ana left; the AI plays Vael again", shared.HostStatuses);
        Assert.Contains("Ana left", statuses);
        Assert.False(shared.Host.Players.ContainsKey(2));
    }

    [Fact]
    public void WhenTheHostEndsTheSession_PlayersAreToldAndStop()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");

        shared.Server.Dispose();
        var statuses = shared.Exchange(ana);

        Assert.Equal(ClientPhase.Closed, ana.Phase);
        Assert.Contains("The host ended the session", statuses);
        Assert.True(ana.Session!.Scheduler.IsPaused);
        Assert.Equal(GodController.Ai, shared.Host.World.Gods.Single(g => g.Id == 2).Controller);
        Assert.Equal("You are not in a session", SessionCommands.CastMiracle(ana.Session, "food", default));
    }

    [Fact]
    public void TheAi_LeavesAPlayersGodAlone()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        shared.Host.DrainEvents();
        shared.Host.LocalGod!.DivinePower = 0f;
        shared.Host.World.Gods.Single(g => g.Id == 2).DivinePower = 5000f;

        shared.Host.RunTicks(900);

        Assert.DoesNotContain(shared.Host.DrainEvents(), e => e is MiracleCast { GodId: 2 } or DiplomacyChanged { ActorGodId: 2 });
        Assert.Equal(GodController.Remote, shared.Host.World.Gods.Single(g => g.Id == ana.GodId).Controller);
    }

    [Fact]
    public void ASavedSharedWorld_LoadsWithTheAiPlayingThePlayersGods()
    {
        var shared = SharedWorlds.Start();
        shared.Join("Ana");
        var path = Path.Combine(Path.GetTempPath(), $"pantheon-shared-{Environment.ProcessId}.json");
        try
        {
            shared.Host.SaveTo(path);
            Assert.Contains("\"Remote\"", File.ReadAllText(path), StringComparison.Ordinal);

            var loaded = GameSession.Load(TestContent.Small, path);

            Assert.Equal(GodController.Ai, loaded.World.Gods.Single(g => g.Id == 2).Controller);
            Assert.Equal(1, loaded.LocalGodId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ANewHostWorld_KeepsThePlayersOnTheirGods()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");

        SessionCommands.NewWorld(shared.Host, new WorldSeed(42));
        shared.Exchange(ana);

        Assert.Equal(GodController.Remote, shared.Host.World.Gods.Single(g => g.Id == 2).Controller);
        Assert.Equal(new WorldSeed(42), ana.Session!.World.Seed);
        WorldAssert.Equal(shared.Host.World, ana.Session.World);
    }
}
