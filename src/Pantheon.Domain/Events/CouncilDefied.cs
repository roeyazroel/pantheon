using Pantheon.Domain.Politics;

namespace Pantheon.Domain.Events;

/// <summary>A god defied a decree of the Divine Council (spec §42, ADR-025).</summary>
public sealed record CouncilDefied : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>The decree's motion.</summary>
    public required CouncilMotion Motion { get; init; }
}
