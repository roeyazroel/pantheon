namespace Pantheon.Domain.Events;

/// <summary>A road between two villages is whole (ADR-033).</summary>
public sealed record RoadBuilt : WorldEvent
{
    /// <summary>The road.</summary>
    public required int RoadId { get; init; }

    /// <summary>The village that built it.</summary>
    public required int FromSettlementId { get; init; }

    /// <summary>The village it leads to.</summary>
    public required int ToSettlementId { get; init; }
}

/// <summary>A road was cut by a disaster, or mended and opened again (ADR-033).</summary>
public sealed record RoadChanged : WorldEvent
{
    /// <summary>The road.</summary>
    public required int RoadId { get; init; }

    /// <summary>One of its villages.</summary>
    public required int FromSettlementId { get; init; }

    /// <summary>The other.</summary>
    public required int ToSettlementId { get; init; }

    /// <summary>Whether it is open again; otherwise it was cut.</summary>
    public required bool Open { get; init; }
}
