using System;
using System.Globalization;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Weather;

/// <summary>
/// Debug drivers for the weather (ADR-013): change the spell and call a storm, so droughts and storm
/// damage can be seen without waiting for them. Each returns a status line for the debug overlay.
/// </summary>
public static class WeatherCommands
{
    /// <summary>Radius of a storm called by <see cref="SummonStorm"/>, in metres.</summary>
    public const float StormRadius = 320f;

    /// <summary>The spell after <paramref name="spell"/> in the debug cycle: normal, dry, wet, heat wave, cold wave, normal.</summary>
    /// <param name="spell">The current spell.</param>
    /// <returns>The next one.</returns>
    public static WeatherSpell Next(WeatherSpell spell) => spell switch
    {
        WeatherSpell.Normal => WeatherSpell.Dry,
        WeatherSpell.Dry => WeatherSpell.Wet,
        WeatherSpell.Wet => WeatherSpell.HeatWave,
        WeatherSpell.HeatWave => WeatherSpell.ColdWave,
        WeatherSpell.ColdWave => WeatherSpell.Normal,
    };

    /// <summary>Starts the next spell of the debug cycle, for its longest length.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <returns>Status line.</returns>
    public static string NextSpell(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        return SetSpell(session, Next(session.World.Weather.Spell));
    }

    /// <summary>Starts a spell now, for its longest length.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="spell">The spell.</param>
    /// <returns>Status line.</returns>
    public static string SetSpell(GameSession session, WeatherSpell spell)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        var (_, longest) = WeatherModel.SpellSeconds(session.Content.Weather, spell);
        world.Weather.Spell = spell;
        world.Weather.SpellEndsTick = world.Tick + (long)MathF.Round(longest * SimulationTime.TicksPerSecond);
        return WeatherText.Describe(world.Weather, world.Tick);
    }

    /// <summary>Calls a storm over a point: a deep, wet low moving with the prevailing wind, short of a hurricane unless asked for one (ADR-028).</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="target">Where the storm starts.</param>
    /// <param name="hurricane">Whether it is a hurricane.</param>
    /// <returns>Status line.</returns>
    public static string SummonStorm(GameSession session, GroundPosition target, bool hurricane = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var rules = session.Content.Weather;
        var weather = session.World.Weather;
        var (windX, windZ) = WeatherModel.WindVector(weather);
        var cell = new WeatherCell
        {
            Id = weather.NextCellId++,
            X = target.X,
            Z = target.Z,
            VelocityX = windX * rules.CellSpeed,
            VelocityZ = windZ * rules.CellSpeed,
            Radius = StormRadius,
            Pressure = hurricane || rules.Extremes.HurricaneWind >= 1f ? 1f : Math.Min(1f, (rules.Extremes.HurricaneWind / Math.Max(rules.WindFromPressure, 1e-3f)) - 0.01f),
            Temperature = WeatherModel.GroundTemperature(session.World.Regions, target.X, target.Z, 0.6f),
            Humidity = 1f,
            Precipitation = 0f,
            Wind = 0f,
            ElectricalActivity = 0f,
            SummonedByGodId = null,
        };
        WeatherModel.Refresh(rules, cell);
        weather.Cells.Add(cell);
        return string.Format(CultureInfo.InvariantCulture, "A storm gathers at ({0:0}, {1:0}): {2}, wind {3:0.00}", target.X, target.Z, WeatherText.Kind(WeatherModel.Classify(rules, cell)), cell.Wind);
    }
}
