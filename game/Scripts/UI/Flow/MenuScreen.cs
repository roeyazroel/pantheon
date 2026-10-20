using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Flow;
using Pantheon.Persistence.Profile;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The game's menus (ADR-020, ADR-060). The title screen lies over the living island: the game's name
/// and its choices on the left, and the chosen page on the right (a new world, the player's worlds,
/// playing together, the settings). The pause menu and the end of a game are panels in the middle.
/// The screen reports choices and holds no game state.
/// </summary>
internal sealed partial class MenuScreen : CanvasLayer
{
    private Control _title = null!;
    private VBoxContainer _choices = null!;
    private PanelContainer _page = null!;
    private VBoxContainer _pageColumn = null!;
    private Label _footer = null!;
    private ColorRect _veil = null!;
    private PanelContainer _panel = null!;
    private VBoxContainer _column = null!;
    private TitleData _data = null!;

    /// <summary>A new world: its seed, how to make it and the player's name for it.</summary>
    public event Action<WorldSeed, Pantheon.Game.NewWorldOptions, string>? NewWorldChosen;

    /// <summary>The tutorial (ADR-064).</summary>
    public event Action? TutorialChosen;

    /// <summary>A save to load from the profile.</summary>
    public event Action<long>? LoadChosen;

    /// <summary>A world to forget, with its saves.</summary>
    public event Action<long>? DeleteWorldChosen;

    /// <summary>Host a new shared world: its seed, the host's name and the port.</summary>
    public event Action<WorldSeed, string, int>? HostChosen;

    /// <summary>Join a host: its address and port, the player's name and the god asked for.</summary>
    public event Action<string, int, string, string?>? JoinChosen;

    /// <summary>Back to the game.</summary>
    public event Action? ResumeChosen;

    /// <summary>Save the world.</summary>
    public event Action? SaveChosen;

    /// <summary>Back to the title screen.</summary>
    public event Action? MainMenuChosen;

    /// <summary>Quit the game.</summary>
    public event Action? QuitChosen;

    /// <summary>The player changed a setting (ADR-021).</summary>
    public event Action<Pantheon.Game.Settings.GameSettings>? SettingsChanged;

    /// <summary>The Creature species the new-world page offers, first the default, with what sets each apart.</summary>
    public IReadOnlyList<(string Name, string Description)> Species { get; set; } = [];

    /// <summary>The world sizes the new-world page offers, first the default, each in words.</summary>
    public IReadOnlyList<(string Name, string Description)> Sizes { get; set; } = [];

    /// <summary>The settings the settings page shows and changes.</summary>
    public Pantheon.Game.Settings.GameSettings Settings { get; set; } = new();

    /// <summary>Whether any menu is shown.</summary>
    public bool IsOpen => _title.Visible || _panel.Visible;

    /// <summary>Whether the title screen is shown.</summary>
    public bool TitleOpen => _title.Visible;

