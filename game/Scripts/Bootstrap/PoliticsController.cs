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
/// Treaties, the Divine Council and conflict in the client (ADR-025, ADR-026): the diplomacy panel
/// (P) with its moves, treaties, requests and ways to meet a god (ADR-068), the council panel (U),
/// the debug key that summons the council (J), veiled casting (T), and the launch options that try them out
/// (<c>--council-panel</c>, <c>--summon-council</c>, <c>--treaty</c>, <c>--war-goal</c>,
/// <c>--holy-war</c>, <c>--veil</c>). It reads the session and sends the player's choices to it.
/// </summary>
internal sealed partial class PoliticsController : Node
{
    private GameSession _session = null!;
    private GodCameraController _camera = null!;
    private Action<string> _setStatus = null!;
    private CouncilPanel _panel = null!;

    /// <summary>The diplomacy panel (P).</summary>
    public DiplomacyPanel Diplomacy { get; private set; } = null!;

    /// <summary>Connects the panels to the session and applies the launch options.</summary>
    /// <param name="session">The session.</param>
    /// <param name="camera">The camera, for the village a neutral-territory motion names.</param>
    /// <param name="setStatus">Shows a status line.</param>
    /// <param name="options">Launch options.</param>
    public void Setup(GameSession session, GodCameraController camera, Action<string> setStatus, LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        (_session, _camera, _setStatus) = (session, camera, setStatus);
        Diplomacy = new DiplomacyPanel { Name = "Diplomacy" };
        AddChild(Diplomacy);
        Diplomacy.CommandClicked += (god, kind) => _setStatus(SessionCommands.CommandDiplomacy(_session, god, kind));
        Diplomacy.PolicyClicked += policy => _setStatus(Game.Migration.MigrationCommands.SetPolicy(_session, policy));
        Diplomacy.ActionClicked += action => _setStatus(PoliticsCommands.Run(_session, action));
        Diplomacy.SeekClicked += god => _setStatus(Game.Creatures.CreatureCommands.Seek(_session, god));
        Diplomacy.BringClicked += god => _setStatus(CreatureOf(god) is { } name ? Game.Creatures.CreatureCommands.BringOther(_session, name) : "That god has no Creature");
        _panel = new CouncilPanel { Name = "Council" };
        AddChild(_panel);
        _panel.ActionClicked += action => _setStatus(PoliticsCommands.Run(_session, action));
        _panel.SummonClicked += () => _setStatus(PoliticsCommands.Summon(_session));
        CallDeferred(nameof(ApplyOptions), options.Treaty is { } treaty ? (int)treaty : -1, options.SummonCouncil, options.CouncilPanel, options.GodsMet, options.DiplomacyPanel);
        CallDeferred(nameof(ApplyConflictOptions), options.WarGoal is { } goal ? (int)goal : -1, options.HolyWar, options.Veil);
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_session is not null && _panel.PanelVisible)
        {
            _panel.ShowModel(CouncilReadout.Build(_session, _panel.SecretBallot, Focused()));
        }

        if (_session is not null && Diplomacy.PanelVisible)
        {
            Diplomacy.ShowEntries(Game.Diplomacy.DiplomacyReadout.Build(_session, Diplomacy.Choice), _session.LocalGod?.RefugeePolicy);
        }
    }

    /// <summary>Opens the diplomacy panel (a rival under the god's plaque, ADR-068).</summary>
    public void OpenDiplomacy() => Diplomacy.PanelVisible = true;

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session is null)
        {
            return;
        }

        if (@event.IsActionPressed(InputActions.ToggleDiplomacy))
        {
            Diplomacy.PanelVisible = !Diplomacy.PanelVisible;
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.ToggleCouncil))
        {
            _panel.PanelVisible = !_panel.PanelVisible;
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.DebugCouncil) && DebugTools.On)
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

    /// <summary>Introduces the gods, signs a treaty, summons the council and opens the panels, as the launch options ask.</summary>
    private void ApplyOptions(int treaty, bool summon, bool open, bool met, bool diplomacy)
    {
        Diplomacy.PanelVisible = diplomacy;
        if (met)
        {
            _setStatus(PoliticsCommands.MeetAll(_session));
        }

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

    /// <summary>A god's Creature's name, for the debug meeting; <c>null</c> when it has none.</summary>
    private string? CreatureOf(int godId) => _session.World.Creatures.Find(c => c.GodId == godId)?.Name;

    /// <summary>The settlement nearest the camera's focus.</summary>
    private int? Focused() => SettlementPicker.Nearest(_session.World, new GroundPosition(_camera.Model.Focus.X, _camera.Model.Focus.Y), float.MaxValue)?.Id;
}
