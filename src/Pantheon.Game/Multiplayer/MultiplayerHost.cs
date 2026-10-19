using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Game.Migration;
using Pantheon.Networking;
using Pantheon.Networking.Protocol;
using Pantheon.Networking.Transport;
using Pantheon.Persistence;

namespace Pantheon.Game.Multiplayer;

/// <summary>
/// The host of a shared world (spec §67, §71, §73, ADR-018, ADR-019). Its session runs the only
/// canonical simulation. A player who joins takes over a god the AI was playing; the host checks
/// each of its intents with the same authorities as the host's own commands, as that god, and
/// answers with a verdict. Players receive the whole world when they join, then each part of it at
/// its own rate (<see cref="Replicator"/>), and every event as it happens. When a player leaves, the
/// AI plays its god again; the god waits for the player to come back under the same name.
/// </summary>
public sealed class MultiplayerHost : IDisposable
{
    private readonly GameSession _session;
    private readonly INetworkTransport _transport;
    private readonly string _hostName;
    private readonly Dictionary<int, PlayerSeat> _seats = [];
    private readonly Dictionary<string, int> _departed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Replicator _replicator = new();
    private readonly TrafficMeter _traffic = new();
    private bool _closed;

    /// <summary>Starts hosting a session's world.</summary>
    /// <param name="session">The host's session; it must not be a replica.</param>
    /// <param name="transport">The host's end of the network.</param>
    /// <param name="hostName">The host player's name.</param>
    public MultiplayerHost(GameSession session, INetworkTransport transport, string hostName)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(transport);
        if (session.IsReplica || !transport.IsHost)
        {
            throw new ArgumentException("A host needs its own world and the host's end of the network.", nameof(session));
        }

