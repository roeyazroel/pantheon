using System;
using System.Globalization;
using System.Linq;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.Network;
using Pantheon.Client.UI;
using Pantheon.Client.UI.Flow;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Flow;
using Pantheon.Game.History;
using Pantheon.Persistence.Profile;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The game as a game (ADR-020, ADR-060): the title screen over the living island, the pause menu
/// (Esc), the player's worlds and their saves in the profile, the autosave, quicksave and quickload,
/// hosting and joining, the history panel (H), the first-minutes guide, and the end of a game.
/// Joining another host starts the game over in place with its address, since a joined world
/// replaces the whole session.
/// </summary>
internal sealed partial class GameFlow : Node
{
    // Real seconds between autosaves of a world in the library.
    private const double AutosaveSeconds = 300.0;

    // How fast the title screen's camera circles the island, in radians a second, and how much lower it looks.
    private const float TitleOrbit = 0.035f;
    private const float TitleTilt = -0.35f;

    // F2 seeds stay short enough to read and type.
    private const long MaxRandomSeed = 1_000_000_000;

    private readonly Guide _guide = new();
    private GameSession _session = null!;
    private NetworkDriver _network = null!;
    private DivineController _divine = null!;
    private DiplomacyPanel _diplomacy = null!;
    private Hud _hud = null!;
    private GodCameraController _camera = null!;
    private Action<string> _setStatus = null!;
    private WorldLibrary _library = null!;
    private MenuScreen _menu = null!;
    private HistoryPanel _history = null!;
    private TutorialController _tutorial = null!;
    private GuidePanel _guidePanel = null!;
    private Births _births = null!;
    private bool _wasPaused;
    private bool _ended;
    private double _sinceAutosave;

    /// <summary>Where saves lay before the profile; they are brought into it once (ADR-060).</summary>
    public static string SaveDirectory => ProjectSettings.GlobalizePath("user://saves");

    /// <summary>Connects the flow to the game and shows the title screen or the guide as asked.</summary>
    /// <param name="session">The session.</param>
    /// <param name="network">The network driver.</param>
    /// <param name="divine">The player's divine interface, for its events and selection.</param>
    /// <param name="ui">The diplomacy panel (for the guide) and the HUD (hidden on the title screen).</param>
    /// <param name="camera">The camera, which circles the island on the title screen.</param>
    /// <param name="setStatus">Shows a status line.</param>
    /// <param name="options">Launch options: the title screen (by default when there are no options), the guide, and the history panel or pause menu open from the start.</param>
    /// <param name="choices">The player's settings, for the settings page, and the gods' minds (ADR-065), for new worlds' gods.</param>
    public void Setup(GameSession session, NetworkDriver network, DivineController divine, (DiplomacyPanel Diplomacy, Hud Hud) ui, GodCameraController camera, Action<string> setStatus, Game.Launch.LaunchOptions options, (Game.Settings.GameSettings Settings, Game.Minds.MindContent? Minds) choices)
    {
        var settings = choices.Settings;
        ArgumentNullException.ThrowIfNull(options);
        var showTitle = options.Menu ?? (options.Seed is null && options.LoadPath is null && options.Join is null && options.HostPort is null && options.QuitAfterFrames is null);
        (_session, _network, _divine, _diplomacy, _hud, _camera, _setStatus) = (session, network, divine, ui.Diplomacy, ui.Hud, camera, setStatus);
        _library = new WorldLibrary(Startup.Profile, session.Content);
        if (_library.ImportFiles(SaveDirectory, DateTime.UtcNow) is var imported and > 0)
        {
            setStatus($"Brought {imported} saved worlds into your profile");
        }

        _menu = new MenuScreen { Name = "Menus" };
        _menu.Species = session.Content.Creatures.Species.Select(s => (s.Name, s.Description)).ToList();
        _menu.Sizes = session.Content.Sizes.Sizes.Select(s => (s.Name, WorldMaker.Describe(s))).ToList();
        _menu.Settings = settings;
        _menu.Minds = choices.Minds;
        _births = new Births(session.Content, choices.Minds, _menu, () => _menu.Settings, options.Minds);
        var talk = new TalkController { Name = "Talk" };
        AddChild(talk);
        talk.Setup(session, _births, ui.Diplomacy, setStatus, options);
        _menu.SettingsChanged += changed =>
        {
            SettingsApplier.Apply(changed, GetViewport(), GetParent().GetNodeOrNull<DirectionalLight3D>("Sun"), startup: false, GetParent().GetNodeOrNull<WorldEnvironment>("WorldEnvironment")?.Environment, _session, _camera);
            Startup.SaveSettings(changed);
        };
        _history = new HistoryPanel { Name = "History" };
        _guidePanel = new GuidePanel { Name = "Guide" };
        AddChild(_menu);
        AddChild(_history);
        AddChild(_guidePanel);
        _tutorial = new TutorialController { Name = "Tutorial" };
        AddChild(_tutorial);
        _tutorial.Setup(session, camera, divine, ui, GetParent().GetNode<Node3D>("World"), setStatus);
        divine.EventsHandled += events => _guide.Observe(events, _session.LocalGodId, _session.LocalCreature?.Id, _diplomacy.PanelVisible);
        session.WorldReplaced += () => _ended = false;
        WireMenu();
        _guidePanel.NextClicked += _guide.Next;
        _guidePanel.HideClicked += _guide.Hide;
        if (options.Guide != true)
        {
            _guide.Hide();
        }

        CallDeferred(nameof(Open), showTitle, options.HistoryPanel, options.PauseMenu, options.TitlePage ?? string.Empty);
        if (options.Tutorial is { } step)
        {
            CallDeferred(nameof(StartTutorial), step);
        }
        if (options.SettingsPage)
        {
            CallDeferred(nameof(OpenSettings));
        }

        if (options.PantheonPage)
        {
            // The world's gods are born anew, by the minds chosen, as a new world's are (ADR-065).
            var seed = options.Seed ?? Game.Launch.LaunchOptions.DefaultSeed;
            var world = options.NewWorld(session.Content);
            Callable.From(ShowTitle).CallDeferred();
            Callable.From(() => _births.Bear(seed, world, (born, note) =>
            {
                _setStatus(SessionCommands.NewWorld(_session, seed, born));
                GD.Print($"Pantheon born by {born.Born?.Author ?? "the content"}: {string.Join(", ", _session.World.Gods.Select(Game.Minds.PersonaText.Title))}{(note is null ? string.Empty : "; " + note)}");
                ShowGods(note ?? string.Empty);
            })).CallDeferred();
        }

        if (options.MindCheck)
        {
            _births.Check(text =>
            {
                GD.Print(text);
                _setStatus(text);
            });
        }
    }

