using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Pantheon.Networking;
using Pantheon.Networking.Protocol;
using Pantheon.Networking.Transport;
using Pantheon.Persistence;

namespace Pantheon.Game.Multiplayer;

/// <summary>Where a joined player's connection stands.</summary>
public enum ClientPhase
{
    /// <summary>Waiting for the host to answer.</summary>
    Connecting,

    /// <summary>Playing a god in the host's world.</summary>
    Joined,

    /// <summary>The host turned the player away.</summary>
    Refused,

    /// <summary>The session is over: the host left, or the player did.</summary>
    Closed,
}

/// <summary>
/// A player connected to a host (spec §67, §73, ADR-018). It asks to join and, once the host has
/// given it a god and the world, holds a replica <see cref="GameSession"/>. Its commands go to the
/// host as intents, whose verdicts come back as status lines; the host's snapshots replace the
/// replica's world and its events become the player's alerts and effects.
/// </summary>
public sealed class MultiplayerClient : IDisposable
{
    private readonly GameContent _content;
    private readonly INetworkTransport _transport;
    private readonly string _playerName;
    private readonly string? _wantedGod;
    private readonly List<PlayerSeat> _seats = [];
    private int _nextRequestId = 1;

    /// <summary>Starts joining a host over a transport that is connecting to it.</summary>
    /// <param name="content">Validated content; the host refuses content that differs from its own.</param>
    /// <param name="transport">The player's end of the network.</param>
    /// <param name="playerName">The name others see.</param>
    /// <param name="wantedGod">The god it wants to play, or <c>null</c> for any.</param>
    public MultiplayerClient(GameContent content, INetworkTransport transport, string playerName, string? wantedGod = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(transport);
        _content = content;
        _transport = transport;
        _playerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        _wantedGod = string.IsNullOrWhiteSpace(wantedGod) ? null : wantedGod.Trim();
    }

    /// <summary>Where the connection stands.</summary>
    public ClientPhase Phase { get; private set; } = ClientPhase.Connecting;

    /// <summary>Why the host turned the player away, or why the session ended.</summary>
    public string? Reason { get; private set; }

    /// <summary>The replica of the host's world, once joined.</summary>
    public GameSession? Session { get; private set; }

    /// <summary>The player's peer id, once joined.</summary>
    public int? PlayerId { get; private set; }

    /// <summary>The god the player plays, once joined.</summary>
    public int? GodId { get; private set; }

    /// <summary>Every player, the host first.</summary>
    public IReadOnlyList<PlayerSeat> Seats => _seats;

    /// <summary>The host's tick in the latest snapshot.</summary>
    public long HostTick { get; private set; }

    /// <summary>Traffic so far.</summary>
    public TransportStats Stats => _transport.Stats;

    /// <summary>The round trip to the host in milliseconds, if known.</summary>
    public float? RoundTripMilliseconds => _transport.RoundTripMilliseconds(NetworkProtocol.HostPeerId);

    /// <summary>Handles what arrived from the host.</summary>
    /// <returns>Status lines: verdicts, joining, players coming and going, the end of the session.</returns>
    public IReadOnlyList<string> Update()
    {
        var statuses = new List<string>();
        foreach (var happened in _transport.Poll())
        {
            switch (happened.Kind)
            {
                case TransportEventKind.Connected:
                    Send(new Hello(NetworkProtocol.Version, _content.Fingerprint, _playerName, _wantedGod));
                    break;
                case TransportEventKind.Disconnected when Phase is ClientPhase.Connecting or ClientPhase.Joined:
                    End(Phase == ClientPhase.Joined ? "The host is gone: the session is over" : "Could not reach the host", statuses);
                    break;
                case TransportEventKind.Disconnected:
                    break;
                case TransportEventKind.Received when happened.Data is { } packet:
                    Receive(packet, statuses);
                    break;
                case TransportEventKind.Received:
                    break;
            }
        }

        return statuses;
    }

    /// <summary>Sends an intent to the host (spec §73); its verdict arrives later as a status line.</summary>
    /// <param name="create">Builds the intent from its request number.</param>
    /// <returns>A status line saying the host was asked, or that it cannot be.</returns>
    public string Request(Func<int, NetMessage> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        if (Phase != ClientPhase.Joined)
        {
            return "You are not in a session";
        }

        Send(create(_nextRequestId++));
        return "Asking the host...";
    }

