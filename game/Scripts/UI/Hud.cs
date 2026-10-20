using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game.Alerts;
using Pantheon.Game.Hud;

namespace Pantheon.Client.UI;

/// <summary>
/// The player HUD (spec §57, ADR-059). At the top left the god's plaque and its rivals; at the top
/// centre the race for victory, the weather and the domains; at the top right the season, the
/// speed and the alerts (spec §59); at the bottom the miracle bar under the target hint and a
/// status toast, and the Creature's card at the bottom left. It shows what
/// <see cref="HudReadout"/> builds and reports clicks; it holds no game state.
/// </summary>
public partial class Hud : CanvasLayer
{
    private const int Margin = 14;

    // The miracle bar keeps clear of the Creature's card on its left.
    private const float BarLeft = 372f;

    private readonly List<MiracleTile> _tiles = [];
    private readonly List<string> _tileIds = [];
    private GodPlaque _plaque = null!;
    private TimeControls _time = null!;
    private AlertList _alerts = null!;
    private CreaturePanel _creature = null!;
    private StatusToast _status = null!;
    private PanelContainer _contestPanel = null!;
    private Label _contest = null!;
    private Meter _contestMeter = null!;
    private Label _weather = null!;
    private Label _domains = null!;
    private Label _overlayLegend = null!;
    private PanelContainer _hintPanel = null!;
    private Label _hint = null!;
    private HBoxContainer _bar = null!;

    /// <summary>The player clicked a miracle; the id is the miracle, or <c>null</c> to put it down.</summary>
    public event Action<string?>? MiracleClicked;

    /// <summary>The player clicked an alert that has a place.</summary>
    public event Action<GroundPosition>? AlertClicked;

    /// <summary>The player clicked Reward or Punish on the Creature card.</summary>
    public event Action<CreatureCommandKind>? CreatureJudged;

    /// <summary>The player clicked Follow on the Creature card.</summary>
    public event Action? CreatureLookClicked;

    /// <summary>The player clicked Send on the Creature card: the next click on the ground sends it there.</summary>
    public event Action? CreatureSendClicked;

    /// <summary>The player clicked Fight on the Creature card: the next click near a rival sets it on that rival (ADR-062).</summary>
    public event Action? CreatureFightClicked;

    /// <summary>The player clicked pause.</summary>
    public event Action? PauseClicked;

    /// <summary>The player clicked a speed: its index among <see cref="Pantheon.Game.SessionCommands.SpeedPresets"/>.</summary>
    public event Action<int>? SpeedClicked;

    /// <summary>A miracle's glyph and colour (set before the HUD enters the tree).</summary>
    internal Func<string, (MiracleGlyph Glyph, Color Tint)> MiracleLook { get; set; } = static _ => (MiracleGlyph.Sparkle, UiTheme.Gold);

    /// <summary>A miracle's whole cooldown in seconds, for the tiles' sweep.</summary>
    internal Func<string, double> CooldownOf { get; set; } = static _ => 0;

    /// <summary>A god's colour by its id.</summary>
    internal Func<int, Color> GodColor { get; set; } = static _ => UiTheme.Gold;

    /// <summary>The player's god, for its colour on the plaque.</summary>
    internal Func<int?> LocalGod { get; set; } = static () => null;

    /// <summary>Builds the HUD's controls.</summary>
    public override void _Ready()
    {
        Layer = 2;
        _plaque = new GodPlaque(GodColor);
        Place(_plaque, Control.LayoutPreset.TopLeft);
        BuildTopCentre();
        BuildTopRight();
        BuildBottom();
        _creature = new CreaturePanel { Visible = false };
        Place(_creature, Control.LayoutPreset.BottomLeft);
        _creature.GrowVertical = Control.GrowDirection.Begin;
        _creature.Judged += kind => CreatureJudged?.Invoke(kind);
        _creature.LookClicked += () => CreatureLookClicked?.Invoke();
        _creature.SendClicked += () => CreatureSendClicked?.Invoke();
        _creature.FightClicked += () => CreatureFightClicked?.Invoke();
    }

    /// <summary>Shows a new HUD model; controls change only where the text or state changed.</summary>
    /// <param name="model">The model.</param>
    public void ShowModel(HudModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _plaque.Visible = model.HasGod;
        if (model.HasGod)
        {
            _plaque.ShowModel(model, LocalGod());
        }

        _time.ShowModel(model);
        Set(_hint, model.TargetHint ?? string.Empty);
        _hintPanel.Visible = model.TargetHint is not null;
        _hint.Modulate = model.TargetValid ? UiTheme.Good : UiTheme.Bad;
        ShowSlots(model.Miracles);
        _creature.Visible = model.Creature is not null;
        if (model.Creature is { } card)
        {
            _creature.ShowCard(card);
        }

        Set(_contest, model.Contest ?? string.Empty);
        _contestPanel.Visible = model.Contest is not null;
        _contest.Modulate = model.ContestFavoursPlayer ? new Color(0.85f, 1f, 0.85f) : new Color(1f, 0.8f, 0.75f);
        _contestMeter.Visible = model.WinningShare > 0f;
        _contestMeter.Show(model.BeliefShare, model.ContestFavoursPlayer ? UiTheme.Good : UiTheme.Warning, model.WinningShare);
        Set(_weather, model.Weather ?? string.Empty);
        _weather.Visible = model.Weather is not null;
        _weather.Modulate = model.WeatherWarning ? UiTheme.Warning : Colors.White;
        Set(_domains, model.Domains ?? string.Empty);
        _domains.Visible = model.Domains is not null;
        _domains.Modulate = model.DomainToChoose ? UiTheme.Gold : new Color(0.85f, 0.8f, 0.65f);
    }

