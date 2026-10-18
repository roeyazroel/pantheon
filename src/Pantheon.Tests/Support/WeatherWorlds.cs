using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Weather;

namespace Pantheon.Tests.Support;

/// <summary>Weather states and pinned weather rules for tests.</summary>
internal static class WeatherWorlds
{
    /// <summary>
    /// Rules with fixed values (the balance shipped with Milestone 7), so weather tests keep their
    /// meaning when <c>weather.json</c> is retuned.
    /// </summary>
    public static WeatherRules Rules { get; } = new()
    {
        StepIntervalTicks = 5,
        MaxCells = 10,
        InitialCells = 4,
        CellsPerSecond = 0.012f,
        DrySpellCellFactor = 0.1f,
        WetSpellCellFactor = 2f,
        StormShare = 0.12f,
        WetSpellStormShare = 0.35f,
        NormalSpellMinSeconds = 360f,
        NormalSpellMaxSeconds = 720f,
        DrySpellMinSeconds = 300f,
        DrySpellMaxSeconds = 600f,
        WetSpellMinSeconds = 180f,
        WetSpellMaxSeconds = 360f,
        DryAfterNormalChance = 0.5f,
        NormalAfterNormalChance = 0.2f,
        WindTurnDegrees = 50f,
        CellSpeed = 5f,
        SummonedSpeedFactor = 0.15f,
        MinRadius = 160f,
        MaxRadius = 380f,
        MinHumidity = 0.55f,
        ShowerPressure = 0.45f,
        StormPressure = 0.75f,
        SummonedPressure = 0.3f,
        LandFillingPerSecond = 0.0015f,
        SeaHumidityPerSecond = 0.01f,
        LandDissipationPerSecond = 0.0015f,
        TemperatureFollowPerSecond = 0.02f,
        RainHumidity = 0.35f,
        RainBase = 0.5f,
        RainFromPressure = 0.6f,
        RainDrainPerSecond = 0.006f,
        WindFromPressure = 1f,
        LightningFactor = 3f,
        StormWind = 0.65f,
        ThunderActivity = 0.5f,
        VisibleRain = 0.1f,
        EdgeShare = 0.3f,
        RainSoakPerSecond = 0.03f,
        EvaporationPerSecond = 0.0025f,
        DrySpellDryingFactor = 1.6f,
        GroundwaterPerSecond = 0.0009f,
        StormFoodLossPerSecond = 0.012f,
        StormConstructionLossPerSecond = 0.02f,
    };

    /// <summary>Clear skies, a normal spell not yet scheduled and a west wind.</summary>
    /// <returns>A new weather state.</returns>
    public static WeatherState Calm() => new()
    {
        Cells = [],
        NextCellId = 1,
        Spell = WeatherSpell.Normal,
        SpellEndsTick = null,
        WindDirection = 0f,
    };

    /// <summary>Adds a cell to a world's weather, with its rain, wind and lightning computed from the rest.</summary>
    /// <param name="world">The world.</param>
    /// <param name="position">Centre of the cell.</param>
    /// <param name="radius">Radius in metres.</param>
    /// <param name="humidity">Humidity (0..1).</param>
    /// <param name="pressure">Pressure (0..1).</param>
    /// <param name="velocityX">East-west velocity in metres per second.</param>
    /// <returns>The cell.</returns>
    public static WeatherCell AddCell(WorldState world, GroundPosition position, float radius, float humidity, float pressure, float velocityX = 0f)
    {
        var cell = new WeatherCell
        {
            Id = world.Weather.NextCellId++,
            X = position.X,
            Z = position.Z,
            VelocityX = velocityX,
            VelocityZ = 0f,
            Radius = radius,
            Pressure = pressure,
            Temperature = 0.6f,
            Humidity = humidity,
            Precipitation = 0f,
            Wind = 0f,
            ElectricalActivity = 0f,
            SummonedByGodId = null,
        };
        WeatherModel.Refresh(Rules, cell);
        world.Weather.Cells.Add(cell);
        return cell;
    }
}