        _session = session;
        _transport = transport;
        _hostName = string.IsNullOrWhiteSpace(hostName) ? "Host" : hostName;
        session.KeepsOutbox = true;
        session.DrainOutbox();
        session.WorldReplaced += Reseat;
        PublishPlayers();
    }

    /// <summary>Every player, the host first.</summary>
    public IReadOnlyList<PlayerSeat> Seats => AllSeats();

    /// <summary>Traffic so far.</summary>
    public TransportStats Stats => _transport.Stats;

    /// <summary>Traffic per second, smoothed.</summary>
    public TrafficMeter Traffic => _traffic;

    /// <summary>Handles what arrived, sends the events raised since the last call, and the parts of the world that are due.</summary>
    /// <param name="realDeltaSeconds">Real seconds since the previous call.</param>
    /// <returns>Status lines for the host: players joining and leaving, refusals.</returns>
    public IReadOnlyList<string> Update(double realDeltaSeconds)
    {
        var statuses = new List<string>();
        if (_closed)
        {
            return statuses;
        }

        foreach (var happened in _transport.Poll())
        {
            Handle(happened, statuses);
        }

        var events = _session.DrainOutbox();
        if (events.Count > 0 && _seats.Count > 0)
        {
            Broadcast(new EventBatch(_session.World.Tick, EventCodec.Wrap(events)));
        }

        Replicate(Math.Max(0.0, realDeltaSeconds));
        _traffic.Update(_transport.Stats, realDeltaSeconds);
        return statuses;
    }

    /// <summary>The round trip to a player in milliseconds, if known.</summary>
    /// <param name="playerId">The player.</param>
    /// <returns>Milliseconds, or <c>null</c>.</returns>
    public float? RoundTripMilliseconds(int playerId) => _transport.RoundTripMilliseconds(playerId);

    /// <summary>Ends the session (spec §71): players are told, and the AI takes their gods back.</summary>
    /// <param name="reason">Why, in words.</param>
    public void Close(string reason = "The host ended the session")
    {
        if (_closed)
        {
            return;
        }

        Broadcast(new Goodbye(reason));
        foreach (var playerId in _seats.Keys.ToList())
        {
            Release(playerId);
        }

        _closed = true;
        _session.WorldReplaced -= Reseat;
        _session.KeepsOutbox = false;
        _session.SetPlayers([]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Close();
        _transport.Dispose();
    }

    /// <summary>One thing that happened on the network.</summary>
    private void Handle(TransportEvent happened, List<string> statuses)
    {
        switch (happened.Kind)
        {
            case TransportEventKind.Connected:
                break;
            case TransportEventKind.Disconnected when Release(happened.PeerId) is { } seat:
                statuses.Add(Format("{0} left; the AI plays {1} again", seat.Name, GodName(seat.GodId)));
                break;
            case TransportEventKind.Disconnected:
                break;
            case TransportEventKind.Received when happened.Data is { } packet:
                Receive(happened.PeerId, packet, statuses);
                break;
            case TransportEventKind.Received:
                break;
        }
    }

    /// <summary>A packet from a player: joining, an intent, or leaving. A packet that is not a message ends the connection.</summary>
    private void Receive(int peerId, byte[] packet, List<string> statuses)
    {
        NetMessage message;
        try
        {
            message = MessageCodec.Decode(packet);
        }
        catch (InvalidDataException)
        {
            _transport.Disconnect(peerId);
            return;
        }

        var seated = _seats.TryGetValue(peerId, out var seat);
        switch (message)
        {
            case Hello hello when !seated:
                Join(peerId, hello, statuses);
                break;
            case Goodbye when seated && Release(peerId) is { } left:
                statuses.Add(Format("{0} left; the AI plays {1} again", left.Name, GodName(left.GodId)));
                _transport.Disconnect(peerId);
                break;
            case CastIntent or CreatureIntent or DiplomacyIntent or RefugeeIntent or DomainIntent or PriorityIntent when seated:
                Answer(peerId, seat!.GodId, message);
                _replicator.Hurry();
                break;
            case ViewFocus focus when seated:
                _replicator.Focus(peerId, new Domain.World.GroundPosition(focus.X, focus.Z));
                break;
            default:
                break;
        }
    }

    /// <summary>A player asks to join: the same protocol and data, and a god free for it; it gets the god, the world and the list of players.</summary>
    private void Join(int peerId, Hello hello, List<string> statuses)
    {
        var name = string.IsNullOrWhiteSpace(hello.PlayerName) ? Format("Player {0}", peerId) : hello.PlayerName.Trim();
        var refusal = hello.ProtocolVersion != NetworkProtocol.Version ? Format("The host speaks protocol {0}, you speak {1}", NetworkProtocol.Version, hello.ProtocolVersion)
            : !string.Equals(hello.ContentFingerprint, _session.Content.Fingerprint, StringComparison.Ordinal) ? "Your game data differs from the host's"
            : null;
        // A player coming back under the same name gets its god again, if the AI still plays it.
        var asked = hello.WantedGod ?? (_departed.TryGetValue(name, out var before) ? _session.World.Gods.Find(g => g.Id == before)?.Name : null);
        var god = refusal is null ? FreeGod(asked) ?? (hello.WantedGod is null ? FreeGod(null) : null) : null;
        refusal ??= god is null ? (hello.WantedGod is { } wanted ? Format("{0} is not free", wanted) : "No god is free") : null;
        if (refusal is not null || god is null)
        {
            Send(peerId, new Refused(refusal ?? "No god is free"));
            statuses.Add(Format("{0} could not join: {1}", name, refusal));
            return;
        }

        god.Controller = GodController.Remote;
        _seats[peerId] = new PlayerSeat(peerId, name, god.Id);
        _departed.Remove(name);
        Send(peerId, new Welcome(peerId, god.Id));
        Send(peerId, CreateSnapshot());
        _replicator.Add(peerId, _session.World);
        PublishPlayers();
        statuses.Add(Format("{0} joined and plays {1}", name, god.Name));
    }

    /// <summary>Carries out a player's intent as its god and answers with the verdict (spec §73).</summary>
    private void Answer(int peerId, int godId, NetMessage intent)
    {
        var (requestId, outcome) = intent switch
        {
            CastIntent cast => (cast.RequestId, SessionCommands.CastMiracleAs(_session, godId, cast.MiracleId, cast.Target)),
            CreatureIntent creature => (creature.RequestId, SessionCommands.CommandCreatureAs(_session, godId, creature.Kind, creature.Target)),
            DiplomacyIntent move => (move.RequestId, SessionCommands.CommandDiplomacyAs(_session, godId, move.OtherGodId, move.Kind)),
            RefugeeIntent policy => (policy.RequestId, MigrationCommands.SetPolicyAs(_session, godId, policy.Policy)),
            DomainIntent domain => (domain.RequestId, Domains.DomainCommands.ChooseAs(_session, godId, domain.Domain)),
            PriorityIntent priority => (priority.RequestId, Society.SocietyCommands.SetPriorityAs(_session, godId, priority.SettlementId, priority.Priority)),
            _ => (0, new CommandOutcome(false, "The host does not know that command")),
        };
        Send(peerId, new Verdict(requestId, outcome.Accepted, outcome.Text));
    }

    /// <summary>
    /// The god a joining player gets: the one it asked for if the AI plays it; else the first the AI
    /// plays that no departed player may come back for; else the first the AI plays.
    /// </summary>
    private GodState? FreeGod(string? wanted)
    {
        var free = _session.World.Gods.Where(g => g.Controller == GodController.Ai).OrderBy(g => g.Id).ToList();
        return wanted is not null
            ? free.Find(g => string.Equals(g.Name, wanted.Trim(), StringComparison.OrdinalIgnoreCase))
            : free.Find(g => !_departed.ContainsValue(g.Id)) ?? free.FirstOrDefault();
    }

    /// <summary>Gives a player's god back to the AI and tells everyone; returns the seat, or <c>null</c> for an unseated peer.</summary>
    private PlayerSeat? Release(int peerId)
    {
        if (!_seats.Remove(peerId, out var seat))
        {
            return null;
        }

        _replicator.Remove(peerId);
        _departed[seat.Name] = seat.GodId;
        if (_session.World.Gods.Find(g => g.Id == seat.GodId) is { Controller: GodController.Remote } god)
        {
            god.Controller = GodController.Ai;
        }

        PublishPlayers();
        return seat;
    }

    /// <summary>
    /// The host started or loaded another world: the players keep their gods by id, as long as the
    /// new world has them, and get the new world at once. A player whose god is gone is let go.
    /// </summary>
    private void Reseat()
    {
        _session.KeepsOutbox = true;
        foreach (var (playerId, seat) in _seats.ToList())
        {
            if (_session.World.Gods.Find(g => g.Id == seat.GodId && g.Controller != GodController.Human) is { } god)
            {
                god.Controller = GodController.Remote;
            }
            else
            {
                Send(playerId, new Goodbye("Your god is not in the host's new world"));
                _seats.Remove(playerId);
                _transport.Disconnect(playerId);
            }
        }

        PublishPlayers();
        Broadcast(CreateSnapshot());
        _replicator.SyncedAll(_session.World);
    }

    /// <summary>Sends each player the whole world or the parts of it that are due (ADR-019).</summary>
    private void Replicate(double seconds)
    {
        if (_seats.Count == 0)
        {
            return;
        }

        var (snapshots, updates) = _replicator.Due(_session.World, seconds);
        if (snapshots.Count > 0)
        {
            var packet = MessageCodec.Encode(CreateSnapshot());
            foreach (var playerId in snapshots)
            {
                _transport.Send(playerId, packet, Delivery.Reliable);
            }
        }

        // The same part goes to several players as one encoded packet.
        var encoded = new Dictionary<(WorldSection, string), byte[]>();
        foreach (var (playerId, update) in updates)
        {
            if (!encoded.TryGetValue((update.Section, update.Data), out var packet))
            {
                packet = encoded[(update.Section, update.Data)] = MessageCodec.Encode(update);
            }

            _transport.Send(playerId, packet, update.Section == WorldSection.CreatureMotion ? Delivery.Unreliable : Delivery.Reliable);
        }
    }

    /// <summary>Tells the session and every player who plays which god.</summary>
    private void PublishPlayers()
    {
        var seats = AllSeats();
        _session.SetPlayers(seats.Select(s => new KeyValuePair<int, string>(s.GodId, s.Name)));
        if (_seats.Count > 0)
        {
            Broadcast(new PlayerList(seats));
        }
    }

    /// <summary>The host's seat, then the players' in id order.</summary>
    private List<PlayerSeat> AllSeats()
    {
        var seats = new List<PlayerSeat>();
        if (_session.LocalGodId is { } host)
        {
            seats.Add(new PlayerSeat(NetworkProtocol.HostPeerId, _hostName, host));
        }

        seats.AddRange(_seats.Values.OrderBy(s => s.PlayerId));
        return seats;
    }

    /// <summary>The whole world now, with the host's clock.</summary>
    private Snapshot CreateSnapshot() =>
        new(_session.World.Tick, _session.Scheduler.TimeScale, _session.Scheduler.IsPaused, WorldSaveSerializer.SerializeCompact(_session.World));

    /// <summary>Sends a message to every seated player.</summary>
    private void Broadcast(NetMessage message)
    {
        if (_seats.Count == 0)
        {
            return;
        }

        var packet = MessageCodec.Encode(message);
        foreach (var playerId in _seats.Keys.Order())
        {
            _transport.Send(playerId, packet, Delivery.Reliable);
        }
    }

    /// <summary>Sends a message to one peer.</summary>
    private void Send(int peerId, NetMessage message) => _transport.Send(peerId, MessageCodec.Encode(message), Delivery.Reliable);

    /// <summary>A god's name.</summary>
    private string GodName(int godId) => _session.World.Gods.Find(g => g.Id == godId)?.Name ?? "its god";

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
