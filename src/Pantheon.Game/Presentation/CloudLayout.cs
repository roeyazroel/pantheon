using System;
using System.Collections.Generic;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Presentation;

/// <summary>
/// How a weather cell looks (ADR-013), as pure functions of the cell, the seed and the time: its
/// puffs, colour and opacity, its rain and its lightning. Presentation only; it draws no simulation
/// random numbers, so showing the weather never changes it.
/// </summary>
public static class CloudLayout
{
    // Lightning is decided per slot of this many seconds.
    private const double FlashSlotSeconds = 0.25;

    /// <summary>The puffs of a cloud, relative to its centre: positions spread over most of its radius.</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="cell">The cell.</param>
    /// <param name="view">Weather view settings.</param>
    /// <returns>The puffs; the same for the same cell every time.</returns>
    public static IReadOnlyList<CloudPuff> Puffs(WorldSeed seed, WeatherCell cell, WeatherViewSettings view)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(view);
        var ratio = cell.Radius / view.PuffSpacing;
        var count = Math.Clamp((int)MathF.Round(ratio * ratio * 0.8f), 3, view.MaxPuffs);
        var puffs = new CloudPuff[count];
        for (var i = 0; i < count; i++)
        {
            var key = (ulong)(uint)i;
            var angle = 2f * MathF.PI * Unit(seed, cell.Id, key, 0);
            var distance = cell.Radius * 0.75f * MathF.Sqrt(Unit(seed, cell.Id, key, 1));
            puffs[i] = new CloudPuff(
                MathF.Cos(angle) * distance,
                MathF.Sin(angle) * distance,
                view.CloudHeight + (view.CloudThickness * Unit(seed, cell.Id, key, 2)),
                view.PuffSize * (0.6f + (0.8f * Unit(seed, cell.Id, key, 3))));
        }

        return puffs;
    }

    /// <summary>
    /// A cloud's colour and opacity: darker and denser the deeper the low and the harder it rains,
    /// fainter as it dries out.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="view">Weather view settings.</param>
    /// <returns>Colour and opacity (0..1).</returns>
    public static (RgbColor Color, float Opacity) Look(WeatherCell cell, WeatherViewSettings view)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(view);
        var darkness = Darkness(cell);
        var density = view.CloudOpacity + ((1f - view.CloudOpacity) * 0.8f * darkness);
        var opacity = density * Math.Clamp(cell.Humidity / 0.6f, 0f, 1f);
        return (RgbColor.Lerp(view.ClearColor, view.StormColor, darkness), opacity);
    }

    /// <summary>How gloomy the light is at a point: the darkness of the clouds over it, weighted by how fully they cover it.</summary>
    /// <param name="rules">Weather rules, for the cells' coverage.</param>
    /// <param name="weather">The weather.</param>
    /// <param name="point">The point.</param>
    /// <returns>Gloom (0..1): 1 under the heart of the darkest storm.</returns>
    public static float Gloom(WeatherRules rules, WeatherState weather, GroundPosition point)
    {
        ArgumentNullException.ThrowIfNull(weather);
        var gloom = 0f;
        foreach (var cell in weather.Cells)
        {
            var wetness = Math.Clamp(cell.Humidity / 0.6f, 0f, 1f);
            gloom = Math.Max(gloom, Darkness(cell) * wetness * WeatherModel.Coverage(rules, cell, point));
        }

        return gloom;
    }

    /// <summary>How many raindrops fall from a cloud: more for heavier rain and larger clouds.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="view">Weather view settings.</param>
    /// <returns>Drops (0 to <see cref="WeatherViewSettings.MaxRainDrops"/>).</returns>
    public static int RainDrops(WeatherCell cell, WeatherViewSettings view)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(view);
        var size = Math.Min(1f, cell.Radius / view.FullRainRadius);
        return (int)MathF.Round(view.MaxRainDrops * Math.Clamp(cell.Precipitation, 0f, 1f) * size * size);
    }

    /// <summary>
    /// Whether lightning strikes from a cloud in the time slot that contains <paramref name="seconds"/>,
    /// and where: only thunderstorms strike, more often the fiercer they are.
    /// </summary>
    /// <param name="seed">World seed.</param>
    /// <param name="cell">The cell.</param>
    /// <param name="rules">Weather rules, for when a cell is a thunderstorm.</param>
    /// <param name="view">Weather view settings.</param>
    /// <param name="seconds">Simulation time in seconds.</param>
    /// <returns>The slot and the strike's offset from the centre, or <c>null</c> when none strikes.</returns>
    public static LightningStrike? Lightning(WorldSeed seed, WeatherCell cell, WeatherRules rules, WeatherViewSettings view, double seconds)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(view);
        if (WeatherModel.Classify(rules, cell) != WeatherKind.Thunderstorm)
        {
            return null;
        }

        var fierceness = Math.Clamp((cell.ElectricalActivity - rules.ThunderActivity) / Math.Max(0.01f, 1f - rules.ThunderActivity), 0.2f, 1f);
        var slot = (long)Math.Floor(seconds / FlashSlotSeconds);
        var key = unchecked((ulong)slot);
        var chance = view.FlashesPerSecond * FlashSlotSeconds * fierceness;
        if (Unit(seed, cell.Id, key, 4) >= chance)
        {
            return null;
        }

        var angle = 2f * MathF.PI * Unit(seed, cell.Id, key, 5);
        var distance = cell.Radius * 0.7f * MathF.Sqrt(Unit(seed, cell.Id, key, 6));
        return new LightningStrike(slot, MathF.Cos(angle) * distance, MathF.Sin(angle) * distance);
    }

    /// <summary>How dark a cloud is (0..1): deeper lows and heavier rain.</summary>
    private static float Darkness(WeatherCell cell) => Math.Clamp((0.6f * cell.Pressure) + (0.6f * cell.Precipitation), 0f, 1f);

    /// <summary>A hashed number in [0, 1) for a cell and a key.</summary>
    private static float Unit(WorldSeed seed, int cellId, ulong key, ulong purpose) =>
        PresentationHash.Unit(seed.Value, (ulong)(uint)cellId, key, purpose);
}

/// <summary>One puff of a cloud.</summary>
/// <param name="OffsetX">East-west offset from the cloud's centre, in metres.</param>
/// <param name="OffsetZ">North-south offset from the cloud's centre, in metres.</param>
/// <param name="Height">Height above sea level, in metres.</param>
/// <param name="Size">Width, in metres.</param>
public readonly record struct CloudPuff(float OffsetX, float OffsetZ, float Height, float Size);

/// <summary>A lightning strike from a cloud.</summary>
/// <param name="Slot">The time slot it belongs to, so a view shows it once.</param>
/// <param name="OffsetX">East-west offset from the cloud's centre, in metres.</param>
/// <param name="OffsetZ">North-south offset from the cloud's centre, in metres.</param>
public readonly record struct LightningStrike(long Slot, float OffsetX, float OffsetZ);
