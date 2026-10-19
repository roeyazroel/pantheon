namespace Pantheon.Domain.Events;

/// <summary>How a fight between Creatures ended (spec §28, ADR-017).</summary>
public enum FightOutcome
{
    /// <summary>The loser was beaten and banished.</summary>
    Banished,

    /// <summary>The loser was beaten and let go.</summary>
    Spared,

    /// <summary>Neither could win in the time a fight lasts.</summary>
    Drew,

    /// <summary>One broke off the fight and ran.</summary>
    BrokeOff,
}

/// <summary>A fight between two Creatures ended.</summary>
public sealed record CreatureFightEnded : WorldEvent
{
    /// <summary>The winner, or the one who stayed when the other broke off; the lower id after a draw.</summary>
    public required int CreatureId { get; init; }

    /// <summary>The loser, or the one who broke off; the higher id after a draw.</summary>
    public required int OtherCreatureId { get; init; }

    /// <summary>How it ended.</summary>
    public required FightOutcome Outcome { get; init; }
}
