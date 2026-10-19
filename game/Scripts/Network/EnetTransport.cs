using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Networking;
using Pantheon.Networking.Transport;

namespace Pantheon.Client.Network;

/// <summary>
/// The network over Godot's ENet (spec §68, ADR-003, ADR-018): UDP, with reliable and unreliable
/// packets on separate channels. The core speaks to it only through <see cref="INetworkTransport"/>.
/// </summary>
internal sealed class EnetTransport : INetworkTransport
{
    private const int ReliableChannel = 0;
    private const int UnreliableChannel = 1;

    private readonly ENetMultiplayerPeer _peer;
    private readonly List<TransportEvent> _events = [];
    private bool _connected;
    private long _sent;
    private long _received;

    private EnetTransport(ENetMultiplayerPeer peer, bool isHost)
    {
        _peer = peer;
        IsHost = isHost;
        _peer.PeerConnected += id => OnConnected((int)id);
        _peer.PeerDisconnected += id => _events.Add(new TransportEvent(TransportEventKind.Disconnected, (int)id, null));
    }

    /// <inheritdoc />
    public bool IsHost { get; }

    /// <inheritdoc />
    public TransportStats Stats => new(_sent, _received);

    /// <summary>Starts listening for players on a UDP port.</summary>
    /// <param name="port">The port.</param>
    /// <returns>The host's transport, or why it could not listen.</returns>
    public static (EnetTransport? Transport, string? Failure) Host(int port)
    {
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(port, NetworkProtocol.MaxClients, 2);
        return error == Error.Ok ? (new EnetTransport(peer, isHost: true), null) : (null, $"Could not host on port {port}: {error}");
    }

    /// <summary>Starts connecting to a host.</summary>
    /// <param name="address">The host's address.</param>
    /// <param name="port">Its port.</param>
    /// <returns>The player's transport, or why it could not start.</returns>
    public static (EnetTransport? Transport, string? Failure) Join(string address, int port)
    {
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(address, port, 2);
        return error == Error.Ok ? (new EnetTransport(peer, isHost: false), null) : (null, $"Could not connect to {address}:{port}: {error}");
    }

    /// <inheritdoc />
    public IReadOnlyList<TransportEvent> Poll()
    {
        if (_peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            // Closed: a player that never got through is told once; afterwards there is nothing to poll.
            if (!IsHost && !_connected)
            {
                _connected = true;
                return [new TransportEvent(TransportEventKind.Disconnected, NetworkProtocol.HostPeerId, null)];
            }

            var closed = _events.ToArray();
            _events.Clear();
            return closed;
        }

        _peer.Poll();
        while (_peer.GetAvailablePacketCount() > 0)
        {
            var from = _peer.GetPacketPeer();
            var packet = _peer.GetPacket();
            _received += packet.Length;
            _events.Add(new TransportEvent(TransportEventKind.Received, from, packet));
        }

        var events = _events.ToArray();
        _events.Clear();
        return events;
    }

    /// <inheritdoc />
    public void Send(int peerId, byte[] packet, Delivery delivery)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (_peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
        {
            return;
        }

        _peer.TransferMode = delivery == Delivery.Reliable ? MultiplayerPeer.TransferModeEnum.Reliable : MultiplayerPeer.TransferModeEnum.Unreliable;
        _peer.TransferChannel = delivery == Delivery.Reliable ? ReliableChannel : UnreliableChannel;
        _peer.SetTargetPeer(peerId);
        if (_peer.PutPacket(packet) == Error.Ok)
        {
            _sent += packet.Length;
        }
    }

    /// <inheritdoc />
    public void Disconnect(int peerId)
    {
        if (_peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            return;
        }

        if (IsHost)
        {
            _peer.DisconnectPeer(peerId);
        }
        else
        {
            _peer.Close();
        }
    }

    /// <inheritdoc />
    public float? RoundTripMilliseconds(int peerId)
    {
        if (_peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
        {
            return null;
        }

        var peer = IsHost || peerId == NetworkProtocol.HostPeerId ? _peer.GetPeer(peerId) : null;
        return peer is null ? null : (float)peer.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            // Let the goodbye already queued leave before the connection closes.
            _peer.Poll();
            _peer.Close();
        }
    }

    /// <summary>A peer connected; for a player, that is the host.</summary>
    private void OnConnected(int id)
    {
        _connected = true;
        _events.Add(new TransportEvent(TransportEventKind.Connected, id, null));
    }
}
