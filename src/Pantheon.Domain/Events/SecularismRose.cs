using Pantheon.Domain.Culture;

namespace Pantheon.Domain.Events;

/// <summary>A settlement turned further from the gods, or back towards them (spec §35, ADR-024).</summary>
public sealed record SecularismRose : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>Its stage now.</summary>
    public required SecularStage Stage { get; init; }

    /// <summary>Its stage before.</summary>
    public required SecularStage Previous { get; init; }
}
