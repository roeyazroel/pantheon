using System;
using Godot;

namespace Pantheon.Client.UI.Flow;

/// <summary>The first-minutes guide's panel (ADR-020): the step's text, "Next" for steps that ask for no action, and "Hide".</summary>
internal sealed partial class GuidePanel : CanvasLayer
{
    private PanelContainer _panel = null!;
    private Label _text = null!;
    private Button _next = null!;

    /// <summary>The player asked for the next step.</summary>
    public event Action? NextClicked;

    /// <summary>The player hid the guide.</summary>
    public event Action? HideClicked;

    /// <inheritdoc />
    public override void _Ready()
    {
        Layer = 2;
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(420, 0), Visible = false };
        _panel.AddThemeStyleboxOverride("panel", UiKit.Style(new Color(0.06f, 0.07f, 0.1f, 0.9f)));
        AddChild(_panel);
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight, Control.LayoutPresetMode.Minsize, 16);
        _panel.GrowHorizontal = Control.GrowDirection.Begin;
        _panel.GrowVertical = Control.GrowDirection.Begin;
        _panel.OffsetBottom = -110;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(column);
        column.AddChild(UiKit.Text("Guide", 16, UiKit.Gold));
        _text = UiKit.Text(string.Empty, 15, UiKit.Ink);
        _text.CustomMinimumSize = new Vector2(400, 0);
        column.AddChild(_text);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        column.AddChild(buttons);
        _next = UiKit.Button("Next", () => NextClicked?.Invoke());
        buttons.AddChild(_next);
        buttons.AddChild(UiKit.Button("Hide guide", () => HideClicked?.Invoke()));
    }

    /// <summary>Shows a step, or hides the panel when there is none.</summary>
    /// <param name="text">The step's text; empty to hide.</param>
    /// <param name="canSkip">Whether "Next" moves on.</param>
    public void ShowStep(string text, bool canSkip)
    {
        _panel.Visible = text.Length > 0;
        if (_text.Text != text)
        {
            _text.Text = text;
        }

        _next.Visible = canSkip;
    }
}
