using System.Collections.Generic;

namespace Pantheon.Game.Book;

/// <summary>A page of the Book of the World (spec §57's World, Religion, Miracles and Creature panels, ADR-031).</summary>
public enum BookTab
{
    /// <summary>The world: its gods, the race, its dangers and its holy sites.</summary>
    World,

    /// <summary>Religion: the player's faith village by village, every god's faith, and the godless.</summary>
    Religion,

    /// <summary>Every miracle, with what it needs and costs, to aim from here.</summary>
    Miracles,

    /// <summary>The player's Creature in full.</summary>
    Creature,
}

/// <summary>How a line of the book is shown.</summary>
public enum BookTone
{
    /// <summary>A heading.</summary>
    Heading,

    /// <summary>Plain text.</summary>
    Plain,

    /// <summary>Good news, or the player's own.</summary>
    Good,

    /// <summary>A warning.</summary>
    Warning,

    /// <summary>Something not available now.</summary>
    Muted,
}

/// <summary>One line of a page.</summary>
/// <param name="Text">What it says.</param>
/// <param name="Tone">How it is shown.</param>
/// <param name="MiracleId">The miracle the line offers to aim, if any.</param>
/// <param name="CanAim">Whether the miracle may be aimed now.</param>
public sealed record BookLine(string Text, BookTone Tone = BookTone.Plain, string? MiracleId = null, bool CanAim = false);

/// <summary>A page of the book.</summary>
/// <param name="Tab">Which page.</param>
/// <param name="Title">Its title.</param>
/// <param name="Lines">Its lines.</param>
public sealed record BookPage(BookTab Tab, string Title, IReadOnlyList<BookLine> Lines);
