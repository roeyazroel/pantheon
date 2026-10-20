using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Game.Controls;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.UI;

/// <summary>
/// The god's bar (ADR-068), under the season at the top right: a button for every panel, the map,
/// the overview, the keys and the menu, each with its key; and beneath it, while open, the map's
/// chooser of overlays.
/// </summary>
internal sealed partial class GodBar : VBoxContainer
{
    private const float Side = 40f;

    private readonly HBoxContainer _row = new() { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
    private readonly PanelContainer _chooser = new() { Visible = false, SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
    private readonly VBoxContainer _choices = new();
    private readonly Dictionary<BarCommand, CommandButton> _buttons = [];
    private bool? _debugShown;

    /// <summary>Builds the bar.</summary>
    public GodBar()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 6);
        _row.AddThemeConstantOverride("separation", 4);
        AddChild(_row);
        _chooser.AddThemeStyleboxOverride("panel", UiTheme.Panel(UiTheme.PanelBack, UiTheme.Edge, 1, 10, margin: 8));
        _choices.AddThemeConstantOverride("separation", 2);
        _chooser.AddChild(_choices);
        AddChild(_chooser);
    }

    /// <summary>The player clicked a button.</summary>
    public event Action<BarCommand>? Clicked;

    /// <summary>The player chose an overlay from the map's chooser, or none.</summary>
    public event Action<OverlayKind?>? OverlayChosen;

    /// <summary>Whether the map's chooser is open.</summary>
    public bool ChooserOpen => _chooser.Visible;


    /// <summary>Builds the buttons again when the debug tools were turned on or off.</summary>
    /// <param name="debugTools">Whether the debug tools are on.</param>
    public void Refresh(bool debugTools)
    {
        if (_debugShown == debugTools)
        {
            return;
        }

        _debugShown = debugTools;
        foreach (var button in _buttons.Values)
        {
            button.QueueFree();
        }

        _buttons.Clear();
        foreach (var bar in CommandBar.Buttons(debugTools))
        {
            var button = new CommandButton(CommandGlyphs.For(bar.Command), KeyText.Of(bar.Action), bar.Name, Side);
            var command = bar.Command;
            button.Clicked += () => Clicked?.Invoke(command);
            _row.AddChild(button);
            _buttons[command] = button;
        }
    }

    /// <summary>Lights a button, or puts it out.</summary>
    /// <param name="command">The button.</param>
    /// <param name="lit">Whether it is lit.</param>
    public void Light(BarCommand command, bool lit)
    {
        if (_buttons.TryGetValue(command, out var button))
        {
            button.Lit = lit;
        }
    }

    /// <summary>Opens the map's chooser with the overlays offered, the one shown marked; or closes it.</summary>
    /// <param name="offered">The overlays offered.</param>
    /// <param name="shown">The overlay shown, if any.</param>
    public void ToggleChooser(IReadOnlyList<OverlayKind> offered, OverlayKind? shown)
    {
        ArgumentNullException.ThrowIfNull(offered);
        if (_chooser.Visible)
        {
            _chooser.Visible = false;
            return;
        }

        foreach (var child in _choices.GetChildren())
        {
            child.QueueFree();
        }

        _choices.AddChild(Choice("No overlay", shown is null, null));
        foreach (var kind in offered)
        {
            _choices.AddChild(Choice(OverlayChoices.Title(kind), shown == kind, kind));
        }

        _chooser.Visible = true;
    }

    /// <summary>A chooser row's box: snug, so the fourteen overlays fit beside the world.</summary>
    private static StyleBoxFlat Row(Color back) => UiTheme.Panel(back, new Color(0f, 0f, 0f, 0f), 0, 5, margin: 4);

    /// <summary>One overlay to choose, marked when it is shown.</summary>
    private Button Choice(string title, bool shown, OverlayKind? kind)
    {
        var button = new Button { Text = (shown ? "● " : "    ") + title, Flat = true, Alignment = HorizontalAlignment.Left, FocusMode = FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", UiTheme.CaptionSize);
        button.AddThemeColorOverride("font_color", shown ? UiTheme.Gold : UiTheme.Ink);
        button.AddThemeColorOverride("font_hover_color", UiTheme.Gold);
        button.AddThemeStyleboxOverride("normal", Row(new Color(0f, 0f, 0f, 0f)));
        button.AddThemeStyleboxOverride("hover", Row(new Color(1f, 1f, 1f, 0.07f)));
        button.AddThemeStyleboxOverride("pressed", Row(new Color(1f, 1f, 1f, 0.12f)));
        button.Pressed += () =>
        {
            _chooser.Visible = false;
            OverlayChosen?.Invoke(kind);
        };
        return button;
    }
}
