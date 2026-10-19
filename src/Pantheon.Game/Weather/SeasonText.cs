using System;
using System.Globalization;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Weather;

/// <summary>Words for the seasons (spec §17, ADR-028), shared by the HUD, alerts and debug readouts.</summary>
public static class SeasonText
{
    /// <summary>A season's name.</summary>
    /// <param name="season">The season.</param>
    /// <returns>For example "Winter".</returns>
    public static string Name(Season season) => season switch
    {
        Season.Spring => "Spring",
        Season.Summer => "Summer",
        Season.Autumn => "Autumn",
        Season.Winter => "Winter",
    };

    /// <summary>What a season means for the land and its people.</summary>
    /// <param name="season">The season.</param>
    /// <returns>A short clause.</returns>
    public static string Effect(Season season) => season switch
    {
        Season.Spring => "the snow melts and the fields are sown",
        Season.Summer => "crops grow, the land dries and fires catch easily",
        Season.Autumn => "the harvest comes in, rain is frequent and hurricanes come from the sea",
        Season.Winter => "the fields lie fallow, snow lies on the hills and stores must last",
    };

    /// <summary>The season, its year and how long it has left, or <c>null</c> in a world without seasons.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="world">World state.</param>
    /// <returns>For example "Winter of year 3 (1:20 left)".</returns>
    public static string? Describe(WeatherRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (ClimateModel.SeasonOf(rules, world) is not { } season)
        {
            return null;
        }

        var left = Divine.DivineText.Clock(ClimateModel.TicksLeft(rules, world) / (double)SimulationTime.TicksPerSecond);
        return string.Format(CultureInfo.InvariantCulture, "{0} of year {1} ({2} left)", Name(season), ClimateModel.YearOf(rules, world), left);
    }
}
