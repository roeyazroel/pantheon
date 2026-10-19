using System.Collections.Generic;

namespace Pantheon.Domain.World.Generation;

/// <summary>Parameters for the settlements founded when a world is generated.</summary>
public sealed record StartingSettlementSettings
{
    /// <summary>Settlements to found. Fewer are founded when the island has too few good sites far enough apart.</summary>
    public required int Count { get; init; }

    /// <summary>Least distance between two settlements, in regions (Chebyshev distance).</summary>
    public required int MinimumSpacing { get; init; }

    /// <summary>Initial population of a settlement before the variation.</summary>
    public required int Population { get; init; }

    /// <summary>Largest share by which a settlement's initial population differs from <see cref="Population"/> (0..1).</summary>
    public required float PopulationVariation { get; init; }

    /// <summary>Initial food store.</summary>
    public required float Food { get; init; }

    /// <summary>Initial water in the wells and cisterns (ADR-022); worlds generated before version 14 had none to store.</summary>
    public float Water { get; init; }

    /// <summary>How strongly site selection prefers land beside a river or lake (ADR-022).</summary>
    public float WaterPreference { get; init; }

    /// <summary>Initial housing capacity in people.</summary>
    public required int Housing { get; init; }

    /// <summary>How strongly site selection prefers regions near the island centre over raw fertility.</summary>
    public required float CenterPreference { get; init; }

    /// <summary>Pool of original settlement names, at least <see cref="Count"/>. The world seed picks them without repeats.</summary>
    public required IReadOnlyList<string> Names { get; init; }

    /// <summary>Initial wood store (ADR-033).</summary>
    public float Wood { get; init; }

    /// <summary>Initial stone store (ADR-033).</summary>
    public float Stone { get; init; }
}
