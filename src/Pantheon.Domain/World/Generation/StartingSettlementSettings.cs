using System.Collections.Generic;

namespace Pantheon.Domain.World.Generation;

/// <summary>Parameters for the settlement founded when a world is generated.</summary>
public sealed record StartingSettlementSettings
{
    /// <summary>Initial population.</summary>
    public required int Population { get; init; }

    /// <summary>Initial food store.</summary>
    public required float Food { get; init; }

    /// <summary>Initial housing capacity in people.</summary>
    public required int Housing { get; init; }

    /// <summary>How strongly site selection prefers regions near the island centre over raw fertility.</summary>
    public required float CenterPreference { get; init; }

    /// <summary>Pool of original settlement names. One is picked using the world seed.</summary>
    public required IReadOnlyList<string> Names { get; init; }
}
