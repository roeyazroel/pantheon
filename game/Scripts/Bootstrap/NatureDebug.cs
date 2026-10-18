using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Disasters;
using Pantheon.Game.Launch;
using Pantheon.Game.Weather;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The debug drivers of weather and disasters (ADR-013, ADR-014): the F8, F10, F11 and F12 hotkeys
/// and the <c>--weather</c> and <c>--disaster</c> launch options. Every change goes through the
/// engine-agnostic commands; this class only wires them to input.
/// </summary>
internal sealed class NatureDebug
{
    private readonly GameSession _session;
    private readonly GodCameraController _camera;
    private readonly Action<string> _setStatus;

    /// <summary>Creates the drivers.</summary>
    /// <param name="session">The session whose world they change.</param>
    /// <param name="camera">The camera, for the point under the cursor.</param>
    /// <param name="setStatus">Shows a status line.</param>
    public NatureDebug(GameSession session, GodCameraController camera, Action<string> setStatus)
    {
        _session = session;
        _camera = camera;
        _setStatus = setStatus;
    }

    /// <summary>The hotkeys: next spell, a storm, an earthquake and a fire at the cursor.</summary>
    /// <returns>Action names and what they do.</returns>
    public IEnumerable<(StringName Action, Action Run)> Hotkeys() =>
    [
        (InputActions.DebugWeatherSpell, () => _setStatus(WeatherCommands.NextSpell(_session))),
        (InputActions.DebugStorm, () => AtCursor("call a storm", point => WeatherCommands.SummonStorm(_session, point))),
        (InputActions.DebugQuake, () => AtCursor("start an earthquake", point => DisasterCommands.StartQuake(_session, point))),
        (InputActions.DebugFire, () => AtCursor("start a fire", point => DisasterCommands.StartFire(_session, point))),
    ];

    /// <summary>Sets up the weather and disaster the launch options ask for, at the focused settlement where a place is needed.</summary>
    /// <param name="options">Launch options.</param>
    /// <param name="focus">The focused settlement's position, if there is one.</param>
    public void ApplyStartOptions(LaunchOptions options, GroundPosition? focus)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Weather is { } weather && Weather(weather, focus) is { } status)
        {
            _setStatus(status);
        }

        if (options.Disaster is { } disaster && focus is { } place)
        {
            _setStatus(disaster switch
            {
                DisasterAtStart.Quake => DisasterCommands.StartQuake(_session, place),
                DisasterAtStart.Fire => DisasterCommands.StartFire(_session, place),
            });
        }
    }

    /// <summary>The weather a <c>--weather</c> value sets up.</summary>
    private string? Weather(WeatherAtStart weather, GroundPosition? focus) => weather switch
    {
        WeatherAtStart.Normal => WeatherCommands.SetSpell(_session, WeatherSpell.Normal),
        WeatherAtStart.Dry => WeatherCommands.SetSpell(_session, WeatherSpell.Dry),
        WeatherAtStart.Wet => WeatherCommands.SetSpell(_session, WeatherSpell.Wet),
        WeatherAtStart.Storm => focus is { } place ? WeatherCommands.SummonStorm(_session, place) : null,
    };

    /// <summary>Runs a command at the ground point under the cursor, or says to point at the ground.</summary>
    private void AtCursor(string what, Func<GroundPosition, string> command) =>
        _setStatus(_camera.CursorGroundPoint is { } cursor ? command(new GroundPosition(cursor.X, cursor.Z)) : "Point at the ground to " + what);
}
