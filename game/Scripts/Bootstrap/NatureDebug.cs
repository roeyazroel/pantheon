using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Creatures;
using Pantheon.Game.Disasters;
using Pantheon.Game.Launch;
using Pantheon.Game.Migration;
using Pantheon.Game.Weather;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The debug drivers of weather, seasons, disasters, migration and Creature meetings (ADR-013, ADR-014,
/// ADR-016, ADR-017, ADR-022, ADR-028, ADR-029): the F8, F10, F11, F12, L, M, K, Y and I hotkeys and the
/// <c>--weather</c>, <c>--season</c>, <c>--disaster</c>, <c>--migrate</c>, <c>--refugees</c> and <c>--meet</c> launch options. Every
/// change goes through the engine-agnostic commands; this class only wires them to input.
/// </summary>
internal sealed class NatureDebug
{
    private readonly GameSession _session;
    private readonly GodCameraController _camera;
    private readonly Action<string> _setStatus;
    private HazardKind? _lastHazard;

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

    /// <summary>The hotkeys: next spell, a storm, an earthquake, a fire and a migration at the cursor.</summary>
    /// <returns>Action names and what they do.</returns>
    public IEnumerable<(StringName Action, Action Run)> Hotkeys() =>
    [
        (InputActions.DebugWeatherSpell, () => _setStatus(WeatherCommands.NextSpell(_session))),
        (InputActions.DebugStorm, () => AtCursor("call a storm", point => WeatherCommands.SummonStorm(_session, point))),
        (InputActions.DebugQuake, () => AtCursor("start an earthquake", point => DisasterCommands.StartQuake(_session, point))),
        (InputActions.DebugFire, () => AtCursor("start a fire", point => DisasterCommands.StartFire(_session, point))),
        (InputActions.DebugFlood, () => AtCursor("start a flood", point => DisasterCommands.StartFlood(_session, point))),
        (InputActions.DebugMigrate, () => AtCursor("send people away", point => MigrationCommands.SendFrom(_session, point))),
        (InputActions.DebugMeet, () => _setStatus(CreatureCommands.BringOther(_session))),
        (InputActions.DebugSeason, () => _setStatus(SeasonCommands.NextSeason(_session))),
        (InputActions.DebugHazard, NextHazard),
    ];

    /// <summary>Starts the next hazard of the cycle at the cursor (I).</summary>
    private void NextHazard()
    {
        var kind = HazardCommands.Next(_lastHazard);
        if (_camera.CursorGroundPoint is { } cursor)
        {
            _lastHazard = kind;
            _setStatus(HazardCommands.Start(_session, kind, new GroundPosition(cursor.X, cursor.Z)));
        }
        else
        {
            _setStatus("Point at the ground to start a hazard");
        }
    }

    /// <summary>Sets up the weather, disaster, refugee policy and migration the launch options ask for, at the focused settlement where a place is needed.</summary>
    /// <param name="options">Launch options.</param>
    /// <param name="focus">The focused settlement's position, if there is one.</param>
    public void ApplyStartOptions(LaunchOptions options, GroundPosition? focus)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Season is { } season)
        {
            _setStatus(SeasonCommands.SetSeason(_session, season));
        }

        if (options.Weather is { } weather && Weather(weather, focus) is { } status)
        {
            _setStatus(status);
        }

        if (options.Refugees is { } policy)
        {
            _setStatus(MigrationCommands.SetPolicy(_session, policy));
        }

        if (options.Migrate is { } people && focus is { } home)
        {
            _setStatus(MigrationCommands.SendFrom(_session, home, people));
        }

        if (options.Hand is { } hand && (hand == HandAtStart.Creature ? _session.LocalCreature?.Position : focus) is { } grip)
        {
            _setStatus(Pantheon.Game.Hand.HandCommands.Use(_session, Pantheon.Domain.Hand.HandAction.PickUp, grip));
        }

        if (options.Roads)
        {
            _setStatus(Pantheon.Game.Resources.ResourceCommands.BuildRoads(_session));
        }

        if (options.Stores is { } full)
        {
            _setStatus(Pantheon.Game.Resources.ResourceCommands.SetStores(_session, full));
        }

        if (options.Prophet && focus is { } village)
        {
            _setStatus(Pantheon.Game.Notables.NotableCommands.RaiseProphet(_session, village));
        }

        if (options.Overview)
        {
            _camera.Model.ToggleOverview();
            _camera.Model.SnapToTarget();
        }

        if (options.Meet is { } name)
        {
            _setStatus(CreatureCommands.BringOther(_session, name));
        }

        if (options.Disaster is { } disaster && focus is { } place)
        {
            _setStatus(disaster switch
            {
                DisasterAtStart.Quake => DisasterCommands.StartQuake(_session, place),
                DisasterAtStart.Fire => DisasterCommands.StartFire(_session, place),
                DisasterAtStart.Flood => DisasterCommands.StartFlood(_session, place),
                DisasterAtStart.Tornado => HazardCommands.Start(_session, HazardKind.Tornado, place),
                DisasterAtStart.Landslide => HazardCommands.Start(_session, HazardKind.Landslide, place),
                DisasterAtStart.Meteor => HazardCommands.Start(_session, HazardKind.Meteor, place),
                DisasterAtStart.Blight => HazardCommands.Start(_session, HazardKind.Blight, place),
                DisasterAtStart.Locusts => HazardCommands.Start(_session, HazardKind.Locusts, place),
                DisasterAtStart.Tsunami => HazardCommands.Start(_session, HazardKind.Tsunami, place),
                DisasterAtStart.Eruption => HazardCommands.Start(_session, HazardKind.Eruption, place),
            });
            if (disaster == DisasterAtStart.Eruption && _session.World.Disasters.Volcano is { } volcano)
            {
                // The volcano may stand far from any village, so the camera goes to it.
                _camera.FocusOn(_session.World.Regions.GetRegionCenter(volcano.RegionId), distance: null, snap: true);
            }
        }
    }

    /// <summary>The weather a <c>--weather</c> value sets up.</summary>
    private string? Weather(WeatherAtStart weather, GroundPosition? focus) => weather switch
    {
        WeatherAtStart.Normal => WeatherCommands.SetSpell(_session, WeatherSpell.Normal),
        WeatherAtStart.Dry => WeatherCommands.SetSpell(_session, WeatherSpell.Dry),
        WeatherAtStart.Wet => WeatherCommands.SetSpell(_session, WeatherSpell.Wet),
        WeatherAtStart.Storm => focus is { } place ? WeatherCommands.SummonStorm(_session, place) : null,
        WeatherAtStart.HeatWave => WeatherCommands.SetSpell(_session, WeatherSpell.HeatWave),
        WeatherAtStart.ColdWave => WeatherCommands.SetSpell(_session, WeatherSpell.ColdWave),
        WeatherAtStart.Hurricane => focus is { } eye ? WeatherCommands.SummonStorm(_session, eye, hurricane: true) : null,
    };

    /// <summary>Runs a command at the ground point under the cursor, or says to point at the ground.</summary>
    private void AtCursor(string what, Func<GroundPosition, string> command) =>
        _setStatus(_camera.CursorGroundPoint is { } cursor ? command(new GroundPosition(cursor.X, cursor.Z)) : "Point at the ground to " + what);
}
