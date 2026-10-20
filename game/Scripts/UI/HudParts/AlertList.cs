using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Alerts;

namespace Pantheon.Client.UI;

/// <summary>
/// The alerts at the top right (spec §59, ADR-059): newest first, each edged in its severity's
/// colour; one with a place takes the camera there when clicked.
/// </summary>
internal sealed partial class AlertList : VBoxContainer
{
    /// <summary>The feed's width at the design size.</summary>
    public const float Width = 360f;

    private readonly Dictionary<Alert, Button> _buttons = [];

    /// <summary>Builds an empty feed.</summary>
    public AlertList()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(Width, 0f);
        AddThemeConstantOverride("separation", 6);
    }

    /// <summary>The player clicked an alert that has a place.</summary>
    public event Action<GroundPosition>? Clicked;

    /// <summary>Shows the alerts, newest first, adding and removing only what changed.</summary>
    /// <param name="alerts">The alerts.</param>
    public void ShowAlerts(IReadOnlyList<Alert> alerts)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        var wanted = new HashSet<Alert>(alerts);
        foreach (var (alert, button) in new Dictionary<Alert, Button>(_buttons))
        {
            if (!wanted.Contains(alert))
            {
                _buttons.Remove(alert);
                button.QueueFree();
            }
        }

        for (var i = 0; i < alerts.Count; i++)
        {
            if (!_buttons.TryGetValue(alerts[i], out var button))
            {
                button = Create(alerts[i]);
                _buttons[alerts[i]] = button;
                AddChild(button);
            }

            MoveChild(button, i);
        }
    }

    /// <summary>A clickable alert with its severity's edge.</summary>
    private Button Create(Alert alert)
    {
        var accent = alert.Severity switch
        {
            AlertSeverity.Info => UiTheme.Calm,
            AlertSeverity.Good => UiTheme.Good,
            AlertSeverity.Warning => UiTheme.Warning,
            AlertSeverity.Danger => UiTheme.Bad,
        };
        var button = new Button
        {
            Text = alert.Text,
            FocusMode = FocusModeEnum.None,
            Alignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(Width, 0f),
            TooltipText = alert.Location is null ? string.Empty : "Click to look there",
            MouseDefaultCursorShape = alert.Location is null ? CursorShape.Arrow : CursorShape.PointingHand,
        };
        button.AddThemeStyleboxOverride("normal", Edged(new Color(0.05f, 0.06f, 0.08f, 0.9f), accent));
        button.AddThemeStyleboxOverride("hover", Edged(new Color(0.12f, 0.12f, 0.12f, 0.95f), accent.Lightened(0.2f)));
        button.AddThemeStyleboxOverride("pressed", Edged(new Color(0.12f, 0.12f, 0.12f, 0.95f), accent));
        button.AddThemeFontSizeOverride("font_size", UiTheme.SmallSize);
        button.AddThemeColorOverride("font_color", UiTheme.Ink);
        button.AddThemeColorOverride("font_hover_color", UiTheme.Ink);
        if (alert.Location is { } location)
        {
            button.Pressed += () => Clicked?.Invoke(location);
        }

        return button;
    }

    /// <summary>A dark panel with a thick edge of the accent on its left.</summary>
    private static StyleBoxFlat Edged(Color back, Color accent)
    {
        var style = UiTheme.Panel(back, accent with { A = 0.9f }, 0, 8, margin: 10);
        style.BorderWidthLeft = 5;
        style.ContentMarginLeft = 14;
        return style;
    }
}
