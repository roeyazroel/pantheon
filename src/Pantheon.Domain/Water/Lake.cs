using System.Collections.Generic;

namespace Pantheon.Domain.Water;

/// <summary>A lake of the world (spec §5, ADR-022): still fresh water filling a hollow in the land.</summary>
public sealed class Lake
{
    /// <summary>Unique lake id within the world.</summary>
    public required int Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>The regions it covers, in id order.</summary>
    public required List<int> Regions { get; init; }

    /// <summary>Height of its surface in metres above the sea.</summary>
    public required float SurfaceHeight { get; init; }
}
