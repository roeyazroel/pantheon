namespace Pantheon.Domain.Events;

/// <summary>A god honoured a defensive pact or mutual defence and went to war against an aggressor (spec §38–39, ADR-025).</summary>
public sealed record TreatyInvoked : WorldEvent
{
    /// <summary>The treaty.</summary>
    public required int TreatyId { get; init; }

    /// <summary>The god that came to its partner's defence.</summary>
    public required int DefenderGodId { get; init; }

    /// <summary>The partner attacked.</summary>
    public required int ProtectedGodId { get; init; }

    /// <summary>The god that declared war on the partner.</summary>
    public required int AggressorGodId { get; init; }

    /// <summary>Whether the defender broke an alliance with the aggressor to honour it.</summary>
    public required bool Betrayal { get; init; }
}
