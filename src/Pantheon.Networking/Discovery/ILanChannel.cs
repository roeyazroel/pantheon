using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Pantheon.Networking.Discovery;

/// <summary>An address and port on the local network.</summary>
/// <param name="Address">The address, as text.</param>
/// <param name="Port">The port.</param>
public readonly record struct LanEndpoint(string Address, int Port);

/// <summary>A datagram that arrived, and where from.</summary>
/// <param name="From">The sender.</param>
/// <param name="Data">The datagram.</param>
public readonly record struct LanPacket(LanEndpoint From, byte[] Data);

/// <summary>
/// Datagrams on the local network for finding worlds (ADR-081): Godot's UDP in the game, an
/// in-memory network in tests. A host's channel listens on <see cref="LanCodec.Port"/>; a player's
/// on a port the system picks.
/// </summary>
public interface ILanChannel : IDisposable
{
    /// <summary>Sends a datagram to <see cref="LanCodec.Port"/> on every computer of the local network, or at least on this one.</summary>
    /// <param name="packet">The datagram.</param>
    void Broadcast(byte[] packet);

    /// <summary>Sends a datagram to one computer.</summary>
    /// <param name="to">Where to.</param>
    /// <param name="packet">The datagram.</param>
    void Send(LanEndpoint to, byte[] packet);

    /// <summary>Returns the datagrams that arrived since the last call, oldest first.</summary>
    /// <returns>The datagrams.</returns>
    IReadOnlyList<LanPacket> Poll();
}

/// <summary>Which addresses count as the local network (ADR-081).</summary>
public static class LanAddress
{
    /// <summary>
    /// Whether an address belongs to a local network: this computer, a private range or a link-local
    /// one. A host answers only these, so its answer is never sent across the internet.
    /// </summary>
    /// <param name="address">The address, as text.</param>
    /// <returns>Whether it is local.</returns>
    public static bool IsLocal(string address)
    {
        if (!IPAddress.TryParse(address, out var ip))
        {
            return false;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Link-local (fe80::/10) and unique local (fc00::/7).
            var bytes = ip.GetAddressBytes();
            return ip.IsIPv6LinkLocal || (bytes[0] & 0xFE) == 0xFC;
        }

        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254);
    }
}
