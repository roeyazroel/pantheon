using Pantheon.Domain.Migration;

namespace Pantheon.Domain.Events;

/// <summary>A god changed what it does with migrants arriving where it is followed (ADR-016).</summary>
public sealed record RefugeePolicyChanged : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>Its new policy.</summary>
    public required RefugeePolicy Policy { get; init; }
}
