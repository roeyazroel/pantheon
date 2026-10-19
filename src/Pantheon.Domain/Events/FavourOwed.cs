namespace Pantheon.Domain.Events;

/// <summary>A god saved another god's followers in great need, and that god now owes it a favour (spec §41, ADR-025).</summary>
public sealed record FavourOwed : WorldEvent
{
    /// <summary>The god owed.</summary>
    public required int CreditorGodId { get; init; }

    /// <summary>The god that owes.</summary>
    public required int DebtorGodId { get; init; }

    /// <summary>The settlement saved.</summary>
    public required int? SettlementId { get; init; }
}
