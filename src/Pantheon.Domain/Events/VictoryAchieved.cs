using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Events;

/// <summary>A god, or an alliance together, held the victory share of the world's belief long enough to win (spec §53, §115).</summary>
public sealed record VictoryAchieved : WorldEvent
{
    /// <summary>The winning gods: one, or the members of the alliance that won together (ADR-015).</summary>
    public required IReadOnlyList<int> GodIds { get; init; }

    /// <summary>How it was won (ADR-027).</summary>
    public VictoryKind Kind { get; init; }
}
