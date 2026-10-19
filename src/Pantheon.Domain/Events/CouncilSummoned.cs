using Pantheon.Domain.Politics;

namespace Pantheon.Domain.Events;

/// <summary>The Divine Council was summoned to vote on a motion (spec §42, ADR-025).</summary>
public sealed record CouncilSummoned : WorldEvent
{
    /// <summary>The motion.</summary>
    public required CouncilMotion Motion { get; init; }

    /// <summary>Why it was summoned.</summary>
    public required CouncilTrigger Trigger { get; init; }
}
