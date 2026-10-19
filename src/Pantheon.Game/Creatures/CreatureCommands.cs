using System;

namespace Pantheon.Game.Creatures;

/// <summary>Debug commands for Creatures dealing with each other (ADR-017), as status lines. Engine-agnostic.</summary>
public static class CreatureCommands
{
    /// <summary>Brings another Creature beside the player's, so they meet at once.</summary>
    /// <param name="session">Game session.</param>
    /// <param name="name">The Creature to bring, by name; the nearest when <c>null</c> or empty.</param>
    /// <returns>A status line.</returns>
    public static string BringOther(GameSession session, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.LocalCreature is not { } mine)
        {
            return "You have no Creature";
        }

        return session.BringOtherCreature(name) is { } other
            ? $"{other.Name} comes to {mine.Name}"
            : string.IsNullOrEmpty(name) ? $"No other Creature can come to {mine.Name} now" : $"{name} cannot come to {mine.Name} now";
    }
}
