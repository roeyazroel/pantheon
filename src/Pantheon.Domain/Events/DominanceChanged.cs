using System.Collections.Generic;

namespace Pantheon.Domain.Events;

/// <summary>A god began or ceased to hold the victory share of the world's belief.</summary>
public sealed record DominanceChanged : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary><c>true</c> when it began to dominate, <c>false</c> when it lost its dominance.</summary>
    public required bool Started { get; init; }

    /// <summary>The allies it dominates together with (ADR-015); empty when it dominates alone.</summary>
    public required IReadOnlyList<int> Allies { get; init; }
}
