using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Pantheon.Client.Network;
using Pantheon.Client.UI;
using Pantheon.Client.UI.Flow;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Flow;
using Pantheon.Game.History;
using Pantheon.Persistence;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The game as a game (ADR-020): the start screen, the pause menu (Esc), saving and loading named
/// worlds and an autosave, hosting and joining from the menu, the history panel (H), the
/// first-minutes guide, and the end of a game. Joining another host starts the game over in place
/// with its address, since a joined world replaces the whole session.
/// </summary>
internal sealed partial class GameFlow : Node
{
    // Real seconds between autosaves of a world this game owns.
    private const double AutosaveSeconds = 300.0;

    // F2 seeds stay short enough to read and type.
    private const long MaxRandomSeed = 1_000_000_000;

    private readonly Guide _guide = new();
    private GameSession _session = null!;
    private NetworkDriver _network = null!;
    private DivineController _divine = null!;
    private DiplomacyPanel _diplomacy = null!;
    private Action<string> _setStatus = null!;
    private MenuScreen _menu = null!;
    private HistoryPanel _history = null!;
    private GuidePanel _guidePanel = null!;
    private bool _wasPaused;
    private bool _ended;
    private double _sinceAutosave;

    /// <summary>Where saves live.</summary>
    public static string SaveDirectory => ProjectSettings.GlobalizePath("user://saves");

    /// <summary>Connects the flow to the game and shows the start screen or the guide as asked.</summary>
    /// <param name="session">The session.</param>
    /// <param name="network">The network driver.</param>
    /// <param name="divine">The player's divine interface, for its events and selection.</param>
    /// <param name="diplomacy">The diplomacy panel, for the guide.</param>
    /// <param name="setStatus">Shows a status line.</param>
    /// <param name="options">Launch options: the start screen (by default when there are no options), the guide, and the history panel or pause menu open from the start.</param>
    /// <param name="settings">The player's settings, for the settings page.</param>
    public void Setup(GameSession session, NetworkDriver network, DivineController divine, DiplomacyPanel diplomacy, Action<string> setStatus, Game.Launch.LaunchOptions options, Game.Settings.GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        var showStart = options.Menu ?? (options.Seed is null && options.LoadPath is null && options.Join is null && options.HostPort is null && options.QuitAfterFrames is null);
        (_session, _network, _divine, _diplomacy, _setStatus) = (session, network, divine, diplomacy, setStatus);
        _menu = new MenuScreen { Name = "Menus" };
        _menu.Species = session.Content.Creatures.Species.Select(s => (s.Name, s.Description)).ToList();
        _menu.Settings = settings;
        _menu.SettingsChanged += changed =>
        {
            SettingsApplier.Apply(changed, GetViewport(), GetParent().GetNodeOrNull<DirectionalLight3D>("Sun"), startup: false);
            Startup.SaveSettings(changed);
        };
        _history = new HistoryPanel { Name = "History" };
        _guidePanel = new GuidePanel { Name = "Guide" };
        AddChild(_menu);
        AddChild(_history);
        AddChild(_guidePanel);
        divine.EventsHandled += events => _guide.Observe(events, _session.LocalGodId, _session.LocalCreature?.Id, _diplomacy.PanelVisible);
        session.WorldReplaced += () => _ended = false;
        WireMenu();
        _guidePanel.NextClicked += _guide.Next;
        _guidePanel.HideClicked += _guide.Hide;
        if (options.Guide != true)
        {
            _guide.Hide();
        }

        CallDeferred(nameof(Open), showStart, options.HistoryPanel, options.PauseMenu);
        if (options.SettingsPage)
        {
            CallDeferred(nameof(OpenSettings));
        }
    }

    /// <summary>Opens what the options ask for, once the panels are ready.</summary>
    private void Open(bool start, bool history, bool pause)
    {
        if (start)
        {
            ShowStart();
        }
        else if (pause)
        {
            Pause();
            _menu.ShowPause(!_session.IsReplica, SaveCatalog.List(SaveDirectory), Year);
        }

        _history.PanelVisible = history;
    }

    /// <summary>Opens the settings page, for captures.</summary>
    private void OpenSettings()
    {
        Pause();
        _menu.ShowSettings(Resume);
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_session is null)
        {
            return;
        }

        _guide.Observe([], _session.LocalGodId, _session.LocalCreature?.Id, _diplomacy.PanelVisible);
        _guidePanel.ShowStep(_guide.IsVisible && !_menu.IsOpen ? _guide.Text(_session.LocalGod?.Name ?? "a god", _session.LocalCreature?.Name ?? "Your Creature") : string.Empty, _guide.CanSkipStep);
        if (_history.PanelVisible)
        {
            _history.ShowEntries(HistoryReadout.Build(_session.World, _session.Content.History, _session.LocalGodId));
        }

