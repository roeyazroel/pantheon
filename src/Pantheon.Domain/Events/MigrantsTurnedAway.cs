namespace Pantheon.Domain.Events;

/// <summary>A god turned a group of migrants away from a settlement it holds (spec §31, ADR-016).</summary>
public sealed record MigrantsTurnedAway : WorldEvent
{
    /// <summary>The group.</summary>
    public required int GroupId { get; init; }

    /// <summary>The settlement that turned them away.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The god that turned them away.</summary>
    public required int ByGodId { get; init; }

    /// <summary>How many they are.</summary>
    public required int People { get; init; }

    /// <summary>The god most of them follow, if any.</summary>
    public required int? GodId { get; init; }

    /// <summary>Where they head now (their origin when they go home).</summary>
    public required int NextSettlementId { get; init; }
}
