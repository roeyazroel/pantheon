using System;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;

namespace Pantheon.Game.Presentation;

/// <summary>
/// The wind felt at a place, for the trees and grass to bend in (ADR-053): the strongest wind of
/// the weather cells over it, fading towards each cell's edge, blowing the weather's way. Pure.
/// </summary>
public static class Breeze
{
    /// <summary>The wind at a place.</summary>
    /// <param name="weather">The weather.</param>
    /// <param name="at">The place.</param>
    /// <returns>Its strength (0 calm, 1 a storm, more in a hurricane) and the way it blows on the ground.</returns>
    public static (float Strength, float X, float Z) At(WeatherState weather, GroundPosition at)
    {
        ArgumentNullException.ThrowIfNull(weather);
        var strength = 0f;
        foreach (var cell in weather.Cells)
        {
            var distance = MathF.Sqrt(((cell.X - at.X) * (cell.X - at.X)) + ((cell.Z - at.Z) * (cell.Z - at.Z)));
            strength = MathF.Max(strength, cell.Wind * Math.Clamp(1f - (distance / Math.Max(1f, cell.Radius)), 0f, 1f));
        }

        return (strength, MathF.Cos(weather.WindDirection), MathF.Sin(weather.WindDirection));
    }
}