    /// <summary>Shows the legend of the map overlay, or hides it.</summary>
    /// <param name="legend">Legend text, or <c>null</c> when no overlay is shown.</param>
    public void ShowOverlayLegend(string? legend)
    {
        Set(_overlayLegend, legend ?? string.Empty);
        _overlayLegend.Visible = legend is not null;
    }

    /// <summary>Shows the alerts, newest first.</summary>
    /// <param name="alerts">Alerts to show.</param>
    public void ShowAlerts(IReadOnlyList<Alert> alerts) => _alerts.ShowAlerts(alerts);

    /// <summary>Shows a short message above the miracle bar, which fades away.</summary>
    /// <param name="message">The message.</param>
    public void ShowStatus(string message) => _status.ShowMessage(message);

    /// <summary>Top centre: the race for victory with its meter, the weather and the domains.</summary>
    private void BuildTopCentre()
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        Place(column, Control.LayoutPreset.CenterTop);
        column.GrowHorizontal = Control.GrowDirection.Both;
        _contestPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(520f, 0f) };
        column.AddChild(_contestPanel);
        var inner = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        inner.AddThemeConstantOverride("separation", 4);
        _contestPanel.AddChild(inner);
        _contest = UiTheme.Label(inner, UiTheme.SmallSize, UiTheme.Ink);
        _contest.HorizontalAlignment = HorizontalAlignment.Center;
        _contestMeter = new Meter(7f);
        inner.AddChild(_contestMeter);
        _weather = Caption(column, UiTheme.CaptionSize, UiTheme.Ink);
        _domains = Caption(column, UiTheme.CaptionSize, UiTheme.Ink);
        _overlayLegend = Caption(column, UiTheme.CaptionSize, UiTheme.Muted);
        _overlayLegend.Visible = false;
    }

    /// <summary>Top right: the season and speed, and the alerts below.</summary>
    private void BuildTopRight()
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 8);
        Place(column, Control.LayoutPreset.TopRight);
        column.GrowHorizontal = Control.GrowDirection.Begin;
        _time = new TimeControls(Pantheon.Game.SessionCommands.SpeedPresets) { SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        _time.PauseClicked += () => PauseClicked?.Invoke();
        _time.SpeedClicked += preset => SpeedClicked?.Invoke(preset);
        column.AddChild(_time);
        _alerts = new AlertList();
        _alerts.Clicked += position => AlertClicked?.Invoke(position);
        column.AddChild(_alerts);
    }

    /// <summary>Bottom: the status toast and the target hint over the miracle bar, clear of the Creature's card.</summary>
    private void BuildBottom()
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        column.AddThemeConstantOverride("separation", 8);
        AddChild(column);
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide, Control.LayoutPresetMode.Minsize, Margin);
        column.OffsetLeft = BarLeft;
        column.GrowVertical = Control.GrowDirection.Begin;
        _status = new StatusToast();
        column.AddChild(_status);
        _hintPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, Visible = false };
        _hintPanel.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(0.03f, 0.04f, 0.05f, 0.85f), UiTheme.Edge with { A = 0.5f }, 1, 14, margin: 8));
        column.AddChild(_hintPanel);
        _hint = UiTheme.Label(_hintPanel, UiTheme.BodySize, Colors.White);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _bar = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        _bar.AddThemeConstantOverride("separation", 6);
        column.AddChild(_bar);
    }

    /// <summary>Creates, updates or removes the miracle tiles to match the slots.</summary>
    private void ShowSlots(IReadOnlyList<MiracleSlot> slots)
    {
        while (_tiles.Count > slots.Count)
        {
            _tiles[^1].QueueFree();
            _tiles.RemoveAt(_tiles.Count - 1);
            _tileIds.RemoveAt(_tileIds.Count - 1);
        }

        while (_tiles.Count < slots.Count)
        {
            var index = _tiles.Count;
            var tile = new MiracleTile();
            tile.Clicked += () => MiracleClicked?.Invoke(_tileIds[index].Length > 0 ? _tileIds[index] : null);
            _bar.AddChild(tile);
            _tiles.Add(tile);
            _tileIds.Add(string.Empty);
        }

        for (var i = 0; i < slots.Count; i++)
        {
            var (glyph, tint) = MiracleLook(slots[i].MiracleId);
            _tiles[i].Show(slots[i], glyph, tint, CooldownOf(slots[i].MiracleId));
            _tileIds[i] = slots[i].Selected ? string.Empty : slots[i].MiracleId;
        }
    }

    /// <summary>Adds a control at a corner or edge of the screen, inset by the margin.</summary>
    private void Place(Control control, Control.LayoutPreset preset)
    {
        AddChild(control);
        control.SetAnchorsAndOffsetsPreset(preset, Control.LayoutPresetMode.Minsize, Margin);
    }

    /// <summary>A centred line of text on a soft shadow, for the top centre.</summary>
    private static Label Caption(Control parent, int size, Color color)
    {
        var label = UiTheme.Label(parent, size, color);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(620f, 0f);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        label.AddThemeConstantOverride("outline_size", 5);
        return label;
    }

    /// <summary>Sets a label's text when it changed.</summary>
    private static void Set(Label label, string text)
    {
        if (label.Text != text)
        {
            label.Text = text;
        }
    }
}
