using System.Collections.Generic;

namespace Pantheon.Domain.World;

/// <summary>The sizes a new world may be made at (spec §6, ADR-027), in <c>world_sizes.json</c>.</summary>
public sealed record WorldSizes
{
    /// <summary>The sizes, smallest first; the first is the world <c>worldgen.json</c> describes.</summary>
    public required IReadOnlyList<WorldSizePreset> Sizes { get; init; }

    /// <summary>Settlement names added to <c>worldgen.json</c>'s for worlds with more settlements.</summary>
    public required IReadOnlyList<string> ExtraSettlementNames { get; init; }

    /// <summary>River names added to <c>worldgen.json</c>'s for worlds with more rivers.</summary>
    public required IReadOnlyList<string> ExtraRiverNames { get; init; }

    /// <summary>Only the world <c>worldgen.json</c> describes.</summary>
    public static WorldSizes Default { get; } = new() { Sizes = [], ExtraSettlementNames = [], ExtraRiverNames = [] };
}

/// <summary>One world size: how large the island is and how many settlements, gods and rivers it holds.</summary>
public sealed record WorldSizePreset
{
    /// <summary>Its name, for example "Large".</summary>
    public required string Name { get; init; }

    /// <summary>Heightmap samples per side.</summary>
    public required int HeightmapResolution { get; init; }

    /// <summary>Metres between samples.</summary>
    public required float VertexSpacing { get; init; }

    /// <summary>Regions per side.</summary>
    public required int RegionsPerSide { get; init; }

    /// <summary>Settlements founded at the start.</summary>
    public required int Settlements { get; init; }

    /// <summary>Gods: <c>gods.json</c>'s gods, then its extra gods.</summary>
    public required int Gods { get; init; }

    /// <summary>The most rivers, and of them the most that run to the sea.</summary>
    public required int Rivers { get; init; }

    /// <summary>The most rivers that run to the sea.</summary>
    public required int MainRivers { get; init; }

    /// <summary>The hydrology's cell size in metres.</summary>
    public required float HydrologyCellSize { get; init; }

    /// <summary>The most gods that may win a pantheon victory together: more in a larger pantheon (ADR-027).</summary>
    public required int PantheonMaxGods { get; init; }
}
