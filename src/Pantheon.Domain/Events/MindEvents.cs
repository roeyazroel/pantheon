namespace Pantheon.Domain.Events;

/// <summary>
/// A god spoke to another god, unasked (ADR-072): a warning, a threat, thanks, an offer or a taunt, written
/// by its agent or, without a model, by the scribes. The words are news only to the god spoken to.
/// </summary>
public sealed record GodSpoke : WorldEvent
{
    /// <summary>The god who spoke.</summary>
    public required int GodId { get; init; }

    /// <summary>The god spoken to.</summary>
    public required int ToGodId { get; init; }

    /// <summary>What it said.</summary>
    public required string Text { get; init; }
}

/// <summary>A Creature said something aloud (ADR-072): what it makes of a moment that mattered to it.</summary>
public sealed record CreatureSpoke : WorldEvent
{
    /// <summary>The Creature.</summary>
    public required int CreatureId { get; init; }

    /// <summary>What it said.</summary>
    public required string Text { get; init; }
}
