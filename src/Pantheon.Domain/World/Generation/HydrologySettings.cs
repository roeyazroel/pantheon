using System.Collections.Generic;

namespace Pantheon.Domain.World.Generation;

/// <summary>
/// How rivers and lakes are found and cut into the land when a world is generated (spec §5, §93,
/// ADR-022). Water flows down a coarse grid over the heightfield: depressions fill into lakes, and
/// where enough rain gathers a river runs to the sea. Distances are in metres.
/// </summary>
public sealed record HydrologySettings
{
    /// <summary>Side of one cell of the coarse flow grid; a whole number of height samples.</summary>
    public required float CellSize { get; init; }

    /// <summary>Area of fully wet land, in square metres, that must drain through a cell for a river to run there.</summary>
    public required float RiverCatchment { get; init; }

    /// <summary>Area of fully wet land, in square metres, draining through a cell for a river to reach up to it: a river found by <see cref="RiverCatchment"/> runs up to its headwaters.</summary>
    public required float HeadwaterCatchment { get; init; }

    /// <summary>Most rivers a world has, tributaries included.</summary>
    public required int MaxRivers { get; init; }

    /// <summary>Most rivers that reach the sea; the longest are kept, and tributaries join them up to <see cref="MaxRivers"/>.</summary>
    public required int MaxMainRivers { get; init; }

    /// <summary>Shortest river kept, in metres.</summary>
    public required float MinRiverLength { get; init; }

    /// <summary>Width of a river at its source.</summary>
    public required float MinChannelWidth { get; init; }

    /// <summary>Width of the largest river at its mouth.</summary>
    public required float MaxChannelWidth { get; init; }

    /// <summary>Depth below the water of a river at its source.</summary>
    public required float MinChannelDepth { get; init; }

    /// <summary>Depth below the water of the largest river at its mouth.</summary>
    public required float MaxChannelDepth { get; init; }

    /// <summary>Width of the sloping banks beside a river's water.</summary>
    public required float BankWidth { get; init; }

    /// <summary>How far a river's water lies below the land it runs through.</summary>
    public required float Freeboard { get; init; }

    /// <summary>Times the river's course is smoothed so it bends rather than steps.</summary>
    public required int SmoothingPasses { get; init; }

    /// <summary>Depth a depression must reach, below the level it spills at, to hold a lake.</summary>
    public required float MinLakeDepth { get; init; }

    /// <summary>Smallest lake kept, in square metres.</summary>
    public required float MinLakeArea { get; init; }

    /// <summary>Share of a region's ground under fresh water at which it counts as fully watered (<see cref="RegionState.FreshWater"/> 1).</summary>
    public required float FullWaterShare { get; init; }

    /// <summary>Names given to rivers, drawn in a seeded order.</summary>
    public required IReadOnlyList<string> RiverNames { get; init; }

    /// <summary>Names given to lakes, drawn in a seeded order.</summary>
    public required IReadOnlyList<string> LakeNames { get; init; }
}
