using Pantheon.Persistence;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Presentation tuning for the weather (ADR-013), loaded from <c>game/Data/weather_view.json</c>: how
/// clouds, rain and lightning are drawn. Only visuals use it.
/// </summary>
public sealed record WeatherViewSettings
{
    /// <summary>File name of the weather view content file.</summary>
    public const string FileName = "weather_view.json";

    /// <summary>Height of the cloud base above sea level, in metres.</summary>
    public required float CloudHeight { get; init; }

    /// <summary>Height over which a cloud's puffs spread above its base, in metres.</summary>
    public required float CloudThickness { get; init; }

    /// <summary>Metres between puffs: a cell of radius r has about (r / spacing)² × 0.8 of them.</summary>
    public required float PuffSpacing { get; init; }

    /// <summary>Most puffs one cloud has.</summary>
    public required int MaxPuffs { get; init; }

    /// <summary>Mean width of a puff, in metres.</summary>
    public required float PuffSize { get; init; }

    /// <summary>Colour of a light, settled cloud.</summary>
    public required RgbColor ClearColor { get; init; }

    /// <summary>Colour of a storm cloud.</summary>
    public required RgbColor StormColor { get; init; }

    /// <summary>Opacity of a full cloud (0..1).</summary>
    public required float CloudOpacity { get; init; }

    /// <summary>Share of the sunlight the darkest storm over the camera's focus takes away (0..1).</summary>
    public required float StormGloom { get; init; }

    /// <summary>Distance from the camera within which clouds are invisible, so they never block the view up close.</summary>
    public required float FadeNearDistance { get; init; }

    /// <summary>Distance from the camera beyond which clouds are fully drawn.</summary>
    public required float FadeFarDistance { get; init; }

    /// <summary>Colour of the rain.</summary>
    public required RgbColor RainColor { get; init; }

    /// <summary>Raindrops of the largest, heaviest rain.</summary>
    public required int MaxRainDrops { get; init; }

    /// <summary>Radius in metres at which a cloud has <see cref="MaxRainDrops"/>; smaller clouds have fewer.</summary>
    public required float FullRainRadius { get; init; }

    /// <summary>Lightning strikes per second of the fiercest thunderstorm.</summary>
    public required float FlashesPerSecond { get; init; }

    /// <summary>Colour of lightning.</summary>
    public required RgbColor LightningColor { get; init; }

    /// <summary>Parses and validates the weather view content file.</summary>
    /// <param name="json">Text of <c>weather_view.json</c>.</param>
    /// <returns>The validated settings.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or holds out-of-range values.</exception>
    public static WeatherViewSettings Parse(string json)
    {
        var settings = ContentJson.Parse<WeatherViewSettings>(json, FileName);
        if (!(settings.CloudHeight > 0f) || !(settings.CloudThickness >= 0f) || !(settings.PuffSpacing > 0f) || settings.MaxPuffs < 1
            || !(settings.PuffSize > 0f) || !(settings.CloudOpacity is > 0f and <= 1f) || !(settings.StormGloom is >= 0f and <= 1f) || !(settings.FadeNearDistance >= 0f)
            || !(settings.FadeFarDistance > settings.FadeNearDistance) || settings.MaxRainDrops < 0 || !(settings.FullRainRadius > 0f)
            || !(settings.FlashesPerSecond >= 0f))
        {
            throw new ContentFormatException($"{FileName}: heights, sizes and counts must be positive, CloudOpacity must lie above 0 and at most 1, StormGloom in 0..1, and FadeFarDistance must exceed FadeNearDistance.");
        }

        return settings;
    }
}
