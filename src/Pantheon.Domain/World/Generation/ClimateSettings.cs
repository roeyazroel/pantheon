namespace Pantheon.Domain.World.Generation;

/// <summary>How temperature and moisture are derived from noise and elevation.</summary>
public sealed record ClimateSettings
{
    /// <summary>Normalised temperature at sea level before noise is applied.</summary>
    public required float BaseTemperature { get; init; }

    /// <summary>Maximum deviation that the temperature noise adds or subtracts.</summary>
    public required float TemperatureVariation { get; init; }

    /// <summary>Temperature lost between sea level and <see cref="TerrainShapeSettings.MaxElevation"/>.</summary>
    public required float LapseRate { get; init; }

    /// <summary>Moisture added to land right at the coast.</summary>
    public required float CoastalMoistureBonus { get; init; }

    /// <summary>Height above sea level, in metres, over which the coastal moisture bonus fades out.</summary>
    public required float CoastalMoistureRange { get; init; }
}
