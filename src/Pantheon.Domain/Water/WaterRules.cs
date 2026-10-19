namespace Pantheon.Domain.Water;

/// <summary>
/// Data-driven tuning of rivers, floods and irrigation (spec §15, §19, ADR-022), loaded from
/// <c>game/Data/water.json</c>. Rates are per simulated second; values without units are 0..1.
/// </summary>
public sealed record WaterRules
{
    /// <summary>Ticks between water steps.</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Level a river settles at with dry soil and no rain.</summary>
    public required float BaseLevel { get; init; }

    /// <summary>Mean soil water along a river above which the wet ground sheds into it.</summary>
    public required float SoilThreshold { get; init; }

    /// <summary>Level a river gains per unit of soil water above <see cref="SoilThreshold"/>.</summary>
    public required float SoilLevelGain { get; init; }

    /// <summary>Level a river gains per unit of rain falling along its course.</summary>
    public required float RainLevelGain { get; init; }

    /// <summary>Share of the gap to the level the weather sets that a river closes per second.</summary>
    public required float LevelResponsePerSecond { get; init; }

    /// <summary>Level above which a river overflows its banks.</summary>
    public required float FloodLevel { get; init; }

    /// <summary>Level at which a flood is at its worst.</summary>
    public required float MaxFloodLevel { get; init; }

    /// <summary>Land beside a flooding river floods too, by half, when it lies less than this many metres above the river's region.</summary>
    public required float FloodBankHeight { get; init; }

    /// <summary>Share of a flood that drains away per second once the water stops rising.</summary>
    public required float FloodRecedePerSecond { get; init; }

    /// <summary>Share of a flooded settlement's houses lost per second at full flood.</summary>
    public required float FloodHousingLossPerSecond { get; init; }

    /// <summary>Share of a flooded settlement's people drowned per second at full flood.</summary>
    public required float FloodDeathsPerSecond { get; init; }

    /// <summary>Share of a flooded settlement's food spoiled per second at full flood.</summary>
    public required float FloodFoodLossPerSecond { get; init; }

    /// <summary>Fear and doubt a flood stirs per second at full flood, as other disasters do.</summary>
    public required float FloodDreadPerSecond { get; init; }

    /// <summary>Blessing the silt of a full flood leaves on the land: it yields more once the water is gone.</summary>
    public required float SiltBlessing { get; init; }

    /// <summary>Soil water per second that rivers and lakes give the land they water, times <c>(1 - soil)</c>.</summary>
    public required float IrrigationPerSecond { get; init; }
}