    /// <summary>Leaves the session.</summary>
    public void Leave()
    {
        if (Phase is ClientPhase.Connecting or ClientPhase.Joined)
        {
            Send(new Goodbye("The player left"));
            Phase = ClientPhase.Closed;
            Reason = "You left the session";
            if (Session is { } session)
            {
                session.Scheduler.IsPaused = true;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Leave();
        _transport.Dispose();
    }

    /// <summary>A message from the host.</summary>
    private void Receive(byte[] packet, List<string> statuses)
    {
        NetMessage message;
        try
        {
            message = MessageCodec.Decode(packet);
        }
        catch (InvalidDataException)
        {
            return;
        }

        switch (message)
        {
            case Welcome welcome:
                (PlayerId, GodId) = (welcome.PlayerId, welcome.GodId);
                break;
            case Snapshot snapshot:
                TakeSnapshot(snapshot, statuses);
                break;
            case EventBatch batch:
                Session?.ReceiveEvents(EventCodec.Unwrap(batch.Events));
                break;
            case PlayerList list:
                TakePlayers(list, statuses);
                break;
            case Verdict verdict:
                statuses.Add(verdict.Text);
                break;
            case Refused refused:
                Phase = ClientPhase.Refused;
                Reason = refused.Reason;
                statuses.Add("The host turned you away: " + refused.Reason);
                _transport.Disconnect(NetworkProtocol.HostPeerId);
                break;
            case Goodbye goodbye:
                End(goodbye.Reason, statuses);
                break;
            default:
                break;
        }
    }

    /// <summary>The host's world: the first makes the replica, later ones replace its world.</summary>
    private void TakeSnapshot(Snapshot snapshot, List<string> statuses)
    {
        if (GodId is not { } godId || Phase is ClientPhase.Refused or ClientPhase.Closed)
        {
            return;
        }

        var world = WorldSaveSerializer.Deserialize(snapshot.World);
        HostTick = snapshot.Tick;
        if (Session is null)
        {
            Session = GameSession.CreateReplica(_content, world, godId, this);
            Session.Scheduler.TimeScale = snapshot.TimeScale;
            Session.Scheduler.IsPaused = snapshot.Paused;
            Session.SetPlayers(_seats.Select(s => new KeyValuePair<int, string>(s.GodId, s.Name)));
            Phase = ClientPhase.Joined;
            statuses.Add(Format("You joined and play {0}", world.Gods.Find(g => g.Id == godId)?.Name ?? "a god"));
            return;
        }

        Session.ApplySnapshot(world, snapshot.TimeScale, snapshot.Paused);
    }

    /// <summary>Who plays which god now; players who came or went are announced.</summary>
    private void TakePlayers(PlayerList list, List<string> statuses)
    {
        var before = _seats.Select(s => s.PlayerId).ToHashSet();
        var after = list.Seats.Select(s => s.PlayerId).ToHashSet();

        // The first list names who was there already; only later ones bring news.
        if (Phase == ClientPhase.Joined && before.Count > 0)
        {
            foreach (var seat in list.Seats.Where(s => !before.Contains(s.PlayerId) && s.PlayerId != PlayerId))
            {
                statuses.Add(Format("{0} joined", seat.Name));
            }

            foreach (var seat in _seats.Where(s => !after.Contains(s.PlayerId)))
            {
                statuses.Add(Format("{0} left", seat.Name));
            }
        }

        _seats.Clear();
        _seats.AddRange(list.Seats);
        Session?.SetPlayers(_seats.Select(s => new KeyValuePair<int, string>(s.GodId, s.Name)));
    }

    /// <summary>The session ends: the replica stops where it is.</summary>
    private void End(string reason, List<string> statuses)
    {
        Phase = ClientPhase.Closed;
        Reason = reason;
        statuses.Add(reason);
        if (Session is { } session)
        {
            session.Scheduler.IsPaused = true;
        }
    }

    /// <summary>Sends a message to the host.</summary>
    private void Send(NetMessage message) => _transport.Send(NetworkProtocol.HostPeerId, MessageCodec.Encode(message), Delivery.Reliable);

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
