using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World.Generation;

namespace Pantheon.Domain.World;

/// <summary>
/// Root of the simulation state and the single source of truth for the game world (ADR-006).
/// Presentation reads it and never stores authoritative data of its own. Saves serialize exactly
/// this object graph.
/// </summary>
public sealed class WorldState
{
    /// <summary>Seed the world was generated from.</summary>
    public required WorldSeed Seed { get; init; }

    /// <summary>
    /// Copy of the generation settings used for this world. The derived terrain heightfield is
    /// regenerated from <see cref="Seed"/> plus these settings, so it never needs to be saved.
    /// </summary>
    public required WorldGenSettings Generation { get; init; }

    /// <summary>Number of simulation ticks executed since the world was created.</summary>
    public required long Tick { get; set; }

    /// <summary>State of the core simulation random stream.</summary>
    public required RandomState Random { get; set; }

    /// <summary>Region grid covering the world.</summary>
    public required RegionGrid Regions { get; init; }

    /// <summary>All settlements in the world.</summary>
    public required List<SettlementState> Settlements { get; init; }

    /// <summary>All gods in the world, ordered by id.</summary>
    public required List<GodState> Gods { get; init; }

    /// <summary>All Creatures in the world, ordered by id: at most one per god (spec §22).</summary>
    public required List<CreatureState> Creatures { get; init; }

    /// <summary>The relations between the gods (spec §37–41).</summary>
    public required DiplomacyState Diplomacy { get; init; }

    /// <summary>The weather (spec §16, ADR-013).</summary>
    public required WeatherState Weather { get; init; }

    /// <summary>Disasters under way (spec §19, ADR-014).</summary>
    public required DisasterState Disasters { get; init; }

    /// <summary>
    /// The gods that have won (spec §53, §115): one god, or an alliance that won together; empty
    /// until someone wins. The world goes on after a victory.
    /// </summary>
    public required List<int> WinnerGodIds { get; init; }
}
