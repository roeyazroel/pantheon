using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Flow;
using Pantheon.Networking;
using Pantheon.Persistence;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The game's menus (ADR-020): the start screen, the pause menu, the list of saves, hosting and
/// joining a shared world, and the end of a game. Each page is built when shown; the screen only
/// reports choices and holds no game state.
/// </summary>
internal sealed partial class MenuScreen : CanvasLayer
{
    private PanelContainer _panel = null!;
    private VBoxContainer _column = null!;
    private ColorRect _veil = null!;

    /// <summary>A new world with a seed, and the species of the player's Creature (ADR-021); <c>null</c> keeps the pantheon's.</summary>
    public event Action<WorldSeed, string?>? NewWorldChosen;

    /// <summary>The Creature species the new-world page offers, first the default, with what sets each apart.</summary>
    public IReadOnlyList<(string Name, string Description)> Species { get; set; } = [];

    /// <summary>A save to load.</summary>
    public event Action<string>? LoadChosen;

    /// <summary>Host a new shared world: its seed, the host's name and the port.</summary>
    public event Action<WorldSeed, string, int>? HostChosen;

    /// <summary>Join a host: its address and port, the player's name and the god asked for.</summary>
    public event Action<string, int, string, string?>? JoinChosen;

    /// <summary>Back to the game.</summary>
    public event Action? ResumeChosen;

    /// <summary>Save the world.</summary>
    public event Action? SaveChosen;

    /// <summary>Back to the start screen.</summary>
    public event Action? MainMenuChosen;

    /// <summary>Quit the game.</summary>
    public event Action? QuitChosen;

    /// <summary>The player changed a setting (ADR-021).</summary>
    public event Action<Pantheon.Game.Settings.GameSettings>? SettingsChanged;

    /// <summary>The settings the settings page shows and changes.</summary>
    public Pantheon.Game.Settings.GameSettings Settings { get; set; } = new();

    /// <summary>Whether a page is shown.</summary>
    public bool IsOpen => _panel.Visible;

