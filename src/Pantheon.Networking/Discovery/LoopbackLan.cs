using System;
using System.Collections.Generic;

namespace Pantheon.Networking.Discovery;

/// <summary>
/// An in-memory local network of computers, for headless tests (ADR-081). A broadcast reaches every
/// channel listening on <see cref="LanCodec.Port"/>; a datagram arrives at the receiver's next poll.
/// </summary>
public sealed class LoopbackLan
{
    private readonly Dictionary<LanEndpoint, Channel> _channels = [];
    private int _nextPort = 50_000;

    /// <summary>A host's channel: listening on the discovery port of a computer.</summary>
    /// <param name="address">The computer's address.</param>
    /// <returns>The channel.</returns>
    /// <exception cref="InvalidOperationException">That computer listens on the port already.</exception>
    public ILanChannel Listen(string address)
    {
        var endpoint = new LanEndpoint(address, LanCodec.Port);
        if (_channels.ContainsKey(endpoint))
        {
            throw new InvalidOperationException($"{address} listens on port {LanCodec.Port} already.");
        }

        return _channels[endpoint] = new Channel(this, endpoint);
    }

    /// <summary>A player's channel, on a port of its own.</summary>
    /// <param name="address">The computer's address.</param>
    /// <returns>The channel.</returns>
    public ILanChannel Open(string address)
    {
        var endpoint = new LanEndpoint(address, _nextPort++);
        return _channels[endpoint] = new Channel(this, endpoint);
    }

    /// <summary>Delivers a datagram, or nothing when nobody listens there.</summary>
    private void Deliver(LanEndpoint from, LanEndpoint to, byte[] packet)
    {
        if (_channels.TryGetValue(to, out var receiver))
        {
            receiver.Inbox.Enqueue(new LanPacket(from, (byte[])packet.Clone()));
        }
    }

    /// <summary>Delivers a datagram to every computer's discovery port.</summary>
    private void DeliverToAll(LanEndpoint from, byte[] packet)
    {
        foreach (var to in new List<LanEndpoint>(_channels.Keys))
        {
            if (to.Port == LanCodec.Port)
            {
                Deliver(from, to, packet);
            }
        }
    }

    /// <summary>One computer's channel.</summary>
    private sealed class Channel(LoopbackLan network, LanEndpoint endpoint) : ILanChannel
    {
        public Queue<LanPacket> Inbox { get; } = new();

        public void Broadcast(byte[] packet) => network.DeliverToAll(endpoint, packet);

        public void Send(LanEndpoint to, byte[] packet) => network.Deliver(endpoint, to, packet);

        public IReadOnlyList<LanPacket> Poll()
        {
            var packets = Inbox.ToArray();
            Inbox.Clear();
            return packets;
        }

        public void Dispose() => network._channels.Remove(endpoint);
    }
}
