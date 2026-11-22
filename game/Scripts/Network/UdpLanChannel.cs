using System.Collections.Generic;
using Godot;
using Pantheon.Networking.Discovery;

namespace Pantheon.Client.Network;

/// <summary>
/// Finding worlds on the local network over Godot's UDP (ADR-081). A host's channel listens on
/// <see cref="LanCodec.Port"/>; a player's takes a port the system picks and broadcasts from it.
/// </summary>
internal sealed class UdpLanChannel : ILanChannel
{
    private const string BroadcastAddress = "255.255.255.255";
    private const string ThisComputer = "127.0.0.1";

    private readonly PacketPeerUdp _socket;

    private UdpLanChannel(PacketPeerUdp socket) => _socket = socket;

    /// <summary>A host's channel, listening on the discovery port.</summary>
    /// <returns>The channel, or why it could not listen.</returns>
    public static (UdpLanChannel? Channel, string? Failure) Listen() => Open(LanCodec.Port);

    /// <summary>A player's channel, on a port the system picks.</summary>
    /// <returns>The channel, or why it could not open.</returns>
    public static (UdpLanChannel? Channel, string? Failure) Ask() => Open(0);

    /// <inheritdoc />
    public void Broadcast(byte[] packet)
    {
        // Without a network the broadcast cannot leave; a host on this computer still hears it here.
        if (SendTo(BroadcastAddress, LanCodec.Port, packet) != Error.Ok)
        {
            SendTo(ThisComputer, LanCodec.Port, packet);
        }
    }

    /// <inheritdoc />
    public void Send(LanEndpoint to, byte[] packet) => SendTo(to.Address, to.Port, packet);

    /// <inheritdoc />
    public IReadOnlyList<LanPacket> Poll()
    {
        var packets = new List<LanPacket>();
        while (_socket.GetAvailablePacketCount() > 0)
        {
            var data = _socket.GetPacket();
            packets.Add(new LanPacket(new LanEndpoint(_socket.GetPacketIP(), _socket.GetPacketPort()), data));
        }

        return packets;
    }

    /// <inheritdoc />
    public void Dispose() => _socket.Close();

    /// <summary>Binds a socket to a port on every IPv4 address of this computer.</summary>
    private static (UdpLanChannel? Channel, string? Failure) Open(int port)
    {
        var socket = new PacketPeerUdp();
        socket.SetBroadcastEnabled(true);
        var error = socket.Bind(port, "0.0.0.0");
        return error == Error.Ok
            ? (new UdpLanChannel(socket), null)
            : (null, $"UDP port {port} is unavailable: {error}");
    }

    /// <summary>Sends one datagram.</summary>
    private Error SendTo(string address, int port, byte[] packet)
    {
        var error = _socket.SetDestAddress(address, port);
        return error == Error.Ok ? _socket.PutPacket(packet) : error;
    }
}
