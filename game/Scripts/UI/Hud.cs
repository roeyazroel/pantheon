using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game.Alerts;
using Pantheon.Game.Hud;

namespace Pantheon.Client.UI;

/// <summary>
/// The player HUD (spec §57): Divine Power, income, followers and rank at the top, the miracle
/// bar and the target hint at the bottom, and the alerts on the right (spec §59). It shows what
/// <see cref="HudReadout"/> builds and reports clicks; it holds no game state.
/// </summary>
public partial class Hud : CanvasLayer
{
    private const int Margin = 12;
    private const int AlertWidth = 380;

    private static readonly Color Ink = new(0.95f, 0.93f, 0.86f);
    private static readonly Color Gold = new(1f, 0.84f, 0.38f);
    private static readonly Color Valid = new(0.6f, 0.95f, 0.6f);
    private static readonly Color Invalid = new(1f, 0.55f, 0.45f);
    private static readonly Color Muted = new(0.62f, 0.62f, 0.6f);
    private static readonly Color Warning = new(1f, 0.78f, 0.45f);

    private readonly List<Button> _slots = [];
    private readonly List<string> _slotIds = [];
    private readonly Dictionary<Alert, Button> _alertButtons = [];
    private Label _god = null!;
    private Label _power = null!;
    private Label _income = null!;
    private Label _followers = null!;
    private Label _hint = null!;
    private Label _overlayLegend = null!;
    private Label _rivals = null!;
    private Label _contest = null!;
    private Label _weather = null!;
    private HBoxContainer _bar = null!;
    private VBoxContainer _alerts = null!;
    private StyleBoxFlat _slotStyle = null!;
    private PanelContainer _creaturePanel = null!;
    private Label _creatureTitle = null!;
    private Label _creatureDoing = null!;
    private Label _creatureCondition = null!;
    private Label _creatureTemperament = null!;
    private Label _creatureJudge = null!;
    private Button _reward = null!;
    private Button _punish = null!;
    private StyleBoxFlat _selectedStyle = null!;

    /// <summary>The player clicked a miracle button; the id is the miracle, or <c>null</c> to deselect it.</summary>
    public event Action<string?>? MiracleClicked;

    /// <summary>The player clicked an alert that has a place.</summary>
    public event Action<GroundPosition>? AlertClicked;

    /// <summary>The player clicked Reward or Punish on the Creature card.</summary>
    public event Action<CreatureCommandKind>? CreatureJudged;

    /// <summary>The player clicked Look on the Creature card.</summary>
    public event Action? CreatureLookClicked;

    /// <summary>The player clicked Send on the Creature card: the next click on the ground sends it there.</summary>
    public event Action? CreatureSendClicked;

    /// <summary>Builds the HUD's controls.</summary>
    public override void _Ready()
    {
        Layer = 2;
        _slotStyle = Panel(new Color(0.06f, 0.08f, 0.1f, 0.82f), new Color(0.35f, 0.32f, 0.25f), 1);
        _selectedStyle = Panel(new Color(0.2f, 0.16f, 0.05f, 0.92f), Gold, 2);
        BuildTopBar();
        BuildMiracleBar();
        BuildAlerts();
        BuildCreatureCard();
    }

    /// <summary>Shows a new HUD model; controls change only where the text or state changed.</summary>
    /// <param name="model">The model.</param>
    public void ShowModel(HudModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        SetText(_god, model.GodLine);
        SetText(_power, model.PowerText);
        SetText(_income, model.IncomeText);
        SetText(_followers, model.FollowersText);
        SetText(_hint, model.TargetHint ?? string.Empty);
        _hint.Modulate = model.TargetValid ? Valid : Invalid;
        ShowSlots(model.Miracles);
        ShowCreature(model.Creature);
        SetText(_rivals, string.Join("   ", model.Rivals));
        _rivals.Visible = model.Rivals.Count > 0;
        SetText(_contest, model.Contest ?? string.Empty);
        _contest.Visible = model.Contest is not null;
        _contest.Modulate = model.ContestFavoursPlayer ? Valid : Invalid;
        SetText(_weather, model.Weather ?? string.Empty);
        _weather.Visible = model.Weather is not null;
        _weather.Modulate = model.WeatherWarning ? Warning : Ink;
    }

    /// <summary>Shows the legend of the map overlay, or hides it.</summary>
    /// <param name="legend">Legend text, or <c>null</c> when no overlay is shown.</param>
    public void ShowOverlayLegend(string? legend)
    {
        SetText(_overlayLegend, legend ?? string.Empty);
        _overlayLegend.Visible = legend is not null;
    }

