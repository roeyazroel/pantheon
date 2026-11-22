using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Game.Weather;
using Pantheon.Networking;
using Pantheon.Networking.Discovery;
using Pantheon.Networking.Protocol;

namespace Pantheon.Game.Multiplayer;

/// <summary>
/// A host's voice on the local network (ADR-081): it answers players looking for worlds with what
/// its world is and who may still join. It answers only local addresses, and at most
/// <see cref="AnswersPerSecond"/> times a second.
/// </summary>
public sealed class LanResponder : IDisposable
{
    /// <summary>The most answers sent in a second.</summary>
    public const int AnswersPerSecond = 20;

    private readonly ILanChannel _channel;
    private readonly Func<LanAnswer> _answer;
    private double _window;
    private int _inWindow;

    /// <summary>Starts answering.</summary>
    /// <param name="channel">The channel listening on <see cref="LanCodec.Port"/>.</param>
    /// <param name="answer">The answer as it is now, asked for when a query comes.</param>
    public LanResponder(ILanChannel channel, Func<LanAnswer> answer)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(answer);
        _channel = channel;
        _answer = answer;
    }

    /// <summary>Queries answered so far.</summary>
    public int Answered { get; private set; }

    /// <summary>Queries left unanswered: from outside the local network, or over the limit.</summary>
    public int Ignored { get; private set; }

    /// <summary>Answers the queries that arrived.</summary>
    /// <param name="realDeltaSeconds">Real seconds since the previous call.</param>
    public void Update(double realDeltaSeconds)
    {
        _window += Math.Max(0.0, realDeltaSeconds);
        if (_window >= 1.0)
        {
            (_window, _inWindow) = (0.0, 0);
        }

        byte[]? packet = null;
        foreach (var arrived in _channel.Poll())
        {
            if (!LanCodec.IsQuery(arrived.Data))
            {
                continue;
            }

            if (!LanAddress.IsLocal(arrived.From.Address) || _inWindow >= AnswersPerSecond)
            {
                Ignored++;
                continue;
            }

            packet ??= LanCodec.Encode(_answer());
            _channel.Send(arrived.From, packet);
            _inWindow++;
            Answered++;
        }
    }

    /// <summary>What a hosted world tells the local network: its host, its world, its players and the gods still free.</summary>
    /// <param name="session">The host's session.</param>
    /// <param name="seats">Every player, the host first (<see cref="MultiplayerHost.Seats"/>).</param>
    /// <param name="port">The port the world listens on.</param>
    /// <returns>The answer.</returns>
    public static LanAnswer Advert(GameSession session, IReadOnlyList<PlayerSeat> seats, int port)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(seats);
        var world = session.World;
        var free = world.Gods.Where(g => g.Controller == GodController.Ai).OrderBy(g => g.Id).Select(g => g.Name).ToList();
        var joined = seats.Count(s => s.PlayerId != NetworkProtocol.HostPeerId);
        var open = Math.Min(free.Count, Math.Max(0, NetworkProtocol.MaxClients - joined));
        var seed = LanText.Seed(world.Seed.Value);
        var line = SeasonText.Calendar(session.Content.Weather, world) is { } calendar ? seed + " · " + calendar : seed;
        var host = seats.FirstOrDefault(s => s.PlayerId == NetworkProtocol.HostPeerId)?.Name ?? "Host";
        return new LanAnswer(NetworkProtocol.Version, session.Content.Fingerprint, port, host, line, seats.Select(s => s.Name).ToList(), free, open);
    }

    /// <inheritdoc />
    public void Dispose() => _channel.Dispose();
}
