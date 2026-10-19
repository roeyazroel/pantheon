namespace Pantheon.Domain.History;

/// <summary>What kind of moment the world remembers (spec §50, §92, ADR-020).</summary>
public enum HistoryKind
{
    /// <summary>Two gods allied.</summary>
    Alliance,

    /// <summary>A god declared war on another.</summary>
    War,

    /// <summary>Two gods made peace.</summary>
    Peace,

    /// <summary>A god betrayed its ally.</summary>
    Betrayal,

    /// <summary>A god, or allied gods, began to hold the victory share of the world's belief.</summary>
    Dominion,

    /// <summary>A god, or allied gods, won.</summary>
    Victory,

    /// <summary>A god reached a rank; <see cref="HistoryRecord.Count"/> is the rank's number.</summary>
    Rank,

    /// <summary>A settlement turned to a god.</summary>
    Conversion,

    /// <summary>An earthquake struck a settlement; <see cref="HistoryRecord.Count"/> is the dead.</summary>
    Earthquake,

    /// <summary>A fire burned a settlement; <see cref="HistoryRecord.Count"/> is the houses lost.</summary>
    Fire,

    /// <summary>A settlement began to starve.</summary>
    Famine,

    /// <summary>Many people left a settlement at once; <see cref="HistoryRecord.Count"/> is how many.</summary>
    Exodus,

    /// <summary>A settlement lost its last people.</summary>
    Fall,

    /// <summary>A fallen settlement was settled again.</summary>
    Resettled,

    /// <summary>A Creature beat another, which was banished.</summary>
    Battle,

    /// <summary>A Creature reached a great level; <see cref="HistoryRecord.Count"/> is the level.</summary>
    Legend,

    /// <summary>A god took up a domain; <see cref="HistoryRecord.Count"/> is the domain's number (ADR-021).</summary>
    Domain,

    /// <summary>A god took a holy site; <see cref="HistoryRecord.Count"/> is the site's id (ADR-021).</summary>
    Claimed,

    /// <summary>Followers of a god broke away to keep its Old Faith; <see cref="HistoryRecord.Count"/> is how many (ADR-021).</summary>
    Schism,

    /// <summary>The Old Faith of a settlement returned to its god (ADR-021).</summary>
    Reunion,
}

/// <summary>
/// One moment the world remembers (spec §50, §92: significant events only). It names who and
/// where by id; the words are the presentation's.
/// </summary>
public sealed class HistoryRecord
{
    /// <summary>Tick at which it happened.</summary>
    public required long Tick { get; init; }

    /// <summary>What happened.</summary>
    public required HistoryKind Kind { get; init; }

    /// <summary>The god who acted or is concerned, if any.</summary>
    public required int? GodId { get; init; }

    /// <summary>The other god, for acts between gods.</summary>
    public required int? OtherGodId { get; init; }

    /// <summary>The settlement concerned, if any.</summary>
    public required int? SettlementId { get; init; }

    /// <summary>The Creature concerned (the winner of a battle), if any.</summary>
    public required int? CreatureId { get; init; }

    /// <summary>The other Creature (the loser of a battle), if any.</summary>
    public required int? OtherCreatureId { get; init; }

    /// <summary>A number the kind gives meaning to: the dead, the houses, the people, the rank, the level, the domain or the holy site; 0 otherwise.</summary>
    public required int Count { get; init; }
}
