using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Game.Multiplayer;
using Pantheon.Networking;
using Pantheon.Networking.Transport;

namespace Pantheon.Game.Diagnostics;

/// <summary>The F1 panel's network lines (spec §106 "network authority", ADR-018): who plays which god, round trips and traffic.</summary>
public static class NetworkReadout
{
    /// <summary>The host's lines.</summary>
    /// <param name="host">The host.</param>
    /// <param name="port">The port it listens on.</param>
    /// <param name="session">The host's session, for god names.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> Describe(MultiplayerHost host, int port, GameSession session)
    {
        System.ArgumentNullException.ThrowIfNull(host);
        System.ArgumentNullException.ThrowIfNull(session);
        var seats = host.Seats;
        var lines = new List<string>
        {
            Format("Network: hosting on port {0}, {1} of {2} players joined; {3}", port, seats.Count - 1, NetworkProtocol.MaxClients, Traffic(host.Stats)),
        };
        lines.AddRange(seats.Select(seat => Format(
            "  {0} plays {1}{2}",
            seat.Name,
            session.World.Gods.Find(g => g.Id == seat.GodId)?.Name ?? "?",
            seat.PlayerId == NetworkProtocol.HostPeerId ? " (host)" : host.RoundTripMilliseconds(seat.PlayerId) is { } rtt ? Format(", round trip {0:0} ms", rtt) : string.Empty)));
        return lines;
    }

    /// <summary>A joined player's lines.</summary>
    /// <param name="client">The player's connection.</param>
    /// <param name="address">The host's address and port.</param>
    /// <param name="session">The player's replica, for god names.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> Describe(MultiplayerClient client, string address, GameSession session)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(session);
        var lines = new List<string>
        {
            Format(
                "Network: {0} {1}, host tick {2}, {3}{4}",
                client.Phase switch
                {
                    ClientPhase.Connecting => "connecting to",
                    ClientPhase.Joined => "joined",
                    ClientPhase.Refused => "refused by",
                    ClientPhase.Closed => "left",
                },
                address,
                client.HostTick,
                Traffic(client.Stats),
                client.RoundTripMilliseconds is { } rtt ? Format(", round trip {0:0} ms", rtt) : string.Empty),
        };
        lines.AddRange(client.Seats.Select(seat => Format(
            "  {0} plays {1}{2}",
            seat.Name,
            session.World.Gods.Find(g => g.Id == seat.GodId)?.Name ?? "?",
            seat.PlayerId == client.PlayerId ? " (you)" : seat.PlayerId == NetworkProtocol.HostPeerId ? " (host)" : string.Empty)));
        return lines;
    }

    /// <summary>Traffic so far, in kilobytes.</summary>
    private static string Traffic(TransportStats stats) => Format("sent {0:0} KB, received {1:0} KB", stats.BytesSent / 1024.0, stats.BytesReceived / 1024.0);

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
