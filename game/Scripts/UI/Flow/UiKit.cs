using System;
using Godot;

namespace Pantheon.Client.UI.Flow;

/// <summary>The look shared by the menus and the game-flow panels: dark panels with a gold edge, in <see cref="UiTheme"/>'s palette.</summary>
internal static class UiKit
{
    /// <summary>Body text.</summary>
    public static readonly Color Ink = UiTheme.Ink;

    /// <summary>Titles.</summary>
    public static readonly Color Gold = UiTheme.Gold;

    /// <summary>Secondary text.</summary>
    public static readonly Color Muted = UiTheme.Muted;

    /// <summary>Good news.</summary>
    public static readonly Color Good = UiTheme.Good;

    /// <summary>Bad news.</summary>
    public static readonly Color Bad = UiTheme.Bad;

    /// <summary>A centred panel with a column inside, added to a layer.</summary>
    /// <param name="layer">The layer.</param>
    /// <param name="width">Minimum width in pixels.</param>
    /// <param name="column">The column to fill.</param>
    /// <returns>The panel.</returns>
    public static PanelContainer CentredPanel(CanvasLayer layer, float width, out VBoxContainer column)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0), MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Style(new Color(0.05f, 0.05f, 0.08f, 0.94f)));
        layer.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.Minsize);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Both;
        column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);
        return panel;
    }

    /// <summary>The panels' style.</summary>
    /// <param name="background">Background colour.</param>
    /// <returns>The style.</returns>
    public static StyleBoxFlat Style(Color background)
    {
        var style = new StyleBoxFlat { BgColor = background, BorderColor = new Color(0.5f, 0.42f, 0.25f), ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14 };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(8);
        return style;
    }

    /// <summary>A label.</summary>
    /// <param name="text">Its text.</param>
    /// <param name="size">Font size.</param>
    /// <param name="color">Colour.</param>
    /// <returns>The label, wrapping words.</returns>
    public static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>A button that runs an action.</summary>
    /// <param name="text">Its text.</param>
    /// <param name="pressed">What it does.</param>
    /// <returns>The button.</returns>
    public static Button Button(string text, Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 34) };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.Pressed += pressed;
        return button;
    }

    /// <summary>A one-line text field.</summary>
    /// <param name="value">Its text.</param>
    /// <param name="placeholder">Shown while empty.</param>
    /// <returns>The field.</returns>
    public static LineEdit Field(string value, string placeholder) => new() { Text = value, PlaceholderText = placeholder, CustomMinimumSize = new Vector2(260, 32) };

    /// <summary>A labelled row: a caption, then a control.</summary>
    /// <param name="caption">The caption.</param>
    /// <param name="control">The control.</param>
    /// <returns>The row.</returns>
    public static HBoxContainer Row(string caption, Control control)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var label = Text(caption, 15, Muted);
        label.CustomMinimumSize = new Vector2(110, 0);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        row.AddChild(label);
        row.AddChild(control);
        return row;
    }

    /// <summary>Removes and frees every child of a container.</summary>
    /// <param name="container">The container.</param>
    public static void Clear(Node container)
    {
        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }
}
