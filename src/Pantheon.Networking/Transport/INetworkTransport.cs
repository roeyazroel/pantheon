using System;
using System.Collections.Generic;

namespace Pantheon.Networking.Transport;

/// <summary>How a packet travels.</summary>
public enum Delivery
{
    /// <summary>Arrives, in order with the other reliable packets (intents, verdicts, events, snapshots).</summary>
    Reliable,

    /// <summary>May be lost or overtaken (frequent state that the next packet replaces).</summary>
    Unreliable,
}

/// <summary>What happened on a transport since the last poll.</summary>
public enum TransportEventKind
{
    /// <summary>A peer connected: a player to the host, or the host to a player.</summary>
    Connected,

    /// <summary>A peer disconnected or could not be reached.</summary>
    Disconnected,

    /// <summary>A packet arrived from a peer.</summary>
    Received,
}

/// <summary>One thing that happened on a transport.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="PeerId">The peer it concerns.</param>
/// <param name="Data">The packet, for <see cref="TransportEventKind.Received"/>.</param>
public readonly record struct TransportEvent(TransportEventKind Kind, int PeerId, byte[]? Data);

/// <summary>Traffic counted by a transport.</summary>
/// <param name="BytesSent">Bytes sent so far.</param>
/// <param name="BytesReceived">Bytes received so far.</param>
public readonly record struct TransportStats(long BytesSent, long BytesReceived);

/// <summary>
/// A host's or a player's connection (ADR-018): Godot's ENet in the game, an in-memory loopback
/// in tests. The host is peer <see cref="NetworkProtocol.HostPeerId"/>; a player sends to it.
/// </summary>
public interface INetworkTransport : IDisposable
{
    /// <summary>Whether this end is the host.</summary>
    bool IsHost { get; }

    /// <summary>Traffic so far.</summary>
    TransportStats Stats { get; }

    /// <summary>Returns what happened since the last call, oldest first.</summary>
    /// <returns>The events.</returns>
    IReadOnlyList<TransportEvent> Poll();

    /// <summary>Sends a packet to a peer.</summary>
    /// <param name="peerId">The peer: a player's id on the host, <see cref="NetworkProtocol.HostPeerId"/> on a player.</param>
    /// <param name="packet">The packet.</param>
    /// <param name="delivery">How it travels.</param>
    void Send(int peerId, byte[] packet, Delivery delivery);

    /// <summary>Ends the connection with a peer.</summary>
    /// <param name="peerId">The peer.</param>
    void Disconnect(int peerId);

    /// <summary>The round trip to a peer in milliseconds, if known.</summary>
    /// <param name="peerId">The peer.</param>
    /// <returns>Milliseconds, or <c>null</c>.</returns>
    float? RoundTripMilliseconds(int peerId);
}
