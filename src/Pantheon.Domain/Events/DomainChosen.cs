using Pantheon.Domain.Gods;

namespace Pantheon.Domain.Events;

/// <summary>A god took up a domain (spec §12, ADR-021).</summary>
public sealed record DomainChosen : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>The domain.</summary>
    public required GodDomain Domain { get; init; }
}