    /// <inheritdoc />
    public override void _Ready()
    {
        Layer = 6;
        BuildTitle();
        _veil = new ColorRect { Color = new Color(0f, 0f, 0f, 0.5f), MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        AddChild(_veil);
        _veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel = UiKit.CentredPanel(this, 480, out _column);
        _panel.Visible = false;
    }

    /// <summary>Closes whatever menu is shown.</summary>
    public void Close()
    {
        _title.Visible = false;
        _panel.Visible = false;
        _veil.Visible = false;
    }

    /// <summary>The title screen, with no page open.</summary>
    /// <param name="data">What it offers: a seed for a new world, the player's worlds, the newest save and the profile.</param>
    public void ShowTitle(TitleData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        Close();
        _title.Visible = true;
        UiKit.Clear(_choices);
        if (data.Latest is { } latest)
        {
            var resume = TitleChoice("Continue", () => LoadChosen?.Invoke(latest.Save.Id));
            resume.TooltipText = $"{latest.World.Name}: {latest.Save.Name}";
            var where = UiTheme.Label(null, UiTheme.SmallSize, UiTheme.Muted, UiTheme.Italic);
            where.Text = $"{latest.World.Name} · {latest.Save.Name}";
            _choices.AddChild(where);
        }

        TitleChoice("New World", () => OpenPage(NewWorldPage.Build(this, _data.Seed)));
        TitleChoice("Tutorial", () => TutorialChosen?.Invoke());
        if (!data.TutorialDone && data.Worlds.Count == 0)
        {
            var hint = UiTheme.Label(null, UiTheme.SmallSize, UiTheme.Gold, UiTheme.Italic);
            hint.Text = "New here? Learn to play in ten minutes.";
            _choices.AddChild(hint);
        }

        TitleChoice("Your Worlds", () => OpenPage(WorldsPage.Build(this, _data.Worlds)));
        TitleChoice("Play Together", () => OpenPage(MultiplayerPage.Build(this, _data.Seed)));
        TitleChoice("Settings", () => OpenPage(SettingsPage.Build(this)));
        TitleChoice("Quit", () => QuitChosen?.Invoke());
        _footer.Text = $"Your worlds and settings are kept on this computer ({data.ProfileKind}).";
        _page.Visible = false;
    }

    /// <summary>Opens a page of the title screen by name (new, worlds, together or settings), for captures.</summary>
    /// <param name="page">The page.</param>
    public void OpenTitlePage(string page) => OpenPage(page switch
    {
        "worlds" => WorldsPage.Build(this, _data.Worlds),
        "together" => MultiplayerPage.Build(this, _data.Seed),
        "settings" => SettingsPage.Build(this),
        _ => NewWorldPage.Build(this, _data.Seed),
    });

    /// <summary>Opens the worlds page on the title screen, after a world was forgotten.</summary>
    /// <param name="data">The title's data, with the worlds as they are now.</param>
    public void ShowWorlds(TitleData data)
    {
        ShowTitle(data);
        OpenPage(WorldsPage.Build(this, data.Worlds));
    }

    /// <summary>The pause menu.</summary>
    /// <param name="canSave">Whether this game may save and load (not a joined player's).</param>
    /// <param name="world">The world being played, if it is in the library, with its line.</param>
    /// <param name="data">The title's data, for the worlds to load.</param>
    public void ShowPause(bool canSave, string? world, TitleData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        Open();
        _column.AddChild(UiKit.Heading("Paused"));
        if (world is not null)
        {
            _column.AddChild(UiKit.Text(world, UiTheme.SmallSize, UiTheme.Muted));
        }

        _column.AddChild(UiKit.Button("Resume", () => ResumeChosen?.Invoke()));
        var save = UiKit.Button("Save the world", () => SaveChosen?.Invoke());
        save.Disabled = !canSave;
        _column.AddChild(save);
        var load = UiKit.Button("Load a world…", () => OpenInPanel(WorldsPage.Build(this, data.Worlds), () => ShowPause(canSave, world, data)));
        load.Disabled = !canSave || data.Worlds.Count == 0;
        _column.AddChild(load);
        if (!canSave)
        {
            _column.AddChild(UiKit.Text("In a shared world only the host saves and loads.", UiTheme.CaptionSize, UiTheme.Muted));
        }

        _column.AddChild(UiKit.Button("Settings…", () => OpenInPanel(SettingsPage.Build(this), () => ShowPause(canSave, world, data))));
        _column.AddChild(UiKit.Button("Save and leave to the title", () => MainMenuChosen?.Invoke()));
        _column.AddChild(UiKit.Button("Save and quit", () => QuitChosen?.Invoke()));
    }

    /// <summary>The settings on their own, for captures (<c>--settings-page</c>).</summary>
    /// <param name="back">Where Back leads.</param>
    public void ShowSettings(Action back) => OpenInPanel(SettingsPage.Build(this), back);

    /// <summary>The end of a game: who won, and what the world remembers.</summary>
    /// <param name="summary">The summary.</param>
    /// <param name="seed">A seed for a new world.</param>
    public void ShowEnd(EndSummary summary, WorldSeed seed)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Open(width: 640);
        var title = UiKit.Heading(summary.Title, 40);
        title.AddThemeColorOverride("font_color", summary.Won ? UiTheme.Good : UiTheme.Bad);
        _column.AddChild(title);
        _column.AddChild(UiKit.Text(summary.Subtitle, UiTheme.BodySize, UiTheme.Ink));
        foreach (var fact in summary.Facts)
        {
            _column.AddChild(UiKit.Text(fact, UiTheme.SmallSize, UiTheme.Muted));
        }

        if (summary.Highlights.Count > 0)
        {
            _column.AddChild(UiKit.Text("From the world's history", UiTheme.BodySize, UiTheme.Gold));
            foreach (var line in summary.Highlights)
            {
                _column.AddChild(UiKit.Text(line, UiTheme.SmallSize, UiTheme.Ink));
            }
        }

        _column.AddChild(UiKit.Button("Keep watching", () => ResumeChosen?.Invoke()));
        _column.AddChild(UiKit.Button("New world", () => NewWorldChosen?.Invoke(seed, new Pantheon.Game.NewWorldOptions(), string.Empty)));
        _column.AddChild(UiKit.Button("Leave to the title", () => MainMenuChosen?.Invoke()));
    }

    /// <summary>Reports a new world (for the pages).</summary>
    internal void ChooseNewWorld(WorldSeed seed, Pantheon.Game.NewWorldOptions options, string name) => NewWorldChosen?.Invoke(seed, options, name);

    /// <summary>Reports a save to load (for the pages).</summary>
    internal void ChooseLoad(long saveId) => LoadChosen?.Invoke(saveId);

    /// <summary>Reports a world to forget (for the pages).</summary>
    internal void ChooseDelete(long worldId) => DeleteWorldChosen?.Invoke(worldId);

