using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Weather;

/// <summary>
/// Turns the hour into the scene's light (ADR-079): the one light swings along the sun's or moon's way
/// and the sky takes the hour's colours. The light's strength and colour it leaves to the weather, which
/// dims them under storms and in an eclipse.
/// </summary>
internal sealed class DaylightView
{
    // How often the sky's colours are redrawn, in seconds: each change refilters the light it gives the land.
    private const double SkySeconds = 0.5;

    private readonly DayNightSettings _settings;
    private readonly DirectionalLight3D? _sun;
    private readonly ShaderMaterial? _sky;
    private double _sinceSky = SkySeconds;

    /// <summary>Creates the view.</summary>
    /// <param name="settings">Day and night settings.</param>
    /// <param name="sun">The light; <c>null</c> for none.</param>
    /// <param name="environment">The scene's environment, for its sky; <c>null</c> for none.</param>
    public DaylightView(DayNightSettings settings, DirectionalLight3D? sun, Godot.Environment? environment)
    {
        (_settings, _sun) = (settings, sun);
        _sky = environment?.Sky?.SkyMaterial as ShaderMaterial;
    }

    /// <summary>Sets the light's way and the sky for the hour now.</summary>
    /// <param name="tick">The world's tick.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <returns>The light at this hour.</returns>
    public Daylight Update(long tick, double delta)
    {
        var hour = DayClock.Held ?? (DayClock.On ? DayNight.HourAt(_settings, tick, DayClock.Shift) : _settings.HeldHour);
        var light = DayNight.At(_settings, hour);
        DayClock.Hour = hour;
        DayClock.Word = DayClock.On || DayClock.Held is not null ? DayNight.WordOf(DayNight.PartOf(_settings, hour)) : string.Empty;
        DayClock.Night = light.Night;
        DayClock.Brightness = Mathf.Clamp(light.Ambient / _settings.Keys[^1].Ambient * 0.5f, 0.32f, 1f);
        if (_sun is not null)
        {
            var toLight = new Vector3(light.ToLight.X, light.ToLight.Y, light.ToLight.Z);
            _sun.Basis = Basis.LookingAt(-toLight, Vector3.Up);
        }

        _sinceSky += delta;
        if (_sky is not null && _sinceSky >= SkySeconds)
        {
            _sinceSky = 0.0;
            _sky.SetShaderParameter("top_color", light.SkyTop.ToGodot());
            _sky.SetShaderParameter("horizon_color", light.SkyHorizon.ToGodot());
        }

        return light;
    }
}
