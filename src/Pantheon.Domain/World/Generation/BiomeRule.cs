using Pantheon.Domain.Ecology;

namespace Pantheon.Domain.World.Generation;

/// <summary>
/// One data-driven biome classification rule with the biome's base traits. Bounds are optional:
/// minimums are inclusive, maximums are exclusive, and an omitted bound is unbounded.
/// </summary>
public sealed record BiomeRule
{
    /// <summary>Biome assigned when the rule matches.</summary>
    public required Biome Biome { get; init; }

    /// <summary>Inclusive minimum elevation in metres.</summary>
    public float? MinElevation { get; init; }

    /// <summary>Exclusive maximum elevation in metres.</summary>
    public float? MaxElevation { get; init; }

    /// <summary>Inclusive minimum normalised temperature.</summary>
    public float? MinTemperature { get; init; }

    /// <summary>Exclusive maximum normalised temperature.</summary>
    public float? MaxTemperature { get; init; }

    /// <summary>Inclusive minimum normalised moisture.</summary>
    public float? MinMoisture { get; init; }

    /// <summary>Exclusive maximum normalised moisture.</summary>
    public float? MaxMoisture { get; init; }

    /// <summary>Fertility of ideal land of this biome (0..1).</summary>
    public required float BaseFertility { get; init; }

    /// <summary>Vegetation cover of ideal land of this biome (0..1).</summary>
    public required float BaseVegetation { get; init; }

    /// <summary>Whether the rule has no bounds at all and therefore matches every sample.</summary>
    /// <returns><c>true</c> for a catch-all rule.</returns>
    public bool IsCatchAll() =>
        MinElevation is null && MaxElevation is null &&
        MinTemperature is null && MaxTemperature is null &&
        MinMoisture is null && MaxMoisture is null;

    /// <summary>Tests whether a sample satisfies every bound of the rule.</summary>
    /// <param name="elevation">Height in metres.</param>
    /// <param name="temperature">Normalised temperature.</param>
    /// <param name="moisture">Normalised moisture.</param>
    /// <returns><c>true</c> when the rule matches.</returns>
    public bool Matches(float elevation, float temperature, float moisture) =>
        IsWithin(elevation, MinElevation, MaxElevation) &&
        IsWithin(temperature, MinTemperature, MaxTemperature) &&
        IsWithin(moisture, MinMoisture, MaxMoisture);

    /// <summary>Checks <paramref name="value"/> against an optional inclusive minimum and exclusive maximum.</summary>
    private static bool IsWithin(float value, float? min, float? max) =>
        (min is null || value >= min.Value) && (max is null || value < max.Value);
}