    /// <summary>The gods of the world (ADR-065) on the title screen, and play begins when the player enters.</summary>
    /// <param name="note">Why the scribes wrote some of them, if they did.</param>
    private void ShowGods(string note)
    {
        if (!_menu.TitleOpen)
        {
            ShowTitle();
        }

        _menu.ShowPantheon(_session.World, _session.LocalGodId, note.Length > 0 ? note : null, () => Begin(guide: true));
    }

    /// <summary>Saves the world to its quicksave slot (F5).</summary>
    public void QuickSave() => _setStatus(_library.Save(_session, SaveKind.Quick, DateTime.UtcNow));

    /// <summary>Loads the world's newest quicksave (F9).</summary>
    public void QuickLoad() =>
        _setStatus(_library.LatestQuick() is { } save ? _library.Load(_session, save.Id) : "No quicksave of this world yet (F5 makes one)");

    /// <summary>A new world from a random seed (F2), started in the library.</summary>
    public void NewRandomWorld()
    {
        var seed = RandomSeed();
        _setStatus(SessionCommands.NewWorld(_session, seed, _births.Scribed(seed, new NewWorldOptions())));
        _library.Begin(_session, null, DateTime.UtcNow);
    }

    /// <summary>Opens what the options ask for, once the panels are ready.</summary>
    private void Open(bool title, bool history, bool pause, string page)
    {
        if (title)
        {
            ShowTitle();
            if (page.Length > 0)
            {
                _menu.OpenTitlePage(page);
            }
        }
        else if (pause)
        {
            ShowPause();
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

        _hud.Visible = !_menu.TitleOpen;

        // The island's labels would show through the title's shade; the view is lower there, as a picture.
        _camera.ViewCamera.SetCullMaskValue(2, !_menu.TitleOpen);
        if (_menu.TitleOpen)
        {
            _camera.Model.Rotate(TitleOrbit * (float)delta);
            return;
        }

        _guide.Observe([], _session.LocalGodId, _session.LocalCreature?.Id, _diplomacy.PanelVisible);
        _guidePanel.ShowStep(_guide.IsVisible && !_menu.IsOpen && !_tutorial.IsActive ? _guide.Text(_session.LocalGod?.Name ?? "a god", _session.LocalCreature?.Name ?? "Your Creature") : string.Empty, _guide.CanSkipStep);
        if (_history.PanelVisible)
        {
            _history.ShowEntries(HistoryReadout.Build(_session.World, _session.Content.History, _session.LocalGodId));
        }

        if (!_ended && !_menu.IsOpen && EndSummaryBuilder.Build(_session) is { } summary)
        {
            _ended = true;
            _library.End(summary.Won);
            Pause();
            _menu.ShowEnd(summary, RandomSeed());
        }

        if (!_menu.IsOpen && !_session.Scheduler.IsPaused)
        {
            _library.AddPlayTime(delta);
        }

        _sinceAutosave += delta;
        if (_sinceAutosave >= AutosaveSeconds && !_session.IsReplica && !_menu.IsOpen && _library.Current is not null)
        {
            _sinceAutosave = 0.0;
            _setStatus(_library.Save(_session, SaveKind.Auto, DateTime.UtcNow));
        }
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session is null || _menu.TitleOpen)
        {
            return;
        }

        if (@event.IsActionPressed(InputActions.GameMenu) && _divine.SelectedMiracle is null && !_menu.IsOpen)
        {
            ShowPause();
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
        _menu.NewWorldChosen += (seed, world, name) => _births.Bear(seed, world, (born, note) =>
        {
            _setStatus(SessionCommands.NewWorld(_session, seed, born));
            var record = _library.Begin(_session, name, DateTime.UtcNow);
            _setStatus(note ?? $"{record.Name}: your world begins");
            ShowGods(note ?? string.Empty);
        });
        _menu.TutorialChosen += () => StartTutorial(1);
        _menu.LoadChosen += saveId =>
        {
            _setStatus(_library.Load(_session, saveId));
            Begin(guide: false);
        };
        _menu.DeleteWorldChosen += worldId =>
        {
            _library.Delete(worldId);
            _menu.ShowWorlds(TitleData());
        };
        _menu.HostChosen += (seed, name, port) => _births.Bear(seed, new NewWorldOptions(), (born, note) =>
        {
            _setStatus(SessionCommands.NewWorld(_session, seed, born));
            _library.Begin(_session, "A shared world", DateTime.UtcNow);
            _network.Host(_session, port, name, upnp: false);
            _setStatus(note ?? "Your shared world begins");
            ShowGods(note ?? string.Empty);
        });
        _menu.JoinChosen += (address, port, name, god) =>
            Restart([$"--join={address}:{port.ToString(CultureInfo.InvariantCulture)}", $"--name={(name.Length > 0 ? name : "Player")}", .. god is null ? Array.Empty<string>() : [$"--god={god}"]]);
        _menu.ResumeChosen += Resume;
        _menu.SaveChosen += () =>
        {
            _setStatus(_library.Save(_session, SaveKind.Manual, DateTime.UtcNow));
            Resume();
        };
        _menu.MainMenuChosen += () =>
        {
            SaveBeforeLeaving();
            if (_session.IsReplica || _network.IsHosting)
            {
                Restart(["--menu=on"]);
                return;
            }

            _library.Leave();
            ShowTitle();
        };
        _menu.QuitChosen += () =>
        {
            SaveBeforeLeaving();
            GetTree().Quit();
        };
    }

    /// <summary>The title screen: the island lives on behind it, and the camera circles it.</summary>
    private void ShowTitle()
    {
        _menu.ShowTitle(TitleData());
        _camera.StopFollowing();
        _camera.Model.TiltBy(TitleTilt - _camera.Model.TargetTilt);
        if (!_session.IsReplica)
        {
            _session.Scheduler.IsPaused = false;
        }
    }

    /// <summary>The pause menu, with the world's line when it is in the library.</summary>
    private void ShowPause()
    {
        Pause();
        var world = _library.Current is { } current ? $"{current.Name} · {current.God}" : null;
        _menu.ShowPause(!_session.IsReplica, world, TitleData());
    }

    /// <summary>What the title screen offers now.</summary>
    private TitleData TitleData() => new(RandomSeed(), _library.Cards(), _library.Latest(), _library.Store.Kind, _library.Store.ReadValue(TutorialController.DoneKey) is not null);

    /// <summary>
    /// The tutorial (ADR-064) on its own island: the first small world, won by nobody so there is no
    /// hurry, not kept among the player's worlds.
    /// </summary>
    /// <param name="step">The step to start at: 1 for the beginning.</param>
    private void StartTutorial(int step)
    {
        if (_session.IsReplica)
        {
            return;
        }

        var seed = Game.Launch.LaunchOptions.DefaultSeed;
        _setStatus(SessionCommands.NewWorld(_session, seed, _births.Scribed(seed, new NewWorldOptions(Victory: Domain.World.VictorySettings.Sandbox))));
        _library.Leave();
        Begin(guide: false);
        _guide.Hide();
        _tutorial.Start((Game.Flow.TutorialStep)(step - 1));
    }

    /// <summary>An autosave of a world in the library before the player leaves it.</summary>
    private void SaveBeforeLeaving()
    {
        if (_library.Current is not null && !_session.IsReplica && !_menu.TitleOpen)
        {
            _setStatus(_library.Save(_session, SaveKind.Auto, DateTime.UtcNow));
        }
    }

    /// <summary>Play begins: the menu closes and the clock runs.</summary>
    private void Begin(bool guide)
    {
        _menu.Close();
        _camera.Model.TiltBy(-_camera.Model.TargetTilt);
        _session.Scheduler.IsPaused = false;
        _wasPaused = false;
        _ended = false;
        _sinceAutosave = 0.0;
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

    /// <summary>A fresh seed for a new world; only the shell picks random seeds.</summary>
    private static WorldSeed RandomSeed() => new((ulong)Random.Shared.NextInt64(1, MaxRandomSeed));
}
