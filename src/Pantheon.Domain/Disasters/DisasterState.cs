using System.Collections.Generic;

namespace Pantheon.Domain.Disasters;

/// <summary>
/// Disasters under way (spec §19, ADR-014). Wildfires live in the regions they burn
/// (<c>RegionState.Fire</c> and <c>Scorch</c>); earthquakes are listed here while they shake.
/// </summary>
public sealed class DisasterState
{
    /// <summary>Earthquakes shaking now, ordered by id.</summary>
    public required List<Earthquake> Earthquakes { get; init; }

    /// <summary>Id the next earthquake will get.</summary>
    public required int NextEarthquakeId { get; set; }

    /// <summary>Tornadoes on the ground (ADR-029).</summary>
    public List<Tornado> Tornadoes { get; init; } = [];

    /// <summary>Locust swarms on the wing (ADR-029).</summary>
    public List<LocustSwarm> Swarms { get; init; } = [];

    /// <summary>Id the next tornado or swarm will get.</summary>
    public int NextHazardId { get; set; } = 1;

    /// <summary>The island's volcano, once it has been found; <c>null</c> before, or on an island without one.</summary>
    public Volcano? Volcano { get; set; }

    /// <summary>The marks disasters left on the land, oldest first (spec §64).</summary>
    public List<TerrainMark> Marks { get; init; } = [];

    /// <summary>The settlements told their fields are blighted, until they are told the blight has gone (ADR-029).</summary>
    public List<int> BlightedSettlementIds { get; init; } = [];
}
