using System.Collections.Generic;
using System.Globalization;

namespace Pantheon.Game.Multiplayer;

/// <summary>Words for worlds found on the local network (ADR-081), for the Play Together page and status lines.</summary>
public static class LanText
{
    /// <summary>A world's seed in words.</summary>
    /// <param name="seed">The seed.</param>
    /// <returns>For example "seed 1234".</returns>
    public static string Seed(ulong seed) => "seed " + seed.ToString(CultureInfo.InvariantCulture);

    /// <summary>A found world's title.</summary>
    /// <param name="game">The world.</param>
    /// <returns>For example "Dana's world".</returns>
    public static string Title(LanGame game) => Format("{0}'s world", game.Answer.Host);

    /// <summary>A found world's details: what it is, who plays, and the gods free or why it cannot be joined.</summary>
    /// <param name="game">The world.</param>
    /// <returns>For example "seed 1234 · Spring, year 1 · Dana and Sam play · free: Wynlius, Belrays".</returns>
    public static string Details(LanGame game)
    {
        var answer = game.Answer;
        var who = answer.Players.Count switch
        {
            0 => "nobody plays yet",
            1 => answer.Players[0] + " plays",
            _ => string.Join(", ", answer.Players) + " play",
        };
        var fit = game.Fit switch
        {
            LanFit.Joinable => "free: " + string.Join(", ", answer.FreeGods),
            LanFit.Full => "no god is free",
            LanFit.OtherVersion => "another version of the game",
            LanFit.OtherData => "different game data",
        };
        return string.Join(" · ", new List<string> { answer.World, who, fit }.FindAll(part => part.Length > 0));
    }

    /// <summary>Where to join a found world.</summary>
    /// <param name="game">The world.</param>
    /// <returns>For example "10.0.0.5:7777".</returns>
    public static string Where(LanGame game) => Format("{0}:{1}", game.Address, game.Answer.Port);

    /// <summary>What the page shows while nothing has answered.</summary>
    /// <param name="seconds">Seconds spent looking.</param>
    /// <returns>The line.</returns>
    public static string Looking(double seconds) => seconds < 3.0
        ? "Looking for worlds on your network…"
        : "No world found on your network yet. A friend hosts from Play Together on the same network, or type their address below.";

    /// <summary>A key for a found world as it is listed, to tell when the list changed.</summary>
    /// <param name="game">The world.</param>
    /// <returns>The key.</returns>
    internal static string Key(LanGame game) => Where(game) + "|" + Title(game) + "|" + Details(game);

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
