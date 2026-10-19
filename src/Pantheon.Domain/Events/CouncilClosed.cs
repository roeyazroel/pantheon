using Pantheon.Domain.Politics;

namespace Pantheon.Domain.Events;

/// <summary>The Divine Council voted (spec §42, ADR-025).</summary>
public sealed record CouncilClosed : WorldEvent
{
    /// <summary>The motion.</summary>
    public required CouncilMotion Motion { get; init; }

    /// <summary>Whether it passed.</summary>
    public required bool Passed { get; init; }

    /// <summary>Votes for.</summary>
    public required int Yes { get; init; }

    /// <summary>Votes against.</summary>
    public required int No { get; init; }
}
