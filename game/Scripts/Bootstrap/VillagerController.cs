using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.Presentation.Villagers;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Game.Villagers;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The villagers the gods follow in the client (spec §80, ADR-043): / follows the figure at the
/// cursor, or with nobody there looks at the one followed, the camera keeping them in view until the
/// player moves it; Shift+/ lets them go; <c>--villager</c> follows one of the focused village's
/// people at the start. It shows every followed villager, and reads the session and sends the
/// player's choices to it.
/// </summary>
internal sealed partial class VillagerController : Node
{
    // How close the camera comes to look at the followed villager.
    private const float LookDistance = 45f;

    private readonly Dictionary<int, VillagerView> _views = [];
    private readonly HashSet<int> _seen = [];
    private GameSession _session = null!;
    private GodCameraController _camera = null!;
    private Node3D _layer = null!;
    private PresentationContent _presentation = null!;
    private Action<string> _setStatus = null!;
    private int? _tracked;
    private System.Numerics.Vector2 _trackedFocus;

    /// <summary>Connects the villagers to the session and applies the launch option.</summary>
    /// <param name="session">The session.</param>
    /// <param name="camera">The camera, for the cursor and the focus.</param>
    /// <param name="layer">The node that holds the villagers' figures.</param>
    /// <param name="presentation">Presentation settings: the crowd's rounds and colours, and the gods' colours.</param>
    /// <param name="setStatus">Shows a status line.</param>
    /// <param name="options">Launch options.</param>
    public void Setup(GameSession session, GodCameraController camera, Node3D layer, PresentationContent presentation, Action<string> setStatus, LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        (_session, _camera, _layer, _presentation, _setStatus) = (session, camera, layer, presentation, setStatus);
        _session.WorldReplaced += Clear;
        if (options.Villager)
        {
            CallDeferred(nameof(FollowAtStart), options.CameraDistance ?? LookDistance);
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_session is null)
        {
            return;
        }

        var world = _session.World;
        var rules = _session.Content.SettlementRules.Villagers;
        _seen.Clear();
        foreach (var villager in world.Villagers)
        {
            _seen.Add(villager.Id);
            var color = _presentation.DivineView.GodColor(villager.FollowerGodId).ToGodot();
            if (!_views.TryGetValue(villager.Id, out var view) || view.GodColor != color)
            {
                view?.Free();
                var look = Pantheon.Game.Population.PersonLooks.Of(_presentation.SettlementView.People, world.Seed.Value, villager.SettlementId, villager.Slot ?? villager.Id, Role(villager.Work));
                view = new VillagerView(_layer, villager, Pantheon.Client.Presentation.People.PersonAssets.For(_presentation.SettlementView), look, color);
                _views[villager.Id] = view;
            }

            view.Sync(villager, world, rules, _session.Terrain, (float)delta, !_session.Scheduler.IsPaused);
        }

        foreach (var id in new List<int>(_views.Keys))
        {
            if (!_seen.Contains(id))
            {
                _views[id].Free();
                _views.Remove(id);
            }
        }

        Track();
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session is null || !@event.IsActionPressed(InputActions.VillagerFollow))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        if (Input.IsKeyPressed(Key.Shift))
        {
            _setStatus(VillagerCommands.Release(_session));
            return;
        }

        var cursor = _camera.CursorGroundPoint is { } point ? new GroundPosition(point.X, point.Z) : (GroundPosition?)null;
        if (cursor is { } at && VillagerCommands.Pick(_session, _presentation.SettlementView, at) is { } picked)
        {
            _setStatus(VillagerCommands.Follow(_session, picked));
        }
        else if (VillagerCommands.Of(_session.World, _session.LocalGodId) is { } followed)
        {
            Look(followed, LookDistance, snap: false);
            _setStatus($"You look at {followed.Name}, {VillagerText.Doing(followed, _session.World)}: the camera follows them until you move it");
        }
        else
        {
            _setStatus("Nobody there: point at someone in a village and press / to follow them");
        }
    }

    /// <summary>Follows one of the people of the village nearest the camera's focus and looks at them.</summary>
    private void FollowAtStart(float distance)
    {
        var focus = new GroundPosition(_camera.Model.Focus.X, _camera.Model.Focus.Y);
        if (SettlementPicker.Nearest(_session.World, focus, float.MaxValue) is { } village && VillagerCommands.First(_session, _presentation.SettlementView, village) is { } first)
        {
            _setStatus(VillagerCommands.Follow(_session, first));
        }

        if (VillagerCommands.Of(_session.World, _session.LocalGodId) is { } followed)
        {
            Look(followed, distance, snap: true);
        }
    }

    /// <summary>Turns the camera on a villager, and keeps it on them from now on.</summary>
    private void Look(VillagerState villager, float distance, bool snap)
    {
        _camera.FocusOn(villager.Position, distance, snap);
        (_tracked, _trackedFocus) = (villager.Id, _camera.Model.TargetFocus);
    }

    /// <summary>Keeps the camera on the villager it looks at, until the player moves the camera or the villager is gone.</summary>
    private void Track()
    {
        if (_tracked is not { } id)
        {
            return;
        }

        if (_session.World.Villagers.Find(v => v.Id == id) is not { } villager || System.Numerics.Vector2.Distance(_camera.Model.TargetFocus, _trackedFocus) > 0.5f)
        {
            _tracked = null;
            return;
        }

        _camera.FocusOn(villager.Position, null, snap: false);
        _trackedFocus = _camera.Model.TargetFocus;
    }

    /// <summary>Drops every figure, for a new or loaded world.</summary>
    private void Clear()
    {
        foreach (var view in _views.Values)
        {
            view.Free();
        }

        _views.Clear();
        _tracked = null;
    }

    /// <summary>The crowd's role for a villager's work, for its colour.</summary>
    private static Game.Population.CitizenRole Role(VillagerWork work) => work switch
    {
        VillagerWork.Farming => Game.Population.CitizenRole.Farmer,
        VillagerWork.Building => Game.Population.CitizenRole.Builder,
        VillagerWork.Chores => Game.Population.CitizenRole.Villager,
    };
}
