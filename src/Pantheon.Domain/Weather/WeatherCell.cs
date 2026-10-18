namespace Pantheon.Domain.Weather;

/// <summary>
/// A moving body of weather (spec §16): a cloud mass that drifts with the wind, gathers water over
/// the sea and rains it out over land. Storms are deep lows: strong wind, heavy rain and lightning.
/// Positions are in metres on the ground plane; values without units are normalised to 0..1.
/// </summary>
public sealed class WeatherCell
{
    /// <summary>Unique id, never reused within a world.</summary>
    public required int Id { get; init; }

    /// <summary>East-west position of the centre in metres.</summary>
    public required float X { get; set; }

    /// <summary>North-south position of the centre in metres.</summary>
    public required float Z { get; set; }

    /// <summary>East-west velocity in metres per simulated second.</summary>
    public required float VelocityX { get; set; }

    /// <summary>North-south velocity in metres per simulated second.</summary>
    public required float VelocityZ { get; set; }

    /// <summary>Radius of the weather on the ground, in metres.</summary>
    public required float Radius { get; init; }

    /// <summary>Depth of the low (0 = settled, 1 = a deep storm). It drives wind and rain, and fills over land.</summary>
    public required float Pressure { get; set; }

    /// <summary>Warmth of the air mass (0 = freezing, 1 = hot), taken up from the land and sea beneath it.</summary>
    public required float Temperature { get; set; }

    /// <summary>Water the cell carries (0..1). The sea refills it; rain spends it.</summary>
    public required float Humidity { get; set; }

    /// <summary>How hard it rains beneath the cell now (0..1).</summary>
    public required float Precipitation { get; set; }

    /// <summary>Wind strength beneath the cell now (0..1).</summary>
    public required float Wind { get; set; }

    /// <summary>Lightning activity now (0..1).</summary>
    public required float ElectricalActivity { get; set; }

    /// <summary>The god whose Rain miracle summoned the cell, or <c>null</c> for natural weather.</summary>
    public required int? SummonedByGodId { get; init; }
}
