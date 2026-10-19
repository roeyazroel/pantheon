using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
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

    /// <summary>People on the move between settlements (spec §31, §82, ADR-016).</summary>
    public required MigrationState Migration { get; init; }

    /// <summary>
    /// The gods that have won (spec §53, §115): one god, or an alliance that won together; empty
    /// until someone wins. The world goes on after a victory.
    /// </summary>
    public required List<int> WinnerGodIds { get; init; }

    /// <summary>The moments the world remembers, oldest first (spec §50, §92, ADR-020).</summary>
    public required List<History.HistoryRecord> History { get; init; }

    /// <summary>The world's sacred places, ordered by id (spec §51, ADR-021).</summary>
    public required List<HolySites.HolySite> HolySites { get; init; }

    /// <summary>The world's rivers and lakes (spec §5, ADR-022).</summary>
    public required Water.WaterState Water { get; init; }

    /// <summary>How the world may be won (spec §53, ADR-027); Divine Ascendancy only by default.</summary>
    public VictorySettings Victory { get; set; } = VictorySettings.Default;

    /// <summary>The world's seasons (spec §17, ADR-028), or <c>null</c> for a world made without them.</summary>
    public Weather.SeasonState? Seasons { get; set; }
}
