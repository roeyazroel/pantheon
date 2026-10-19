using System;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.UI;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Game.Society;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Settlements' society in the client (ADR-023): the Settlements panel (N) on the village nearest
/// the cursor, the player's priorities, and the launch options that try them out (<c>--priority</c>,
/// <c>--settlement-panel</c>) and the culture of <c>--doctrine</c> (ADR-024). It reads the session and sends the player's choices to it.
/// </summary>
internal sealed partial class SocietyController : Node
{
    // How far from the cursor a village may be to be the one shown.
    private const float PickDistance = 250f;

    private GameSession _session = null!;
    private GodCameraController _camera = null!;
    private Action<string> _setStatus = null!;
    private SettlementPanel _panel = null!;
    private int? _settlementId;

    /// <summary>Connects the panel to the session and applies the launch options.</summary>
    /// <param name="session">The session.</param>
    /// <param name="camera">The camera, for the cursor and the focus.</param>
    /// <param name="setStatus">Shows a status line.</param>
    /// <param name="options">Launch options.</param>
    public void Setup(GameSession session, GodCameraController camera, Action<string> setStatus, LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        (_session, _camera, _setStatus) = (session, camera, setStatus);
        _panel = new SettlementPanel { Name = "Settlements" };
        AddChild(_panel);
        _panel.PriorityChosen += (settlement, priority) => _setStatus(SocietyCommands.SetPriority(_session, settlement, priority));
        _panel.ActionClicked += action => _setStatus(Game.Politics.PoliticsCommands.Run(_session, action));
        CallDeferred(nameof(ApplyOptions), options.Priority is { } priority ? (int)priority : -1, options.SettlementPanel, options.Doctrine is { } doctrine ? (int)doctrine : -1);
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_session is not null && _panel.PanelVisible && _settlementId is { } id && SettlementPanelReadout.Build(_session, id) is { } model)
        {
            _panel.ShowModel(model);
        }
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session is null || !@event.IsActionPressed(InputActions.ToggleSettlements))
        {
            return;
        }

        var picked = Pick(cursorFirst: true);
        if (_panel.PanelVisible && (picked is null || picked == _settlementId))
        {
            _panel.PanelVisible = false;
        }
        else if (picked is not null)
        {
            _settlementId = picked;
            _panel.PanelVisible = true;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>Sets the focused village's priority and culture and opens the panel, as the launch options ask.</summary>
    private void ApplyOptions(int priority, bool open, int doctrine)
    {
        if (Pick(cursorFirst: false) is not { } id)
        {
            return;
        }

        if (priority >= 0)
        {
            _setStatus(SocietyCommands.SetPriority(_session, id, (Domain.Society.SettlementPriority)priority));
        }

        if (doctrine >= 0)
        {
            _setStatus(Game.Culture.CultureCommands.Impose(_session, id, (CultureAtStart)doctrine));
        }

        _settlementId = id;
        _panel.PanelVisible = open;
    }

    /// <summary>The village nearest the cursor (when asked), else the one nearest the camera's focus.</summary>
    private int? Pick(bool cursorFirst)
    {
        var world = _session.World;
        if (cursorFirst && _camera.CursorGroundPoint is { } cursor && SettlementPicker.Nearest(world, new GroundPosition(cursor.X, cursor.Z), PickDistance) is { } near)
        {
            return near.Id;
        }

        var focus = new GroundPosition(_camera.Model.Focus.X, _camera.Model.Focus.Y);
        return SettlementPicker.Nearest(world, focus, float.MaxValue)?.Id;
    }
}
