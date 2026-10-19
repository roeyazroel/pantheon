using Pantheon.Domain.Culture;

namespace Pantheon.Domain.Events;

/// <summary>The world came to see a god a new way, from what it did (spec §11, ADR-024).</summary>
public sealed record TraitEarned : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>The trait.</summary>
    public required GodTrait Trait { get; init; }
}
