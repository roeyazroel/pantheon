using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Game.Hud;

namespace Pantheon.Client.UI;

/// <summary>The top right of the HUD (ADR-059): the season and year, and buttons to pause and to run at ×1, ×4 or ×16.</summary>
internal sealed partial class TimeControls : PanelContainer
{
    private readonly Label _calendar;
    private readonly Button _pause;
    private readonly List<Button> _speeds = [];
    private (bool Paused, int Speed)? _shown;

    /// <summary>Builds the controls.</summary>
    /// <param name="speeds">The speeds offered, as multiples.</param>
    public TimeControls(IReadOnlyList<double> speeds)
    {
        ArgumentNullException.ThrowIfNull(speeds);
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.PanelBack, UiTheme.Edge, 1, 10, margin: 8));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        AddChild(row);
        _calendar = UiTheme.Label(row, UiTheme.SmallSize, UiTheme.Ink, UiTheme.Title);
        _calendar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _calendar.CustomMinimumSize = new Vector2(0f, 0f);
        _pause = Small(row, "❚❚", "Pause or resume (Space)", () => PauseClicked?.Invoke());
        for (var i = 0; i < speeds.Count; i++)
        {
            var preset = i;
            _speeds.Add(Small(row, $"×{speeds[i]:0}", $"Run at ×{speeds[i]:0} ([ and ])", () => SpeedClicked?.Invoke(preset)));
        }
    }

    /// <summary>The player clicked pause.</summary>
    public event Action? PauseClicked;

    /// <summary>The player clicked a speed: its index among the presets.</summary>
    public event Action<int>? SpeedClicked;

    /// <summary>Shows the calendar and which button is lit.</summary>
    /// <param name="model">The HUD's model.</param>
    public void ShowModel(HudModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var calendar = model.Calendar ?? string.Empty;
        if (_calendar.Text != calendar)
        {
            _calendar.Text = calendar;
            _calendar.Visible = calendar.Length > 0;
        }

        if (_shown == (model.Paused, model.SpeedPreset))
        {
            return;
        }

        _shown = (model.Paused, model.SpeedPreset);
        _pause.ButtonPressed = model.Paused;
        _pause.Text = model.Paused ? "▶" : "❚❚";
        for (var i = 0; i < _speeds.Count; i++)
        {
            _speeds[i].ButtonPressed = !model.Paused && i == model.SpeedPreset;
        }
    }

    /// <summary>A small toggle-look button.</summary>
    private static Button Small(Container parent, string text, string tooltip, Action pressed)
    {
        var button = new Button { Text = text, ToggleMode = true, FocusMode = FocusModeEnum.None, TooltipText = tooltip, CustomMinimumSize = new Vector2(44f, 32f) };
        button.AddThemeFontSizeOverride("font_size", UiTheme.SmallSize);
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }
}
