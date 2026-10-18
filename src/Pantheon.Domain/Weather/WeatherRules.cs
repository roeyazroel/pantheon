namespace Pantheon.Domain.Weather;

/// <summary>
/// Data-driven tuning of weather and soil water, loaded from <c>game/Data/weather.json</c>
/// (ADR-013). Rates are per simulated second; values without units are normalised to 0..1.
/// </summary>
public sealed record WeatherRules
{
    /// <summary>Ticks between weather steps (spec §16: one or two updates per second).</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Most cells over the world at once.</summary>
    public required int MaxCells { get; init; }

    /// <summary>Cells scattered over the map when the weather starts, so a new world begins mid-pattern.</summary>
    public required int InitialCells { get; init; }

    /// <summary>New cells per simulated second in a normal spell, before the spell's factor.</summary>
    public required float CellsPerSecond { get; init; }

    /// <summary>Factor on new cells in a dry spell.</summary>
    public required float DrySpellCellFactor { get; init; }

    /// <summary>Factor on new cells in a wet spell.</summary>
    public required float WetSpellCellFactor { get; init; }

    /// <summary>Share of new cells that are storms in a normal or dry spell.</summary>
    public required float StormShare { get; init; }

    /// <summary>Share of new cells that are storms in a wet spell.</summary>
    public required float WetSpellStormShare { get; init; }

    /// <summary>Shortest normal spell, in simulated seconds.</summary>
    public required float NormalSpellMinSeconds { get; init; }

    /// <summary>Longest normal spell, in simulated seconds.</summary>
    public required float NormalSpellMaxSeconds { get; init; }

    /// <summary>Shortest dry spell, in simulated seconds.</summary>
    public required float DrySpellMinSeconds { get; init; }

    /// <summary>Longest dry spell, in simulated seconds.</summary>
    public required float DrySpellMaxSeconds { get; init; }

    /// <summary>Shortest wet spell, in simulated seconds.</summary>
    public required float WetSpellMinSeconds { get; init; }

    /// <summary>Longest wet spell, in simulated seconds.</summary>
    public required float WetSpellMaxSeconds { get; init; }

    /// <summary>Chance that a normal spell is followed by a dry one; otherwise a wet one or, by <see cref="NormalAfterNormalChance"/>, another normal one.</summary>
    public required float DryAfterNormalChance { get; init; }

    /// <summary>Chance that a normal spell is followed by another normal one.</summary>
    public required float NormalAfterNormalChance { get; init; }

    /// <summary>Largest turn of the prevailing wind when a spell changes, in degrees either way.</summary>
    public required float WindTurnDegrees { get; init; }

    /// <summary>Mean speed of a cell in metres per simulated second.</summary>
    public required float CellSpeed { get; init; }

    /// <summary>Speed of a summoned rain cell as a share of <see cref="CellSpeed"/>; it lingers where it was called.</summary>
    public required float SummonedSpeedFactor { get; init; }

    /// <summary>Smallest radius of a new cell in metres.</summary>
    public required float MinRadius { get; init; }

    /// <summary>Largest radius of a new cell in metres.</summary>
    public required float MaxRadius { get; init; }

    /// <summary>Smallest humidity of a new cell.</summary>
    public required float MinHumidity { get; init; }

    /// <summary>Pressure (depth of the low) of an ordinary new cell, at most.</summary>
    public required float ShowerPressure { get; init; }

    /// <summary>Pressure of a new storm, at least.</summary>
    public required float StormPressure { get; init; }

    /// <summary>Pressure of a summoned rain cell.</summary>
    public required float SummonedPressure { get; init; }

    /// <summary>Pressure lost per second over land: lows fill once they leave the sea.</summary>
    public required float LandFillingPerSecond { get; init; }

    /// <summary>Share of the missing humidity the sea gives a cell per second.</summary>
    public required float SeaHumidityPerSecond { get; init; }

    /// <summary>Humidity a cell loses per second over land besides its rain: clouds that have rained out thin away.</summary>
    public required float LandDissipationPerSecond { get; init; }

    /// <summary>Share of the gap to the ground's temperature a cell closes per second.</summary>
    public required float TemperatureFollowPerSecond { get; init; }

    /// <summary>Humidity below which a cell does not rain.</summary>
    public required float RainHumidity { get; init; }

    /// <summary>Rain of a full cell with no low; <see cref="RainFromPressure"/> adds more for deeper lows.</summary>
    public required float RainBase { get; init; }

    /// <summary>Extra rain of a full cell per unit of pressure.</summary>
    public required float RainFromPressure { get; init; }

    /// <summary>Humidity a cell loses per second per unit of rain.</summary>
    public required float RainDrainPerSecond { get; init; }

    /// <summary>Wind per unit of pressure.</summary>
    public required float WindFromPressure { get; init; }

    /// <summary>Lightning per unit of pressure times rain times warmth.</summary>
    public required float LightningFactor { get; init; }

    /// <summary>Wind from which a cell is a windstorm and does damage.</summary>
    public required float StormWind { get; init; }

    /// <summary>Lightning from which a cell is a thunderstorm.</summary>
    public required float ThunderActivity { get; init; }

    /// <summary>Rain from which a cell counts as raining.</summary>
    public required float VisibleRain { get; init; }

    /// <summary>Share of a cell's radius at its edge over which its weather fades out.</summary>
    public required float EdgeShare { get; init; }

    /// <summary>Soil moisture a second of full rain adds, times the soil's room for water.</summary>
    public required float RainSoakPerSecond { get; init; }

    /// <summary>Share of the soil's water lost per second at a temperature of 0.5; warmer land dries faster.</summary>
    public required float EvaporationPerSecond { get; init; }

    /// <summary>Factor on evaporation in a dry spell: the air is hot and dry.</summary>
    public required float DrySpellDryingFactor { get; init; }

    /// <summary>Share of the soil's room for water that springs and streams refill per second, times the region's moisture.</summary>
    public required float GroundwaterPerSecond { get; init; }

    /// <summary>Share of a settlement's stored food a full storm ruins per second.</summary>
    public required float StormFoodLossPerSecond { get; init; }

    /// <summary>Share of the work on a settlement's house under construction a full storm undoes per second.</summary>
    public required float StormConstructionLossPerSecond { get; init; }
}
