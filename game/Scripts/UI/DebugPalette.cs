using System;
using System.Linq;
using Godot;
using Pantheon.Client.UI.Flow;
using Pantheon.Game.Controls;

namespace Pantheon.Client.UI;

/// <summary>
/// The debug tools' palette (ADR-068), opened by the god's bar's Debug button while the tools are
/// on: a button for every debug tool, with its key. It stays open beside the world, and a tool that
/// acts where the player points acts at the view's centre.
/// </summary>
internal sealed partial class DebugPalette : CanvasLayer
{
    private PanelContainer _panel = null!;

    /// <summary>The player pressed a tool: its input action.</summary>
    public event Action<string>? ActionPressed;

    /// <summary>Whether the palette is shown.</summary>
    public bool PanelVisible
    {
        get => _panel.Visible;
        set => _panel.Visible = value;
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        Layer = 4;
        _panel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(620f, 0f) };
        _panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(0.05f, 0.05f, 0.08f, 0.94f), UiTheme.Edge, 1, 10, margin: 12));
        AddChild(_panel);
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterLeft, Control.LayoutPresetMode.Minsize, 16);
        _panel.GrowVertical = Control.GrowDirection.Both;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(column);
        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 12);
        var title = UiKit.Text("Debug tools", 20, UiKit.Gold);
        title.AddThemeFontOverride("font", UiTheme.Title);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        heading.AddChild(UiKit.Button("Close", () => PanelVisible = false));
        column.AddChild(heading);
        column.AddChild(UiKit.Text("For trying the game out. A tool that acts where you point acts at the middle of the view.", UiTheme.CaptionSize, UiKit.Muted));
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        column.AddChild(grid);
        foreach (var entry in KeyGuide.Entries.Where(e => e.Group == KeyGroup.Debug))
        {
            grid.AddChild(Tool(entry));
        }
    }

    /// <summary>A tool's button: what it does, and its key.</summary>
    private Button Tool(KeyEntry entry)
    {
        var action = entry.Actions[0];
        var keys = KeyText.Of(entry);
        var button = new Button { Text = entry.Text, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(300f, 34f), ClipText = true, TooltipText = $"{entry.Text} ({keys})" };
        button.AddThemeFontSizeOverride("font_size", UiTheme.CaptionSize);
        button.AddThemeConstantOverride("h_separation", 0);
        var key = UiKit.Text(keys, UiTheme.CaptionSize, UiTheme.Gold);
        key.AutowrapMode = TextServer.AutowrapMode.Off;
        key.MouseFilter = Control.MouseFilterEnum.Ignore;
        key.HorizontalAlignment = HorizontalAlignment.Right;
        key.VerticalAlignment = VerticalAlignment.Center;
        button.AddChild(key);
        key.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        key.OffsetRight = -10f;
        button.Pressed += () => ActionPressed?.Invoke(action);
        return button;
    }
}
