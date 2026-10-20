using System;

namespace Pantheon.Persistence.Profile;

/// <summary>How a save came to be (ADR-060): the player's own, the periodic autosave, or the quicksave (F5).</summary>
public enum SaveKind
{
    /// <summary>Saved by the player from the pause menu; every one is kept.</summary>
    Manual,

    /// <summary>The periodic autosave; a world keeps only its newest.</summary>
    Auto,

    /// <summary>The quicksave (F5); a world keeps only its newest.</summary>
    Quick,
}

/// <summary>
/// A world the player has started (ADR-060): what it is, whom the player plays, how far it has gone
/// and when it was last played. Its saves hang from it.
/// </summary>
public sealed record WorldRecord
{
    /// <summary>The record's id; 0 before it is stored.</summary>
    public long Id { get; init; }

    /// <summary>The world's name, given by the player or after its first village.</summary>
    public required string Name { get; init; }

    /// <summary>The god the player plays.</summary>
    public required string God { get; init; }

    /// <summary>The world's seed.</summary>
    public required ulong Seed { get; init; }

    /// <summary>The world's size, in words ("Small").</summary>
    public required string Size { get; init; }

    /// <summary>How it may be won, in words.</summary>
    public required string Victory { get; init; }

    /// <summary>Whether it turns through four seasons.</summary>
    public required bool Seasons { get; init; }

    /// <summary>The species of the player's Creature.</summary>
    public required string Species { get; init; }

    /// <summary>When it was started, in UTC.</summary>
    public required DateTime CreatedUtc { get; init; }

    /// <summary>When it was last saved, in UTC.</summary>
    public required DateTime PlayedUtc { get; init; }

    /// <summary>Real seconds it has been played.</summary>
    public double PlaySeconds { get; init; }

    /// <summary>The tick of its newest save.</summary>
    public long Tick { get; init; }

    /// <summary>The player's followers at its newest save.</summary>
    public int Followers { get; init; }

    /// <summary>The player's rank at its newest save, in words.</summary>
    public string Rank { get; init; } = string.Empty;

    /// <summary>How it ended ("Won", "Lost"), or empty while it goes on.</summary>
    public string Outcome { get; init; } = string.Empty;
}

/// <summary>One save of a world (ADR-060): the versioned JSON of its <c>WorldState</c>, compressed.</summary>
/// <param name="Id">The save's id.</param>
/// <param name="WorldId">The world it belongs to.</param>
/// <param name="Kind">How it came to be.</param>
/// <param name="Name">Its name.</param>
/// <param name="Tick">The world's tick when it was saved.</param>
/// <param name="SavedUtc">When it was written, in UTC.</param>
/// <param name="Version">Its save version.</param>
/// <param name="Bytes">Its size compressed.</param>
public sealed record SaveRecord(long Id, long WorldId, SaveKind Kind, string Name, long Tick, DateTime SavedUtc, int Version, long Bytes);
