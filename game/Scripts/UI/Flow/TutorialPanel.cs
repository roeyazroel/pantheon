using System;
using Godot;
using Pantheon.Game.Flow;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The tutorial's card (ADR-064): which step of how many, its title and words, Next (or Skip this
/// step) and Leave tutorial; and a pulsing gold frame around the part of the screen the step speaks of.
/// </summary>
internal sealed partial class TutorialPanel : CanvasLayer
{
    private PanelContainer _card = null!;
    private Label _counter = null!;
    private Label _title = null!;
    private Label _text = null!;
    private Meter _progress = null!;
    private Button _next = null!;
    private Frame _frame = null!;

    /// <summary>The player clicked Next or Skip.</summary>
    public event Action? NextClicked;

    /// <summary>The player left the tutorial.</summary>
    public event Action? LeaveClicked;

    /// <summary>Who speaks the tutorial: the player's angel by name (ADR-074), else the tutorial itself.</summary>
    public string Speaker { get; set; } = "Tutorial";

    /// <summary>Where the card is on the screen while it shows, for the angel to stand beside it.</summary>
    public Rect2? CardRect => _card is { Visible: true } ? _card.GetGlobalRect() : null;

    /// <inheritdoc />
    public override void _Ready()
    {
        Layer = 5;
        _frame = new Frame { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_frame);
        _frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _card = new PanelContainer { CustomMinimumSize = new Vector2(430f, 0f), Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _card.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(0.05f, 0.06f, 0.09f, 0.95f), UiTheme.Gold with { A = 0.8f }, 2, 12, margin: 18));
        AddChild(_card);
        _card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterRight, Control.LayoutPresetMode.Minsize, 16);
        _card.GrowHorizontal = Control.GrowDirection.Begin;
        _card.GrowVertical = Control.GrowDirection.Both;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        _card.AddChild(column);
        _counter = UiTheme.Label(column, UiTheme.CaptionSize, UiTheme.Muted, UiTheme.Italic);
        _title = UiTheme.Label(column, 26, UiTheme.Gold, UiTheme.Title);
        _progress = new Meter(5f);
        column.AddChild(_progress);
        _text = UiTheme.Label(column, UiTheme.BodySize, UiTheme.Ink);
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.CustomMinimumSize = new Vector2(400f, 0f);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        column.AddChild(buttons);
        _next = UiKit.Button("Next", () => NextClicked?.Invoke());
        _next.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        buttons.AddChild(_next);
        var leave = UiKit.Button("Leave tutorial", () => LeaveClicked?.Invoke());
        leave.Flat = true;
        buttons.AddChild(leave);
    }

    /// <summary>Shows a step, or hides the card when the tutorial is over.</summary>
    /// <param name="tutorial">The tutorial.</param>
    /// <param name="text">The step's words.</param>
    /// <param name="target">Where on the screen it points, if anywhere.</param>
    public void ShowStep(Tutorial tutorial, string text, Rect2? target)
    {
        ArgumentNullException.ThrowIfNull(tutorial);
        _card.Visible = tutorial.IsActive;
        _frame.Target = tutorial.IsActive ? target : null;
        if (!tutorial.IsActive)
        {
            return;
        }

        var number = (int)tutorial.Step + 1;
        Set(_counter, $"{Speaker} · step {number} of {Tutorial.StepCount}");
        Set(_title, tutorial.Title());
        Set(_text, text);
        _progress.Show(number / (float)Tutorial.StepCount, UiTheme.Gold);
        Set(_next, tutorial.WaitsForNext ? (tutorial.Step == TutorialStep.Win ? "Finish" : "Next ▸") : "Skip this step");
    }

    private static void Set(Label label, string text)
    {
        if (label.Text != text)
        {
            label.Text = text;
        }
    }

    private static void Set(Button button, string text)
    {
        if (button.Text != text)
        {
            button.Text = text;
        }
    }

    /// <summary>A pulsing gold frame around what the step points at.</summary>
    private sealed partial class Frame : Control
    {
        private Rect2? _target;

        public Rect2? Target
        {
            get => _target;
            set
            {
                _target = value;
                QueueRedraw();
            }
        }

        public override void _Process(double delta)
        {
            if (_target is not null)
            {
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            if (_target is not { } target)
            {
                return;
            }

            var pulse = 0.5f + (0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 260f));
            var grown = target.Grow(6f + (4f * pulse));
            DrawStyleBox(UiTheme.Panel(new Color(0f, 0f, 0f, 0f), UiTheme.Gold with { A = 0.55f + (0.45f * pulse) }, 3, 12, margin: 0), grown);
            DrawStyleBox(UiTheme.Panel(new Color(0f, 0f, 0f, 0f), UiTheme.Gold with { A = 0.18f * pulse }, 8, 16, margin: 0), grown.Grow(6f));
        }
    }
}