        if (!_ended && !_menu.IsOpen && EndSummaryBuilder.Build(_session) is { } summary)
        {
            _ended = true;
            Pause();
            _menu.ShowEnd(summary, RandomSeed());
        }

        _sinceAutosave += delta;
        if (_sinceAutosave >= AutosaveSeconds && !_session.IsReplica && !_menu.IsOpen)
        {
            _sinceAutosave = 0.0;
            _setStatus(SessionCommands.QuickSave(_session, SavePath("autosave")));
        }
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session is null)
        {
            return;
        }

        if (@event.IsActionPressed(InputActions.GameMenu) && _divine.SelectedMiracle is null && !_menu.IsOpen)
        {
            Pause();
            _menu.ShowPause(!_session.IsReplica, SaveCatalog.List(SaveDirectory), Year);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.GameMenu) && _menu.IsOpen && !_ended)
        {
            Resume();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.ToggleHistory) && !_menu.IsOpen)
        {
            _history.PanelVisible = !_history.PanelVisible;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>What each menu choice does.</summary>
    private void WireMenu()
    {
        _menu.NewWorldChosen += (seed, species) =>
        {
            _setStatus(SessionCommands.NewWorld(_session, seed, species));
            Begin(guide: true);
        };
        _menu.LoadChosen += path =>
        {
            _setStatus(SessionCommands.QuickLoad(_session, path));
            Begin(guide: false);
        };
        _menu.HostChosen += (seed, name, port) =>
        {
            _setStatus(SessionCommands.NewWorld(_session, seed));
            _network.Host(_session, port, name, upnp: false);
            Begin(guide: true);
        };
        _menu.JoinChosen += (address, port, name, god) =>
            Restart([$"--join={address}:{port.ToString(CultureInfo.InvariantCulture)}", $"--name={(name.Length > 0 ? name : "Player")}", .. god is null ? Array.Empty<string>() : [$"--god={god}"]]);
        _menu.ResumeChosen += Resume;
        _menu.SaveChosen += () =>
        {
            _setStatus(SessionCommands.QuickSave(_session, SavePath(SaveName())));
            Resume();
        };
        _menu.MainMenuChosen += () =>
        {
            if (_session.IsReplica || _network.IsHosting)
            {
                Restart(["--menu=on"]);
                return;
            }

            ShowStart();
        };
        _menu.QuitChosen += () => GetTree().Quit();
    }

    /// <summary>The start screen over a paused world.</summary>
    private void ShowStart()
    {
        Pause();
        var saves = SaveCatalog.List(SaveDirectory);
        _menu.ShowStart(RandomSeed(), saves.FirstOrDefault(), saves, Year);
    }

    /// <summary>Play begins: the menu closes and the clock runs.</summary>
    private void Begin(bool guide)
    {
        _menu.Close();
        _session.Scheduler.IsPaused = false;
        _wasPaused = false;
        _ended = false;
        if (guide)
        {
            _guide.Restart();
        }
    }

    /// <summary>Stops the clock where the player may (not on a joined player's game), remembering whether it ran.</summary>
    private void Pause()
    {
        if (_session.IsReplica)
        {
            return;
        }

        _wasPaused = _session.Scheduler.IsPaused;
        _session.Scheduler.IsPaused = true;
    }

    /// <summary>Closes the menu and lets the clock run again if it ran before.</summary>
    private void Resume()
    {
        _menu.Close();
        if (!_session.IsReplica)
        {
            _session.Scheduler.IsPaused = _wasPaused;
        }
    }

    /// <summary>
    /// Starts the game over in this process with other options: the main scene is reloaded, which
    /// leaves any shared world (a host saves its world), and the next start reads these options
    /// instead of the command line.
    /// </summary>
    private void Restart(string[] userArgs)
    {
        PendingLaunch.Arguments = userArgs;
        GetTree().ReloadCurrentScene();
    }

    /// <summary>The year a tick falls in.</summary>
    private int Year(long tick) => HistoryText.Year(tick, _session.Content.History);

    /// <summary>A save's name: the god, the year and the seed.</summary>
    private string SaveName() =>
        string.Create(CultureInfo.InvariantCulture, $"{_session.LocalGod?.Name ?? "world"}-year{Year(_session.World.Tick)}-seed{_session.World.Seed.Value}");

    /// <summary>The path of a named save.</summary>
    private static string SavePath(string name) => System.IO.Path.Combine(SaveDirectory, name + ".json");

    /// <summary>A fresh seed for a new world; only the shell picks random seeds.</summary>
    private static WorldSeed RandomSeed() => new((ulong)Random.Shared.NextInt64(1, MaxRandomSeed));
}
