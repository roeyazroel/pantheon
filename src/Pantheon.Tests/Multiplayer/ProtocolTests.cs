using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Networking;
using Pantheon.Networking.Protocol;
using Pantheon.Networking.Transport;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Multiplayer;

/// <summary>The messages, their encoding and the loopback transport (ADR-018).</summary>
public sealed class ProtocolTests
{
    public static TheoryData<NetMessage> Messages() =>
    [
        new Hello(NetworkProtocol.Version, "abc", "Ana", "Seren"),
        new Hello(NetworkProtocol.Version, "abc", "Bo", null),
        new Welcome(2, 3),
        new Refused("No god is free"),
        new PlayerList([new PlayerSeat(1, "Host", 1), new PlayerSeat(2, "Ana", 3)]),
        new CastIntent(7, "food", new GroundPosition(10.5f, -3f)),
        new CreatureIntent(8, CreatureCommandKind.GoTo, new GroundPosition(1f, 2f)),
        new DiplomacyIntent(9, 1, DiplomaticCommandKind.OfferAlliance),
        new RefugeeIntent(10, RefugeePolicy.TurnAway),
        new Verdict(7, true, "Food cast on Elar"),
        new Goodbye("The player left"),
    ];

    [Theory]
    [MemberData(nameof(Messages))]
    public void EveryMessage_SurvivesTheTrip(NetMessage message)
    {
        var decoded = MessageCodec.Decode(MessageCodec.Encode(message));

        Assert.Equal(MessageCodec.ToJson(message), MessageCodec.ToJson(decoded));
        Assert.Equal(message.GetType(), decoded.GetType());
    }

    [Fact]
    public void AWorldSnapshot_IsCompressed_AndRestoresTheWorld()
    {
        var world = Pantheon.Game.GameSession.CreateNew(TestContent.Small, new WorldSeed(1234)).World;
        var snapshot = new Snapshot(world.Tick, 4.0, true, WorldSaveSerializer.Serialize(world));

        var packet = MessageCodec.Encode(snapshot);
        var decoded = Assert.IsType<Snapshot>(MessageCodec.Decode(packet));

        Assert.True(packet.Length < snapshot.World.Length / 4, $"a {packet.Length}-byte packet for {snapshot.World.Length} characters of world");
        Assert.Equal((4.0, true), (decoded.TimeScale, decoded.Paused));
        WorldAssert.Equal(world, WorldSaveSerializer.Deserialize(decoded.World));
    }

    [Fact]
    public void Events_TravelWithTheirFields_AndUnknownKindsAreSkipped()
    {
        WorldEvent[] events =
        [
            new MiracleCast { Tick = 5, GodId = 2, MiracleId = "food", Target = new GroundPosition(1f, 2f), Cost = 40f, AffectedSettlementIds = [3, 4], Deaths = 0 },
            new CreaturesMet { Tick = 6, CreatureId = 1, OtherCreatureId = 2, Outcome = MeetingOutcome.Fought, Betrayal = true, GodsAtWar = false, Friends = false },
            new DiplomacyChanged { Tick = 7, ActorGodId = 1, OtherGodId = 2, Kind = DiplomaticCommandKind.DeclareWar, Status = DiplomaticStatus.War, Betrayal = false },
        ];

        var envelopes = EventCodec.Wrap(events).Append(new EventEnvelope("FromTheFuture", JsonDocument.Parse("{}").RootElement)).ToList();
        var batch = Assert.IsType<EventBatch>(MessageCodec.Decode(MessageCodec.Encode(new EventBatch(7, envelopes))));
        var back = EventCodec.Unwrap(batch.Events);

        static string Json(WorldEvent e) => JsonSerializer.Serialize(e, e.GetType(), MessageCodec.Options);
        Assert.Equal(events.Select(e => e.GetType()), back.Select(e => e.GetType()));
        Assert.Equal(events.Select(Json), back.Select(Json));
        Assert.Contains("CreatureFightEnded", EventCodec.KnownKinds);
        Assert.Equal(typeof(WorldEvent).Assembly.GetTypes().Count(t => t.IsSubclassOf(typeof(WorldEvent)) && !t.IsAbstract), EventCodec.KnownKinds.Count);
    }

    [Fact]
    public void APacketThatIsNotAMessage_IsRefused()
    {
        Assert.Throws<InvalidDataException>(() => MessageCodec.Decode([0]));
        Assert.Throws<InvalidDataException>(() => MessageCodec.Decode([9, 1, 2]));
        Assert.Throws<InvalidDataException>(() => MessageCodec.Decode([0, (byte)'{', (byte)'}']));
    }

    [Fact]
    public void TheLoopback_ConnectsDeliversInOrderAndDisconnects()
    {
        var network = new LoopbackNetwork();
        var host = network.CreateHost();
        var player = network.Connect();

        Assert.Equal(new TransportEvent(TransportEventKind.Connected, 2, null), Assert.Single(host.Poll()));
        Assert.Equal(new TransportEvent(TransportEventKind.Connected, NetworkProtocol.HostPeerId, null), Assert.Single(player.Poll()));
        player.Send(NetworkProtocol.HostPeerId, [1, 2], Delivery.Reliable);
        player.Send(NetworkProtocol.HostPeerId, [3], Delivery.Unreliable);
        Assert.Equal([[1, 2], [3]], host.Poll().Select(e => e.Data!));
        Assert.Equal(new TransportStats(3, 0), player.Stats);

        player.Dispose();
        Assert.Equal(TransportEventKind.Disconnected, Assert.Single(host.Poll()).Kind);
        Assert.Throws<InvalidOperationException>(network.CreateHost);
        host.Dispose();
        Assert.Throws<InvalidOperationException>(() => network.Connect());
    }
}
