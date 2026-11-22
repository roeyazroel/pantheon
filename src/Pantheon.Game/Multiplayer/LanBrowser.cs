using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Networking;
using Pantheon.Networking.Discovery;

namespace Pantheon.Game.Multiplayer;

/// <summary>Whether this game can join a world found on the local network.</summary>
public enum LanFit
{
    /// <summary>The same game, with a god free.</summary>
    Joinable,

    /// <summary>Every god a player may take is taken.</summary>
    Full,

    /// <summary>Another version of the game: the host would refuse it.</summary>
    OtherVersion,

    /// <summary>The same version with other game data: the host would refuse it.</summary>
    OtherData,
}

/// <summary>A world found on the local network.</summary>
/// <param name="Address">The host's address.</param>
/// <param name="Answer">What the host said of its world; its port is the one to join.</param>
/// <param name="Fit">Whether this game can join it.</param>
public sealed record LanGame(string Address, LanAnswer Answer, LanFit Fit);

/// <summary>
/// A player looking for worlds on the local network (ADR-081): it asks every
/// <see cref="AskEverySeconds"/> and lists the hosts that answered lately, the joinable first. A
/// host not heard from for <see cref="ForgetAfterSeconds"/> has gone.
/// </summary>
public sealed class LanBrowser : IDisposable
{
    /// <summary>How often the question goes out.</summary>
    public const double AskEverySeconds = 1.0;

    /// <summary>How long a host that stopped answering stays listed.</summary>
    public const double ForgetAfterSeconds = 3.5;

    /// <summary>The address <c>--join</c> takes to mean the first joinable world found.</summary>
    public const string AnyHost = "lan";

    private readonly ILanChannel _channel;
    private readonly string _content;
    private readonly Dictionary<LanEndpoint, (LanAnswer Answer, double HeardAt)> _heard = [];
    private double _clock;
    private double _nextAsk;
    private string _listed = string.Empty;

    /// <summary>Starts looking.</summary>
    /// <param name="channel">A channel on a port of its own.</param>
    /// <param name="contentFingerprint">This game's data, to tell which worlds it could join.</param>
    public LanBrowser(ILanChannel channel, string contentFingerprint)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
        _content = contentFingerprint ?? string.Empty;
    }

    /// <summary>The worlds found: the joinable first, then by host and address.</summary>
    public IReadOnlyList<LanGame> Games { get; private set; } = [];

    /// <summary>Real seconds spent looking.</summary>
    public double Seconds => _clock;

    /// <summary>Questions sent so far.</summary>
    public int Asked { get; private set; }

    /// <summary>Whether an address given to <c>--join</c> means the first joinable world found.</summary>
    /// <param name="address">The address.</param>
    /// <returns>Whether it is <see cref="AnyHost"/>.</returns>
    public static bool IsAnyHost(string address) => string.Equals(address, AnyHost, StringComparison.OrdinalIgnoreCase);

    /// <summary>Asks when it is time, and takes in the answers.</summary>
    /// <param name="realDeltaSeconds">Real seconds since the previous call.</param>
    /// <returns>Whether <see cref="Games"/> changed.</returns>
    public bool Update(double realDeltaSeconds)
    {
        _clock += Math.Max(0.0, realDeltaSeconds);
        if (_clock >= _nextAsk)
        {
            _channel.Broadcast(LanCodec.Query);
            Asked++;
            _nextAsk = _clock + AskEverySeconds;
        }

        foreach (var arrived in _channel.Poll())
        {
            if (LanCodec.Read(arrived.Data) is { } answer)
            {
                _heard[new LanEndpoint(arrived.From.Address, answer.Port)] = (answer, _clock);
            }
        }

        foreach (var gone in _heard.Where(p => _clock - p.Value.HeardAt > ForgetAfterSeconds).Select(p => p.Key).ToList())
        {
            _heard.Remove(gone);
        }

        Games = _heard
            .Select(p => new LanGame(p.Key.Address, p.Value.Answer, FitOf(p.Value.Answer)))
            .OrderBy(g => g.Fit)
            .ThenBy(g => g.Answer.Host, StringComparer.Ordinal)
            .ThenBy(g => g.Address, StringComparer.Ordinal)
            .ThenBy(g => g.Answer.Port)
            .ToList();
        var listed = string.Join('\n', Games.Select(g => LanText.Key(g)));
        var changed = !string.Equals(listed, _listed, StringComparison.Ordinal);
        _listed = listed;
        return changed;
    }

    /// <summary>The first joinable world, with the god asked for free if one is asked for.</summary>
    /// <param name="wantedGod">The god asked for, by name, or <c>null</c>.</param>
    /// <returns>The world, or <c>null</c>.</returns>
    public LanGame? Pick(string? wantedGod) => Games.FirstOrDefault(g =>
        g.Fit == LanFit.Joinable
        && (wantedGod is null || g.Answer.FreeGods.Any(god => string.Equals(god, wantedGod.Trim(), StringComparison.OrdinalIgnoreCase))));

    /// <inheritdoc />
    public void Dispose() => _channel.Dispose();

    /// <summary>Whether this game could join a host that answered so.</summary>
    private LanFit FitOf(LanAnswer answer) =>
        answer.Protocol != NetworkProtocol.Version ? LanFit.OtherVersion
        : !string.Equals(answer.Content, _content, StringComparison.Ordinal) ? LanFit.OtherData
        : answer.Open <= 0 || answer.FreeGods.Count == 0 ? LanFit.Full
        : LanFit.Joinable;
}
