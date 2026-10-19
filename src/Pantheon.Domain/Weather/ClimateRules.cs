namespace Pantheon.Domain.Weather;

/// <summary>
/// What a season, or a heat or cold wave, does to the world (spec §17, ADR-028). Shifts add,
/// factors multiply and losses add when a season and a wave come together. Rates are per simulated
/// second.
/// </summary>
public sealed record ClimateEffects
{
    /// <summary>Added to every region's temperature (0..1 scale): warmer summers, colder winters.</summary>
    public float TemperatureShift { get; init; }

    /// <summary>Factor on the weather cells that blow in: rainier and drier times.</summary>
    public float RainFactor { get; init; } = 1f;

    /// <summary>Factor on the harvest.</summary>
    public float YieldFactor { get; init; } = 1f;

    /// <summary>Factor on how readily the land burns.</summary>
    public float FireFactor { get; init; } = 1f;

    /// <summary>Factor on how fast the soil dries.</summary>
    public float DryingFactor { get; init; } = 1f;

    /// <summary>Share of every settlement's stored food that spoils per second.</summary>
    public float SpoilagePerSecond { get; init; }

    /// <summary>Health every settlement loses per second to the cold or the heat, less what its housing and medicine spare.</summary>
    public float HealthLossPerSecond { get; init; }

    /// <summary>Factor on people's wish to leave their village: few set out in winter.</summary>
    public float MigrationFactor { get; init; } = 1f;

    /// <summary>Chance that a heat wave follows an ordinary spell in this season.</summary>
    public float HeatWaveChance { get; init; }

    /// <summary>Chance that a cold wave follows an ordinary spell in this season.</summary>
    public float ColdWaveChance { get; init; }

    /// <summary>Share of storms born over warm sea that grow into hurricanes in this season.</summary>
    public float HurricaneShare { get; init; }

    /// <summary>No effect at all.</summary>
    public static ClimateEffects None { get; } = new();
}

/// <summary>The year's seasons (spec §17, ADR-028), in <c>weather.json</c>.</summary>
public sealed record SeasonRules
{
    /// <summary>Length of each season in simulated seconds; four make a year.</summary>
    public float SeasonSeconds { get; init; } = 150f;

    /// <summary>The climate of a world made without seasons: no shifts, and the extremes' year-round chances.</summary>
    public ClimateEffects Steady { get; init; } = ClimateEffects.None;

    /// <summary>Spring.</summary>
    public ClimateEffects Spring { get; init; } = ClimateEffects.None;

    /// <summary>Summer.</summary>
    public ClimateEffects Summer { get; init; } = ClimateEffects.None;

    /// <summary>Autumn.</summary>
    public ClimateEffects Autumn { get; init; } = ClimateEffects.None;

    /// <summary>Winter.</summary>
    public ClimateEffects Winter { get; init; } = ClimateEffects.None;

    /// <summary>Seasons that change nothing.</summary>
    public static SeasonRules None { get; } = new();
}

/// <summary>Snow, fog, hurricanes and heat and cold waves (spec §16, ADR-028), in <c>weather.json</c>.</summary>
public sealed record ExtremeRules
{
    /// <summary>A cell colder than this snows instead of raining; 0 for no snow.</summary>
    public float SnowTemperature { get; init; }

    /// <summary>Snow lying on land warmer than this melts.</summary>
    public float MeltTemperature { get; init; } = 1f;

    /// <summary>Snow cover a second of full snowfall lays down.</summary>
    public float SnowPerSecond { get; init; }

    /// <summary>Snow cover that melts per second per unit of warmth above <see cref="MeltTemperature"/>.</summary>
    public float MeltPerSecond { get; init; }

    /// <summary>Soil water a unit of melted snow gives.</summary>
    public float MeltWater { get; init; } = 1f;

    /// <summary>A cell at least this humid, this calm and this cool is fog, mist and drizzle; above 1 for no fog.</summary>
    public float FogHumidity { get; init; } = 2f;

    /// <summary>Pressure at most of a foggy cell.</summary>
    public float FogPressure { get; init; }

    /// <summary>Temperature at most of a foggy cell.</summary>
    public float FogTemperature { get; init; }

    /// <summary>Pressure of a new hurricane, at least.</summary>
    public float HurricanePressure { get; init; } = 1f;

    /// <summary>Wind from which a cell is a hurricane; above 1 for none.</summary>
    public float HurricaneWind { get; init; } = 2f;

    /// <summary>Factor on how fast a hurricane fills over land: it carries its winds far inland.</summary>
    public float HurricaneLandFilling { get; init; } = 1f;

    /// <summary>Sea warmth (with the season's shift) a storm needs to grow into a hurricane.</summary>
    public float HurricaneSeaTemperature { get; init; } = 1f;

    /// <summary>Share of a settlement's houses a full hurricane ruins per second.</summary>
    public float HurricaneHouseLossPerSecond { get; init; }

    /// <summary>What a heat wave does.</summary>
    public ClimateEffects HeatWave { get; init; } = ClimateEffects.None;

    /// <summary>What a cold wave does.</summary>
    public ClimateEffects ColdWave { get; init; } = ClimateEffects.None;

    /// <summary>Shortest heat or cold wave, in simulated seconds.</summary>
    public float WaveMinSeconds { get; init; } = 60f;

    /// <summary>Longest heat or cold wave, in simulated seconds.</summary>
    public float WaveMaxSeconds { get; init; } = 120f;

    /// <summary>No snow, fog, hurricanes or waves.</summary>
    public static ExtremeRules None { get; } = new();
}
