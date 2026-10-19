using Pantheon.Domain.Politics;

namespace Pantheon.Domain.Events;

/// <summary>A god kept or broke a promise (spec §40–41, ADR-025).</summary>
public sealed record ObligationEnded : WorldEvent
{
    /// <summary>The god that promised.</summary>
    public required int DebtorGodId { get; init; }

    /// <summary>The god promised.</summary>
    public required int CreditorGodId { get; init; }

    /// <summary>What was promised.</summary>
    public required ObligationKind Kind { get; init; }

    /// <summary>Whether it was kept.</summary>
    public required bool Kept { get; init; }
}
