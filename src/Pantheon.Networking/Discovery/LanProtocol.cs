using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Pantheon.Networking.Discovery;

/// <summary>
/// A host's answer to a player looking for worlds on the local network (ADR-081): what the Play
/// Together page lists, and what a player needs to tell whether it can join.
/// </summary>
/// <param name="Protocol">The host's <see cref="NetworkProtocol.Version"/>.</param>
/// <param name="Content">The fingerprint of the host's game data.</param>
/// <param name="Port">The port the host's world listens on.</param>
/// <param name="Host">The host player's name.</param>
/// <param name="World">The world in a few words, such as "seed 1234 · Spring, year 1".</param>
/// <param name="Players">Who plays, the host first.</param>
/// <param name="FreeGods">The gods the AI plays, which a joining player may take.</param>
/// <param name="Open">How many more players may join.</param>
public sealed record LanAnswer(
    int Protocol,
    string Content,
    int Port,
    string Host,
    string World,
    IReadOnlyList<string> Players,
    IReadOnlyList<string> FreeGods,
    int Open);

/// <summary>
/// The packets that find worlds on the local network (ADR-081). A player broadcasts the query to
/// <see cref="LanCodec.Port"/>; each host there answers it alone with a <see cref="LanAnswer"/>. The
/// packets start with a mark of the game, so anything else on the port is left unanswered.
/// </summary>
public static class LanCodec
{
    /// <summary>The UDP port hosts listen on for players looking for worlds.</summary>
    public const int Port = 7778;

    /// <summary>The largest answer sent or read, to fit one datagram on any network.</summary>
    public const int MaxPacketBytes = 1200;

    /// <summary>The longest name or line in an answer.</summary>
    public const int MaxText = 64;

    /// <summary>The most names in a list of an answer.</summary>
    public const int MaxNames = 16;

    /// <summary>The longest fingerprint of game data an answer carries; never cut, since it must match whole.</summary>
    public const int MaxFingerprint = 128;

    private static readonly byte[] QueryMark = "PANTHEON?"u8.ToArray();
    private static readonly byte[] AnswerMark = "PANTHEON!"u8.ToArray();
    private static readonly JsonSerializerOptions Options = new();

    /// <summary>The query a player broadcasts.</summary>
    public static byte[] Query => QueryMark.ToArray();

    /// <summary>Whether a packet is a player's query. A later version may add to it; the mark is enough.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>Whether to answer it.</returns>
    public static bool IsQuery(byte[] packet) => packet is not null && packet.AsSpan().StartsWith(QueryMark);

    /// <summary>
    /// Encodes a host's answer in one datagram: its names are cut short, and shorter still, and
    /// fewer, until it fits <see cref="MaxPacketBytes"/>.
    /// </summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The packet.</returns>
    /// <exception cref="ArgumentException">The fingerprint is longer than <see cref="MaxFingerprint"/>.</exception>
    public static byte[] Encode(LanAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer.Content is not { Length: <= MaxFingerprint })
        {
            throw new ArgumentException("The fingerprint is too long to send.", nameof(answer));
        }

        foreach (var (text, names) in new[] { (MaxText, MaxNames), (24, 8), (12, 4), (12, 0) })
        {
            var packet = Pack(answer, text, names);
            if (packet.Length <= MaxPacketBytes)
            {
                return packet;
            }
        }

        throw new ArgumentException("The answer does not fit one datagram.", nameof(answer));
    }

    /// <summary>
    /// Reads a host's answer. Members it does not know are skipped, so a later version's host still
    /// shows as another version rather than not at all; anything else unexpected is not an answer.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The answer, or <c>null</c> when the packet is not a valid one.</returns>
    public static LanAnswer? Read(byte[] packet)
    {
        if (packet is null || packet.Length > MaxPacketBytes || !packet.AsSpan().StartsWith(AnswerMark))
        {
            return null;
        }

        LanAnswer? answer;
        try
        {
            answer = JsonSerializer.Deserialize<LanAnswer>(packet.AsSpan(AnswerMark.Length), Options);
        }
        catch (JsonException)
        {
            return null;
        }

        return answer is not null && IsValid(answer) ? answer : null;
    }

    /// <summary>Whether every member of an answer is present and in range.</summary>
    private static bool IsValid(LanAnswer answer) =>
        answer.Protocol > 0
        && answer.Port is > 0 and <= 65535
        && answer.Open is >= 0 and <= MaxNames
        && answer.Content is { Length: <= MaxFingerprint } && Fits(answer.Host) && answer.Host.Length > 0 && Fits(answer.World)
        && answer.Players is { Count: <= MaxNames } players && players.All(Fits)
        && answer.FreeGods is { Count: <= MaxNames } gods && gods.All(Fits);

    /// <summary>Whether a text is present and short enough.</summary>
    private static bool Fits(string? text) => text is not null && text.Length <= MaxText;

    /// <summary>The answer after the mark, its texts at most so long and its lists at most so many.</summary>
    private static byte[] Pack(LanAnswer answer, int text, int names)
    {
        string Cut(string? value) => (value ?? string.Empty).Length <= text ? value ?? string.Empty : value![..text];
        var fitted = answer with
        {
            Host = Cut(answer.Host),
            World = Cut(answer.World),
            Players = (answer.Players ?? []).Take(names).Select(Cut).ToList(),
            FreeGods = (answer.FreeGods ?? []).Take(names).Select(Cut).ToList(),
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(fitted, Options);
        var packet = new byte[AnswerMark.Length + json.Length];
        AnswerMark.CopyTo(packet, 0);
        json.CopyTo(packet, AnswerMark.Length);
        return packet;
    }
}