    /// <summary>Shows the alerts, newest first.</summary>
    /// <param name="alerts">Alerts to show.</param>
    public void ShowAlerts(IReadOnlyList<Alert> alerts)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        var wanted = new HashSet<Alert>(alerts);
        foreach (var (alert, button) in new Dictionary<Alert, Button>(_alertButtons))
        {
            if (!wanted.Contains(alert))
            {
                _alertButtons.Remove(alert);
                button.QueueFree();
            }
        }

        for (var i = 0; i < alerts.Count; i++)
        {
            if (!_alertButtons.TryGetValue(alerts[i], out var button))
            {
                button = CreateAlertButton(alerts[i]);
                _alertButtons[alerts[i]] = button;
                _alerts.AddChild(button);
            }

            _alerts.MoveChild(button, i);
        }
    }

    /// <summary>Top centre: god and rank, Divine Power with income, followers.</summary>
    private void BuildTopBar()
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Panel(new Color(0.05f, 0.06f, 0.08f, 0.8f), new Color(0.45f, 0.38f, 0.2f), 1));
        AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, Margin);
        panel.GrowHorizontal = Control.GrowDirection.Both;

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(column);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 18);
        column.AddChild(row);
        _god = AddLabel(row, 18, Ink);
        _power = AddLabel(row, 24, Gold);
        _income = AddLabel(row, 16, Valid);
        _followers = AddLabel(row, 16, Ink);
        _rivals = AddLabel(column, 14, new Color(0.95f, 0.7f, 0.65f));
        _rivals.HorizontalAlignment = HorizontalAlignment.Center;
        _contest = AddLabel(column, 15, Ink);
        _contest.HorizontalAlignment = HorizontalAlignment.Center;
        _weather = AddLabel(column, 14, Ink);
        _weather.HorizontalAlignment = HorizontalAlignment.Center;
        _overlayLegend = AddLabel(column, 14, Muted);
        _overlayLegend.HorizontalAlignment = HorizontalAlignment.Center;
        _overlayLegend.Visible = false;
    }

    /// <summary>Bottom centre: the target hint above the miracle buttons.</summary>
    private void BuildMiracleBar()
    {
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        AddChild(column);
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom, Control.LayoutPresetMode.Minsize, Margin);
        column.GrowHorizontal = Control.GrowDirection.Both;
        column.GrowVertical = Control.GrowDirection.Begin;
        _hint = AddLabel(column, 17, Valid);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        _hint.AddThemeConstantOverride("outline_size", 6);
        _bar = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        _bar.AddThemeConstantOverride("separation", 8);
        column.AddChild(_bar);
    }

    /// <summary>Right side, below the fallback badge: the alert list.</summary>
    private void BuildAlerts()
    {
        _alerts = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(AlertWidth, 0) };
        _alerts.AddThemeConstantOverride("separation", 6);
        AddChild(_alerts);
        _alerts.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, Margin);
        _alerts.GrowHorizontal = Control.GrowDirection.Begin;
        _alerts.OffsetTop = 56;
    }

    /// <summary>Left side: the Creature's card with its buttons.</summary>
    private void BuildCreatureCard()
    {
        _creaturePanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false, CustomMinimumSize = new Vector2(330, 0) };
        _creaturePanel.AddThemeStyleboxOverride("panel", Panel(new Color(0.05f, 0.07f, 0.06f, 0.82f), new Color(0.3f, 0.45f, 0.25f), 1));
        AddChild(_creaturePanel);
        _creaturePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterLeft, Control.LayoutPresetMode.Minsize, Margin);
        _creaturePanel.GrowVertical = Control.GrowDirection.Both;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        _creaturePanel.AddChild(column);
        _creatureTitle = AddLabel(column, 17, Gold);
        _creatureDoing = AddLabel(column, 15, Ink);
        _creatureCondition = AddLabel(column, 13, Muted);
        _creatureTemperament = AddLabel(column, 13, Muted);
        _creatureJudge = AddLabel(column, 13, Valid);
        _creatureJudge.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        column.AddChild(row);
        _reward = AddCardButton(row, "Reward [Z]", () => CreatureJudged?.Invoke(CreatureCommandKind.Reward));
        _punish = AddCardButton(row, "Punish [X]", () => CreatureJudged?.Invoke(CreatureCommandKind.Punish));
        AddCardButton(row, "Look [C]", () => CreatureLookClicked?.Invoke());
        AddCardButton(row, "Send [V]", () => CreatureSendClicked?.Invoke());
    }

    /// <summary>Shows the Creature card, or hides it without a Creature.</summary>
    private void ShowCreature(CreatureCard? card)
    {
        _creaturePanel.Visible = card is not null;
        if (card is null)
        {
            return;
        }

        SetText(_creatureTitle, card.Title);
        SetText(_creatureDoing, card.Doing);
        SetText(_creatureCondition, card.Condition);
        SetText(_creatureTemperament, card.Temperament);
        SetText(_creatureJudge, card.JudgeHint);
        _creatureJudge.Modulate = card.CanJudge ? Colors.White : new Color(0.7f, 0.7f, 0.7f);
        _reward.Disabled = !card.CanJudge;
        _punish.Disabled = !card.CanJudge;
        _creatureDoing.Modulate = card.Banished ? Invalid : Colors.White;
    }

    /// <summary>A small button on the Creature card.</summary>
    private Button AddCardButton(Container parent, string text, Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeStyleboxOverride("normal", _slotStyle);
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    /// <summary>Creates, updates or removes the miracle buttons to match the slots.</summary>
    private void ShowSlots(IReadOnlyList<MiracleSlot> slots)
    {
        while (_slots.Count > slots.Count)
        {
            _slots[^1].QueueFree();
            _slots.RemoveAt(_slots.Count - 1);
            _slotIds.RemoveAt(_slotIds.Count - 1);
        }

        while (_slots.Count < slots.Count)
        {
            var index = _slots.Count;
            var button = new Button { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(118, 52) };
            button.AddThemeFontSizeOverride("font_size", 15);
            button.Pressed += () => MiracleClicked?.Invoke(_slotIds[index].Length > 0 ? _slotIds[index] : null);
            _bar.AddChild(button);
            _slots.Add(button);
            _slotIds.Add(string.Empty);
        }

        for (var i = 0; i < slots.Count; i++)
        {
            ShowSlot(_slots[i], slots[i]);
            _slotIds[i] = slots[i].Selected ? string.Empty : slots[i].MiracleId;
        }
    }

    /// <summary>One miracle button: hotkey, name and cost or cooldown, greyed when it cannot be cast.</summary>
    private void ShowSlot(Button button, MiracleSlot slot)
    {
        var state = slot.Locked ? "locked"
            : slot.CooldownSeconds > 0 ? string.Create(CultureInfo.InvariantCulture, $"{slot.CooldownSeconds:0.0} s")
            : string.Create(CultureInfo.InvariantCulture, $"{slot.BaseCost:0}");
        var text = slot.Hotkey > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{slot.Hotkey}  {slot.Name}\n{state}")
            : $"{slot.Name}\n{state}";
        SetText(button, text);
        if (button.TooltipText != slot.Tooltip)
        {
            button.TooltipText = slot.Tooltip;
        }

        var style = slot.Selected ? _selectedStyle : _slotStyle;
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", _selectedStyle);
        var usable = !slot.Locked && slot.CooldownSeconds <= 0 && slot.Affordable;
        button.Modulate = usable || slot.Selected ? Colors.White : new Color(0.62f, 0.62f, 0.62f, 0.9f);
    }

    /// <summary>A clickable alert, coloured by severity.</summary>
    private Button CreateAlertButton(Alert alert)
    {
        var color = alert.Severity switch
        {
            AlertSeverity.Info => new Color(0.12f, 0.16f, 0.22f, 0.88f),
            AlertSeverity.Good => new Color(0.1f, 0.3f, 0.14f, 0.9f),
            AlertSeverity.Warning => new Color(0.42f, 0.3f, 0.06f, 0.92f),
            AlertSeverity.Danger => new Color(0.5f, 0.1f, 0.08f, 0.94f),
        };
        var button = new Button
        {
            Text = alert.Text,
            FocusMode = Control.FocusModeEnum.None,
            Alignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(AlertWidth, 0),
            TooltipText = alert.Location is null ? string.Empty : "Click to look there",
        };
        var style = Panel(color, color.Lightened(0.25f), 1);
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", Panel(color.Lightened(0.1f), color.Lightened(0.4f), 1));
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeColorOverride("font_color", Ink);
        if (alert.Location is { } location)
        {
            button.Pressed += () => AlertClicked?.Invoke(location);
        }

        return button;
    }

    /// <summary>Adds a label that ignores the mouse.</summary>
    private static Label AddLabel(Control parent, int size, Color color)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
        return label;
    }

    /// <summary>A rounded panel style.</summary>
    private static StyleBoxFlat Panel(Color background, Color border, int borderWidth)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(6);
        return style;
    }

    /// <summary>Sets a label's text when it changed.</summary>
    private static void SetText(Label label, string text)
    {
        if (label.Text != text)
        {
            label.Text = text;
        }
    }

    /// <summary>Sets a button's text when it changed.</summary>
    private static void SetText(Button button, string text)
    {
        if (button.Text != text)
        {
            button.Text = text;
        }
    }
}
