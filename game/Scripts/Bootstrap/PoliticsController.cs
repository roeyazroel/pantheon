using System;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.UI;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Launch;
using Pantheon.Game.Politics;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Treaties, the Divine Council and conflict in the client (ADR-025, ADR-026): the council panel
/// (U), the debug key that summons the council (J), veiled casting (T), the diplomacy panel's
/// treaty, request and war buttons, and the launch options that try them out
/// (<c>--council-panel</c>, <c>--summon-council</c>, <c>--treaty</c>, <c>--war-goal</c>,
/// <c>--holy-war</c>, <c>--veil</c>). It reads the session and sends the player's choices to it.
/// </summary>
internal sealed partial class PoliticsController : Node
{
    private GameSession _session = null!;
    private GodCameraController _camera = null!;
    private Action<string> _setStatus = null!;
    private CouncilPanel _panel = null!;

    /// <summary>Connects the panels to the session and applies the launch options.</summary>
    /// <param name="session">The session.</param>
    /// <param name="camera">The camera, for the village a neutral-territory motion names.</param>
    /// <param name="diplomacy">The diplomacy panel, whose treaty buttons this answers.</param>
    /// <param name="setStatus">Shows a status line.</param>
    /// <param name="options">Launch options.</param>
    public void Setup(GameSession session, GodCameraController camera, DiplomacyPanel diplomacy, Action<string> setStatus, LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(diplomacy);
        ArgumentNullException.ThrowIfNull(options);
        (_session, _camera, _setStatus) = (session, camera, setStatus);
        _panel = new CouncilPanel { Name = "Council" };
        AddChild(_panel);
        _panel.ActionClicked += action => _setStatus(PoliticsCommands.Run(_session, action));
        diplomacy.ActionClicked += action => _setStatus(PoliticsCommands.Run(_session, action));
        CallDeferred(nameof(ApplyOptions), options.Treaty is { } treaty ? (int)treaty : -1, options.SummonCouncil, options.CouncilPanel);
        CallDeferred(nameof(ApplyConflictOptions), options.WarGoal is { } goal ? (int)goal : -1, options.HolyWar, options.Veil);
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_session is not null && _panel.PanelVisible)
        {
            _panel.ShowModel(CouncilReadout.Build(_session, _panel.SecretBallot, Focused()));
        }
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session is null)
        {
            return;
        }

        if (@event.IsActionPressed(InputActions.ToggleCouncil))
        {
            _panel.PanelVisible = !_panel.PanelVisible;
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.DebugCouncil))
        {
            _setStatus(PoliticsCommands.Summon(_session));
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.ToggleVeil))
        {
            _session.Veiled = !_session.Veiled;
            _setStatus(_session.Veiled ? "Your miracles are veiled: dearer, and unattributed unless traced (T)" : "Your miracles are seen as yours (T)");
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Signs a treaty, summons the council and opens its panel, as the launch options ask.</summary>
    private void ApplyOptions(int treaty, bool summon, bool open)
    {
        if (treaty >= 0)
        {
            _setStatus(PoliticsCommands.SignTreaty(_session, (Domain.Politics.TreatyKind)treaty));
        }

        if (summon)
        {
            _setStatus(PoliticsCommands.Summon(_session));
        }

        _panel.PanelVisible = open;
    }

    /// <summary>Declares a war, starts a holy war at the focused village and veils the player's miracles, as the launch options ask.</summary>
    private void ApplyConflictOptions(int goal, bool holyWar, bool veil)
    {
        if (goal >= 0)
        {
            _setStatus(Game.Conflict.ConflictCommands.DeclareWar(_session, (Domain.Conflict.WarGoal)goal));
        }

        if (holyWar && Focused() is { } settlement)
        {
            _setStatus(Game.Conflict.ConflictCommands.StartHolyWar(_session, settlement));
        }

        _session.Veiled = veil;
    }

    /// <summary>The settlement nearest the camera's focus.</summary>
    private int? Focused() => SettlementPicker.Nearest(_session.World, new GroundPosition(_camera.Model.Focus.X, _camera.Model.Focus.Y), float.MaxValue)?.Id;
}
