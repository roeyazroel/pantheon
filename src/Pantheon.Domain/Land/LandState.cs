using System.Collections.Generic;

namespace Pantheon.Domain.Land;

/// <summary>How a Divine Act reshaped the land (spec §14, ADR-045).</summary>
public enum ReshapeKind
{
    /// <summary>An island raised from the sea.</summary>
    Island,

    /// <summary>A gorge split through a mountain.</summary>
    Gorge,

    /// <summary>A channel that turns a river to a new course, with a dam across the old.</summary>
    Channel,
}

/// <summary>
/// One lasting change to the land's shape (spec §14's Divine Acts, ADR-045). The terrain is made
/// again from the seed with every reshaping applied in order, before the rivers are found, so the
/// water, the biomes and the regions follow the new land.
/// </summary>
public sealed class TerrainReshape
{
    /// <summary>Unique id, from 1.</summary>
    public required int Id { get; init; }

    /// <summary>What kind.</summary>
    public required ReshapeKind Kind { get; init; }

    /// <summary>The god that did it.</summary>
    public required int GodId { get; init; }

    /// <summary>When.</summary>
    public required long Tick { get; init; }

    /// <summary>East-west: the island's centre, the gorge's or channel's start, in metres.</summary>
    public required float X { get; init; }

    /// <summary>North-south: the island's centre, the gorge's or channel's start, in metres.</summary>
    public required float Z { get; init; }

    /// <summary>East-west of the gorge's or channel's end; the island's centre for an island.</summary>
    public required float ToX { get; init; }

    /// <summary>North-south of the gorge's or channel's end; the island's centre for an island.</summary>
    public required float ToZ { get; init; }

    /// <summary>The island's radius, or the gorge's or channel's half width, in metres.</summary>
    public required float Radius { get; init; }

    /// <summary>The island's peak above the sea, or how deep the channel starts below the river, in metres.</summary>
    public required float Peak { get; init; }

    /// <summary>The floor's height at the start, in metres: the gorge's or the river's.</summary>
    public required float FromHeight { get; init; }

    /// <summary>The floor's height at the end, in metres.</summary>
    public required float ToHeight { get; init; }

    /// <summary>East-west of the dam across a river's old course; unused otherwise.</summary>
    public float DamX { get; init; }

    /// <summary>North-south of the dam across a river's old course; unused otherwise.</summary>
    public float DamZ { get; init; }

    /// <summary>The dam's crest, in metres; 0 for none.</summary>
    public float DamTop { get; init; }
}

/// <summary>The land's lasting changes (ADR-045).</summary>
public sealed class LandState
{
    /// <summary>Every reshaping, oldest first; the terrain applies them in this order.</summary>
    public List<TerrainReshape> Reshapes { get; init; } = [];

    /// <summary>Id the next reshaping will get.</summary>
    public int NextReshapeId { get; set; } = 1;
}
