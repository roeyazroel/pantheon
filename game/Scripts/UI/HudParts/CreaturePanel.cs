using System;
using System.Globalization;
using Godot;
using Pantheon.Domain.Creatures;
using Pantheon.Game.Hud;

namespace Pantheon.Client.UI;

/// <summary>
/// The Creature's card at the bottom left (ADR-059): its name and level, what it is doing, meters
/// for its health and needs, its loyalty and its nature from cruel to kind, what a reward or a
/// punishment would judge, the buttons that command it, and its paths, attributes and bonds when
/// opened.
/// </summary>
internal sealed partial class CreaturePanel : PanelContainer
{
    private readonly Label _name;
    private readonly Label _level;
    private readonly Label _kind;
    private readonly Label _doing;
    private readonly Meter _health = new();
    private readonly Meter _hunger = new();
    private readonly Meter _fatigue = new();
    private readonly Meter _boredom = new();
    private readonly Meter _loyalty = new();
    private readonly Meter _nature = new(8f, centred: true);
    private readonly Label _judge;
    private readonly Button _reward;
    private readonly Button _punish;
    private readonly Button _more;
    private readonly VBoxContainer _details;
    private readonly Label _paths;
    private readonly Label _attributes;
    private readonly Label _bonds;

    /// <summary>Builds the card.</summary>
    public CreaturePanel()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(340f, 0f);
        AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(0.05f, 0.07f, 0.06f, 0.88f), new Color(0.5f, 0.62f, 0.36f, 0.85f), 1, 10));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        AddChild(column);

        var header = new HBoxContainer();
        column.AddChild(header);
        _name = UiTheme.Label(header, 22, UiTheme.Gold, UiTheme.Title);
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _level = UiTheme.Label(header, UiTheme.SmallSize, UiTheme.Ink, UiTheme.Strong);
        _kind = UiTheme.Label(column, UiTheme.CaptionSize, UiTheme.Muted, UiTheme.Italic);
        _kind.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _doing = UiTheme.Label(column, UiTheme.BodySize, UiTheme.Ink);
        _doing.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 3);
        column.AddChild(grid);
        Row(grid, "Health", _health);
        Row(grid, "Hunger", _hunger);
        Row(grid, "Tiredness", _fatigue);
        Row(grid, "Boredom", _boredom);
        Row(grid, "Loyalty", _loyalty);
        Row(grid, "Cruel – Kind", _nature);

        _judge = UiTheme.Label(column, UiTheme.CaptionSize, UiTheme.Good);
        _judge.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var buttons = new HFlowContainer();
        buttons.AddThemeConstantOverride("h_separation", 6);
        buttons.AddThemeConstantOverride("v_separation", 6);
        column.AddChild(buttons);
        _reward = Command(buttons, "Reward", "Z", "Reward it for what it is doing, or just did: it will do it more", () => Judged?.Invoke(CreatureCommandKind.Reward));
        _punish = Command(buttons, "Punish", "X", "Punish it for what it is doing, or just did: it will do it less", () => Judged?.Invoke(CreatureCommandKind.Punish));
        Command(buttons, "Follow", "C", "Look at it and follow it; move the view to let go", () => LookClicked?.Invoke());
        Command(buttons, "Send", "V", "Click the ground where it should go", () => SendClicked?.Invoke());
        Command(buttons, "Fight", "\u21e7V", "Click near a rival Creature: yours hunts it down and fights it", () => FightClicked?.Invoke());

        _more = new Button { Text = "More ▾", Flat = true, FocusMode = FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
        _more.AddThemeFontSizeOverride("font_size", UiTheme.CaptionSize);
        _more.Pressed += () => SetDetails(!_details!.Visible);
        column.AddChild(_more);
        _details = new VBoxContainer { Visible = false };
        _details.AddThemeConstantOverride("separation", 3);
        column.AddChild(_details);
        _paths = Wrapped(_details, UiTheme.Gold);
        _attributes = Wrapped(_details, UiTheme.Muted);
        _bonds = Wrapped(_details, UiTheme.Calm);
    }

    /// <summary>The player clicked Reward or Punish.</summary>
    public event Action<CreatureCommandKind>? Judged;

    /// <summary>The player clicked Follow.</summary>
    public event Action? LookClicked;

    /// <summary>The player clicked Send.</summary>
    public event Action? SendClicked;

    /// <summary>The player clicked Fight.</summary>
    public event Action? FightClicked;

    /// <summary>Shows a card; labels change only where their text did.</summary>
    /// <param name="card">The card.</param>
    public void ShowCard(CreatureCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        Set(_name, card.Name);
        Set(_level, string.Create(CultureInfo.InvariantCulture, $"Level {card.Level}"));
        Set(_kind, card.Character.Length > 0 ? $"{card.Species} · {card.Character}" : card.Species);
        Set(_doing, card.Doing);
        _doing.Modulate = card.Banished ? UiTheme.Bad : Colors.White;
        _health.Show(card.Health, card.Health < 0.3f ? UiTheme.Bad : UiTheme.Good);
        _hunger.Show(card.Hunger, card.Hunger > 0.7f ? UiTheme.Warning : new Color(0.85f, 0.65f, 0.35f));
        _fatigue.Show(card.Fatigue, card.Fatigue > 0.7f ? UiTheme.Warning : new Color(0.55f, 0.6f, 0.85f));
        _boredom.Show(card.Boredom, card.Boredom > 0.7f ? UiTheme.Warning : new Color(0.7f, 0.55f, 0.85f));
        _loyalty.Show(card.Loyalty, UiTheme.Gold);
        _nature.Show((card.Morality + 1f) / 2f, card.Morality >= 0f ? UiTheme.Good : UiTheme.Bad);
        Set(_judge, card.JudgeHint);
        _judge.Modulate = card.CanJudge ? Colors.White : new Color(1f, 1f, 1f, 0.55f);
        _reward.Disabled = !card.CanJudge;
        _punish.Disabled = !card.CanJudge;
        Set(_paths, card.Paths);
        Set(_attributes, card.Attributes);
        Set(_bonds, card.Bonds);
    }

    /// <summary>Opens or closes the details.</summary>
    private void SetDetails(bool open)
    {
        _details.Visible = open;
        _more.Text = open ? "Less ▴" : "More ▾";
    }

    /// <summary>A caption and its meter.</summary>
    private static void Row(GridContainer grid, string caption, Meter meter)
    {
        var label = UiTheme.Label(grid, UiTheme.CaptionSize, UiTheme.Muted);
        label.Text = caption;
        meter.CustomMinimumSize = new Vector2(190f, meter.CustomMinimumSize.Y);
        grid.AddChild(meter);
    }

    /// <summary>A command button with its key.</summary>
    private static Button Command(Container parent, string text, string key, string tooltip, Action pressed)
    {
        var button = new Button { Text = $"{text}  {key}", FocusMode = FocusModeEnum.None, TooltipText = tooltip, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        button.AddThemeFontSizeOverride("font_size", UiTheme.CaptionSize);
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    /// <summary>A wrapping label in the details.</summary>
    private static Label Wrapped(Control parent, Color color)
    {
        var label = UiTheme.Label(parent, UiTheme.CaptionSize, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(320f, 0f);
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
