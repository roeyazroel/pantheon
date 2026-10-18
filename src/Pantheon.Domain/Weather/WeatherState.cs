using System.Collections.Generic;

namespace Pantheon.Domain.Weather;

/// <summary>
/// The world's weather (spec §16, ADR-013): the moving cells, the spell the island is in and the
/// prevailing wind. The soil's water is kept per region (<c>RegionState.SoilMoisture</c>).
/// </summary>
public sealed class WeatherState
{
    /// <summary>The weather cells, ordered by id.</summary>
    public required List<WeatherCell> Cells { get; init; }

    /// <summary>Id the next cell will get.</summary>
    public required int NextCellId { get; set; }

    /// <summary>The spell the island is in.</summary>
    public required WeatherSpell Spell { get; set; }

    /// <summary>Tick at which the spell ends, or <c>null</c> until the weather step first schedules it.</summary>
    public required long? SpellEndsTick { get; set; }

    /// <summary>Direction the prevailing wind blows towards, in radians on the ground plane (0 = towards +X).</summary>
    public required float WindDirection { get; set; }
}
