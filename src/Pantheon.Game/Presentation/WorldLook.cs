using System;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Presentation;

/// <summary>What the land looks like now (ADR-078), shared by all its shaders.</summary>
/// <param name="Spring">How far spring shows, 0..1.</param>
/// <param name="Autumn">How far autumn shows, 0..1.</param>
/// <param name="Winter">How far winter shows, 0..1.</param>
/// <param name="Overcast">How dark the sky is over the place looked at, 0..1.</param>
public readonly record struct WorldLookState(float Spring, float Autumn, float Winter, float Overcast);

/// <summary>
/// How the year and the weather change the look of the land (spec §17, §61, ADR-078): each season
/// shows in full, then fades into the next over the last part of it, so the land turns slowly. A world
/// without seasons stays as it is. Pure: drawing never changes the simulation.
/// </summary>
public static class WorldLook
{
    // The share at the end of a season over which it fades into the next.
    private const float Fade = 0.3f;

    /// <summary>The look at a place now.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="focus">The ground point the camera looks at.</param>
    /// <returns>The look.</returns>
    public static WorldLookState Of(WeatherRules rules, WorldState world, GroundPosition focus)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var overcast = CloudLayout.Gloom(rules, world.Weather, focus);
        if (ClimateModel.SeasonOf(rules, world) is not { } season)
        {
            return new WorldLookState(0f, 0f, 0f, overcast);
        }

        var length = ClimateModel.SeasonTicks(rules);
        var gone = 1f - ((float)ClimateModel.TicksLeft(rules, world) / length);
        var next = (Season)(((int)season + 1) % 4);
        var blend = SmoothStep(1f - Fade, 1f, gone);
        float Weight(Season s) => (s == season ? 1f - blend : 0f) + (s == next ? blend : 0f);
        return new WorldLookState(Weight(Season.Spring), Weight(Season.Autumn), Weight(Season.Winter), overcast);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