    /// <inheritdoc />
    public override void _Ready()
    {
        Layer = 6;
        _veil = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f), MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        AddChild(_veil);
        _veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel = UiKit.CentredPanel(this, 460, out _column);
        _panel.Visible = false;
    }

    /// <summary>Closes whatever page is shown.</summary>
    public void Close()
    {
        _panel.Visible = false;
        _veil.Visible = false;
    }

    /// <summary>The start screen.</summary>
    /// <param name="seed">The seed offered for a new world.</param>
    /// <param name="latest">The newest save, offered to continue, if any.</param>
    /// <param name="saves">Every save, for the load page.</param>
    /// <param name="year">The year a tick falls in.</param>
    public void ShowStart(WorldSeed seed, SaveSummary? latest, IReadOnlyList<SaveSummary> saves, Func<long, int> year)
    {
        Open();
        _column.AddChild(UiKit.Text("PANTHEON", 34, UiKit.Gold));
        _column.AddChild(UiKit.Text("Win the belief of the world's people. Raise your Creature, deal with three rival gods, and hold 60% of all belief for ten minutes.", 15, UiKit.Muted));
        var seedField = UiKit.Field(seed.Value.ToString(CultureInfo.InvariantCulture), "seed");
        _column.AddChild(UiKit.Row("World seed", seedField));
        var species = SpeciesChoice(out var about);
        _column.AddChild(UiKit.Row("Your Creature", species));
        _column.AddChild(about);
        _column.AddChild(UiKit.Button("New world", () => NewWorldChosen?.Invoke(ParseSeed(seedField.Text, seed), Chosen(species))));
        if (latest is not null)
        {
            _column.AddChild(UiKit.Button($"Continue: {Describe(latest, year)}", () => LoadChosen?.Invoke(latest.Path)));
        }

        if (saves.Count > 0)
        {
            _column.AddChild(UiKit.Button("Load a world...", () => ShowLoad(saves, year, () => ShowStart(seed, latest, saves, year))));
        }

        _column.AddChild(UiKit.Button("Host a shared world...", () => ShowHost(seed, () => ShowStart(seed, latest, saves, year))));
        _column.AddChild(UiKit.Button("Join a shared world...", () => ShowJoin(() => ShowStart(seed, latest, saves, year))));
        _column.AddChild(UiKit.Button("Settings...", () => ShowSettings(() => ShowStart(seed, latest, saves, year))));
        _column.AddChild(UiKit.Button("Quit", () => QuitChosen?.Invoke()));
    }

    /// <summary>The pause menu.</summary>
    /// <param name="canSave">Whether this game may save and load (not a joined player's).</param>
    /// <param name="saves">Every save, for the load page.</param>
    /// <param name="year">The year a tick falls in.</param>
    public void ShowPause(bool canSave, IReadOnlyList<SaveSummary> saves, Func<long, int> year)
    {
        Open();
        _column.AddChild(UiKit.Text("Paused", 26, UiKit.Gold));
        _column.AddChild(UiKit.Button("Resume", () => ResumeChosen?.Invoke()));
        var save = UiKit.Button("Save the world", () => SaveChosen?.Invoke());
        save.Disabled = !canSave;
        _column.AddChild(save);
        var load = UiKit.Button("Load a world...", () => ShowLoad(saves, year, () => ShowPause(canSave, saves, year)));
        load.Disabled = !canSave || saves.Count == 0;
        _column.AddChild(load);
        if (!canSave)
        {
            _column.AddChild(UiKit.Text("In a shared world only the host saves and loads.", 14, UiKit.Muted));
        }

        _column.AddChild(UiKit.Button("Settings...", () => ShowSettings(() => ShowPause(canSave, saves, year))));
        _column.AddChild(UiKit.Button("Main menu", () => MainMenuChosen?.Invoke()));
        _column.AddChild(UiKit.Button("Quit", () => QuitChosen?.Invoke()));
    }

    /// <summary>
    /// The settings (spec §105, ADR-021): the window, frame pacing, shadows, render scale, citizens
    /// shown and loudness. Each change applies at once and is kept for the next run.
    /// </summary>
    /// <param name="back">Where Back leads.</param>
    public void ShowSettings(Action back)
    {
        Open(width: 520);
        _column.AddChild(UiKit.Text("Settings", 24, UiKit.Gold));
        Toggle("Fullscreen", Settings.Fullscreen, on => Settings with { Fullscreen = on });
        Toggle("Vertical sync", Settings.VSync, on => Settings with { VSync = on });
        Toggle("Shadows", Settings.Shadows, on => Settings with { Shadows = on });
        Slide("Render scale", Settings.RenderScale, Pantheon.Game.Settings.GameSettings.MinRenderScale, value => Settings with { RenderScale = value });
        Slide("Citizens shown", Settings.Citizens, Pantheon.Game.Settings.GameSettings.MinCitizens, value => Settings with { Citizens = value });
        _column.AddChild(UiKit.Text("Fewer citizens take effect when the game next starts.", 13, UiKit.Muted));
        Slide("Volume", Settings.MasterVolume, 0f, value => Settings with { MasterVolume = value });
        Slide("Music", Settings.MusicVolume, 0f, value => Settings with { MusicVolume = value });
        Slide("Effects", Settings.EffectsVolume, 0f, value => Settings with { EffectsVolume = value });
        Slide("Wind and rain", Settings.AmbienceVolume, 0f, value => Settings with { AmbienceVolume = value });
        _column.AddChild(UiKit.Button("Back", back));
    }

    /// <summary>A check box that changes a setting.</summary>
    private void Toggle(string caption, bool value, Func<bool, Pantheon.Game.Settings.GameSettings> change)
    {
        var box = new CheckBox { ButtonPressed = value };
        box.Toggled += on => Change(change(on));
        _column.AddChild(UiKit.Row(caption, box));
    }

    /// <summary>A slider from a least value to 1 that changes a setting.</summary>
    private void Slide(string caption, float value, float least, Func<float, Pantheon.Game.Settings.GameSettings> change)
    {
        var slider = new HSlider { MinValue = least, MaxValue = 1.0, Step = 0.05, Value = value, CustomMinimumSize = new Vector2(280, 28) };
        slider.ValueChanged += changed => Change(change((float)changed));
        _column.AddChild(UiKit.Row(caption, slider));
    }

    /// <summary>Keeps a changed setting and reports it.</summary>
    private void Change(Pantheon.Game.Settings.GameSettings settings)
    {
        Settings = settings.Clamped();
        SettingsChanged?.Invoke(Settings);
    }

    /// <summary>The end of a game: who won, and what the world remembers.</summary>
    /// <param name="summary">The summary.</param>
    /// <param name="seed">A seed for a new world.</param>
    public void ShowEnd(EndSummary summary, WorldSeed seed)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Open(width: 620);
        _column.AddChild(UiKit.Text(summary.Title, 34, summary.Won ? UiKit.Good : UiKit.Bad));
        _column.AddChild(UiKit.Text(summary.Subtitle, 18, UiKit.Ink));
        foreach (var fact in summary.Facts)
        {
            _column.AddChild(UiKit.Text(fact, 15, UiKit.Muted));
        }

        if (summary.Highlights.Count > 0)
        {
            _column.AddChild(UiKit.Text("From the world's history", 16, UiKit.Gold));
            foreach (var line in summary.Highlights)
            {
                _column.AddChild(UiKit.Text(line, 14, UiKit.Ink));
            }
        }

        _column.AddChild(UiKit.Button("Keep watching", () => ResumeChosen?.Invoke()));
        _column.AddChild(UiKit.Button("New world", () => NewWorldChosen?.Invoke(seed, null)));
        _column.AddChild(UiKit.Button("Main menu", () => MainMenuChosen?.Invoke()));
    }

    /// <summary>The saves to load from.</summary>
    private void ShowLoad(IReadOnlyList<SaveSummary> saves, Func<long, int> year, Action back)
    {
        Open();
        _column.AddChild(UiKit.Text("Load a world", 24, UiKit.Gold));
        foreach (var save in saves)
        {
            _column.AddChild(UiKit.Button(Describe(save, year), () => LoadChosen?.Invoke(save.Path)));
        }

        _column.AddChild(UiKit.Button("Back", back));
    }

    /// <summary>Hosting a new shared world.</summary>
    private void ShowHost(WorldSeed seed, Action back)
    {
        Open();
        _column.AddChild(UiKit.Text("Host a shared world", 24, UiKit.Gold));
        _column.AddChild(UiKit.Text("Up to three friends can join and each play one of the rival gods. They need your address and the port.", 14, UiKit.Muted));
        var name = UiKit.Field("Host", "your name");
        var port = UiKit.Field(NetworkProtocol.DefaultPort.ToString(CultureInfo.InvariantCulture), "port");
        var seedField = UiKit.Field(seed.Value.ToString(CultureInfo.InvariantCulture), "seed");
        _column.AddChild(UiKit.Row("Your name", name));
        _column.AddChild(UiKit.Row("Port", port));
        _column.AddChild(UiKit.Row("World seed", seedField));
        _column.AddChild(UiKit.Button("Host", () => HostChosen?.Invoke(ParseSeed(seedField.Text, seed), name.Text.Trim(), ParsePort(port.Text))));
        _column.AddChild(UiKit.Button("Back", back));
    }

    /// <summary>Joining a host.</summary>
    private void ShowJoin(Action back)
    {
        Open();
        _column.AddChild(UiKit.Text("Join a shared world", 24, UiKit.Gold));
        var address = UiKit.Field("127.0.0.1", "host address");
        var port = UiKit.Field(NetworkProtocol.DefaultPort.ToString(CultureInfo.InvariantCulture), "port");
        var name = UiKit.Field("Player", "your name");
        var god = UiKit.Field(string.Empty, "any");
        _column.AddChild(UiKit.Row("Host address", address));
        _column.AddChild(UiKit.Row("Port", port));
        _column.AddChild(UiKit.Row("Your name", name));
        _column.AddChild(UiKit.Row("God (optional)", god));
        _column.AddChild(UiKit.Button("Join", () => JoinChosen?.Invoke(address.Text.Trim(), ParsePort(port.Text), name.Text.Trim(), god.Text.Trim().Length > 0 ? god.Text.Trim() : null)));
        _column.AddChild(UiKit.Button("Back", back));
    }

    /// <summary>A choice of the player's Creature species, with a line saying what sets the chosen one apart.</summary>
    private OptionButton SpeciesChoice(out Label about)
    {
        var choice = new OptionButton { CustomMinimumSize = new Vector2(260, 32) };
        var description = UiKit.Text(string.Empty, 13, UiKit.Muted);
        foreach (var (name, _) in Species)
        {
            choice.AddItem(name);
        }

        void Describe(long index) => description.Text = index >= 0 && index < Species.Count ? Species[(int)index].Description : string.Empty;
        choice.ItemSelected += Describe;
        Describe(0);
        about = description;
        return choice;
    }

    /// <summary>The species chosen, or <c>null</c> when there is no choice.</summary>
    private string? Chosen(OptionButton choice) => choice.Selected >= 0 && choice.Selected < Species.Count ? Species[choice.Selected].Name : null;

    /// <summary>Shows the panel with an empty column.</summary>
    private void Open(float width = 460)
    {
        UiKit.Clear(_column);
        _panel.CustomMinimumSize = new Vector2(width, 0);
        _panel.Visible = true;
        _veil.Visible = true;
    }

    /// <summary>A save as a line: its name, seed, year and when it was written.</summary>
    private static string Describe(SaveSummary save, Func<long, int> year) =>
        string.Create(CultureInfo.InvariantCulture, $"{save.Name}: seed {save.Seed.Value}, year {year(save.Tick)}, {save.WrittenUtc.ToLocalTime():yyyy-MM-dd HH:mm}");

    /// <summary>A seed typed by the player, or the offered one.</summary>
    private static WorldSeed ParseSeed(string text, WorldSeed fallback) =>
        ulong.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? new WorldSeed(value) : fallback;

    /// <summary>A port typed by the player, or the default.</summary>
    private static int ParsePort(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535 ? port : NetworkProtocol.DefaultPort;
}
