using Pantheon.Domain.Society;

namespace Pantheon.Domain.Events;

/// <summary>A settlement reached a new whole level in a field of technology (spec §36, ADR-023).</summary>
public sealed record TechnologyAdvanced : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The field.</summary>
    public required TechnologyField Field { get; init; }

    /// <summary>The level reached.</summary>
    public required int Level { get; init; }
}
