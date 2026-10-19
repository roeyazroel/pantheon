using System;
using System.Collections.Generic;

namespace Pantheon.Networking.Transport;

/// <summary>
/// An in-memory network of one host and its players, for headless tests (ADR-018). Packets arrive
/// at the receiver's next <see cref="INetworkTransport.Poll"/>, in order, reliable or not.
/// </summary>
public sealed class LoopbackNetwork
{
    private readonly Dictionary<int, Endpoint> _endpoints = [];
    private int _nextPlayerId = NetworkProtocol.HostPeerId + 1;

    /// <summary>Creates the host's end.</summary>
    /// <returns>The host transport.</returns>
    /// <exception cref="InvalidOperationException">There is a host already.</exception>
    public INetworkTransport CreateHost()
    {
        if (_endpoints.ContainsKey(NetworkProtocol.HostPeerId))
        {
            throw new InvalidOperationException("The loopback network has a host already.");
        }

        var host = new Endpoint(this, NetworkProtocol.HostPeerId, isHost: true);
        _endpoints[host.Id] = host;
        return host;
    }

    /// <summary>Connects a new player to the host: each sees the other connect at its next poll.</summary>
    /// <returns>The player's transport.</returns>
    /// <exception cref="InvalidOperationException">There is no host.</exception>
    public INetworkTransport Connect()
    {
        if (!_endpoints.TryGetValue(NetworkProtocol.HostPeerId, out var host))
        {
            throw new InvalidOperationException("The loopback network has no host to connect to.");
        }

        var player = new Endpoint(this, _nextPlayerId++, isHost: false);
        _endpoints[player.Id] = player;
        host.Inbox.Enqueue(new TransportEvent(TransportEventKind.Connected, player.Id, null));
        player.Inbox.Enqueue(new TransportEvent(TransportEventKind.Connected, NetworkProtocol.HostPeerId, null));
        return player;
    }

    /// <summary>Delivers a packet, or nothing when the receiver is gone.</summary>
    private void Deliver(int from, int to, byte[] packet)
    {
        if (_endpoints.TryGetValue(to, out var receiver))
        {
            receiver.Inbox.Enqueue(new TransportEvent(TransportEventKind.Received, from, packet));
            receiver.Received += packet.Length;
        }
    }

    /// <summary>Ends the link between two endpoints; both hear of it, and a host that leaves ends every link.</summary>
    private void Cut(Endpoint from, int to)
    {
        if (from.IsHost && to == from.Id)
        {
            foreach (var endpoint in new List<Endpoint>(_endpoints.Values))
            {
                if (!endpoint.IsHost)
                {
                    endpoint.Inbox.Enqueue(new TransportEvent(TransportEventKind.Disconnected, from.Id, null));
                    _endpoints.Remove(endpoint.Id);
                }
            }

            _endpoints.Remove(from.Id);
            return;
        }

        if (_endpoints.TryGetValue(to, out var other))
        {
            other.Inbox.Enqueue(new TransportEvent(TransportEventKind.Disconnected, from.Id, null));
        }

        from.Inbox.Enqueue(new TransportEvent(TransportEventKind.Disconnected, to, null));
        _endpoints.Remove(from.IsHost ? to : from.Id);
    }

    /// <summary>One end of the loopback.</summary>
    private sealed class Endpoint(LoopbackNetwork network, int id, bool isHost) : INetworkTransport
    {
        public int Id { get; } = id;

        public bool IsHost { get; } = isHost;

        public Queue<TransportEvent> Inbox { get; } = new();

        public long Received { get; set; }

        private long Sent { get; set; }

        public TransportStats Stats => new(Sent, Received);

        public IReadOnlyList<TransportEvent> Poll()
        {
            var events = Inbox.ToArray();
            Inbox.Clear();
            return events;
        }

        public void Send(int peerId, byte[] packet, Delivery delivery)
        {
            ArgumentNullException.ThrowIfNull(packet);
            Sent += packet.Length;
            network.Deliver(Id, peerId, packet);
        }

        public void Disconnect(int peerId) => network.Cut(this, peerId);

        public float? RoundTripMilliseconds(int peerId) => 0f;

        public void Dispose() => network.Cut(this, IsHost ? Id : NetworkProtocol.HostPeerId);
    }
}
