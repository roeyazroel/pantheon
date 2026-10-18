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
}
