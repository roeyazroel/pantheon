using System.Collections.Generic;

namespace Pantheon.Domain.Water;

/// <summary>The world's fresh water (spec §5, §18, ADR-022): its rivers and lakes.</summary>
public sealed class WaterState
{
    /// <summary>The rivers, ordered by id; tributaries are rivers of their own.</summary>
    public required List<River> Rivers { get; init; }

    /// <summary>The lakes, ordered by id.</summary>
    public required List<Lake> Lakes { get; init; }
}