    /// <summary>Reports hosting (for the pages).</summary>
    internal void ChooseHost(WorldSeed seed, string name, int port) => HostChosen?.Invoke(seed, name, port);

    /// <summary>Reports joining (for the pages).</summary>
    internal void ChooseJoin(string address, int port, string name, string? god) => JoinChosen?.Invoke(address, port, name, god);

    /// <summary>Keeps a changed setting and reports it (for the settings page).</summary>
    internal void ChangeSettings(Pantheon.Game.Settings.GameSettings settings)
    {
        Settings = settings.Clamped();
        SettingsChanged?.Invoke(Settings);
    }

    /// <summary>The title screen's frame: a shade over the island's left, the name and choices, the page on the right and a footer.</summary>
    private void BuildTitle()
    {
        _title = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        AddChild(_title);
        _title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient { Colors = [new Color(0.02f, 0.02f, 0.04f, 0.9f), new Color(0.02f, 0.02f, 0.04f, 0.55f), new Color(0f, 0f, 0f, 0f)], Offsets = [0f, 0.45f, 1f] },
                Width = 256,
                Height = 4,
            },
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _title.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
        shade.OffsetRight = 900f;

        var left = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        left.AddThemeConstantOverride("separation", 6);
        _title.AddChild(left);
        left.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterLeft, Control.LayoutPresetMode.Minsize, 90);
        left.GrowVertical = Control.GrowDirection.Both;
        var name = UiTheme.Label(left, 92, UiTheme.Gold, UiTheme.Title);
        name.Text = "PANTHEON";
        name.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.6f));
        name.AddThemeConstantOverride("shadow_offset_y", 4);
        var subtitle = UiTheme.Label(left, 24, UiTheme.Ink, UiTheme.Italic);
        subtitle.Text = "Be worshipped. Raise a Creature. Win the world.";
        left.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 36f), MouseFilter = Control.MouseFilterEnum.Ignore });
        _choices = new VBoxContainer();
        _choices.AddThemeConstantOverride("separation", 4);
        left.AddChild(_choices);

        _page = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(640f, 0f) };
        _page.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(0.05f, 0.06f, 0.09f, 0.94f), UiTheme.Edge, 1, 14, margin: 26));
        _title.AddChild(_page);
        _page.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterRight, Control.LayoutPresetMode.Minsize, 90);
        _page.GrowHorizontal = Control.GrowDirection.Begin;
        _page.GrowVertical = Control.GrowDirection.Both;
        _pageColumn = new VBoxContainer();
        _pageColumn.AddThemeConstantOverride("separation", 10);
        _page.AddChild(_pageColumn);

        _footer = UiTheme.Label(_title, UiTheme.CaptionSize, UiTheme.Muted);
        _footer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 24);
        _footer.GrowVertical = Control.GrowDirection.Begin;
    }

    /// <summary>A large choice on the title screen: Cinzel, gold when hovered.</summary>
    private Button TitleChoice(string text, Action pressed)
    {
        var button = new Button { Text = text, Flat = true, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeFontOverride("font", UiTheme.Title);
        button.AddThemeFontSizeOverride("font_size", 34);
        button.AddThemeColorOverride("font_color", UiTheme.Ink);
        button.AddThemeColorOverride("font_hover_color", UiTheme.Gold);
        button.AddThemeColorOverride("font_pressed_color", UiTheme.Gold);
        button.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.7f));
        button.AddThemeConstantOverride("outline_size", 6);
        button.Pressed += pressed;
        _choices.AddChild(button);
        return button;
    }

    /// <summary>Shows a page on the title screen's right.</summary>
    private void OpenPage(Control page)
    {
        UiKit.Clear(_pageColumn);
        _pageColumn.AddChild(page);
        _page.Visible = true;
    }

    /// <summary>Shows a page in the middle panel, with a way back.</summary>
    private void OpenInPanel(Control page, Action back)
    {
        Open(width: 640);
        _column.AddChild(page);
        _column.AddChild(UiKit.Button("Back", back));
    }

    /// <summary>Shows the middle panel with an empty column.</summary>
    private void Open(float width = 480)
    {
        _title.Visible = false;
        UiKit.Clear(_column);
        _panel.CustomMinimumSize = new Vector2(width, 0);
        _panel.Visible = true;
        _veil.Visible = true;
    }
}

/// <summary>What the title screen offers (ADR-060).</summary>
/// <param name="Seed">A seed for a new world.</param>
/// <param name="Worlds">The player's worlds, the most recently played first.</param>
/// <param name="Latest">The newest save of any world, to continue from.</param>
/// <param name="ProfileKind">What keeps the profile, in words.</param>
/// <param name="TutorialDone">Whether the player has finished the tutorial (ADR-064).</param>
internal sealed record TitleData(WorldSeed Seed, IReadOnlyList<WorldCard> Worlds, (WorldRecord World, SaveRecord Save)? Latest, string ProfileKind, bool TutorialDone = false);
