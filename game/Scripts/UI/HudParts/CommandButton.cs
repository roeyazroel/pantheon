using System;
using Godot;

namespace Pantheon.Client.UI;

/// <summary>
/// A square button of the god's bar, or the Veil toggle (ADR-068): its picture, its key on a badge,
/// its name in the tooltip, and a gilded edge while what it opens is lit.
/// </summary>
internal sealed partial class CommandButton : Control
{
    private readonly CommandGlyph _glyph;
    private readonly string _key;
    private bool _lit;
    private bool _hovered;

    /// <summary>Builds the button.</summary>
    /// <param name="glyph">Its picture.</param>
    /// <param name="key">Its key, for the badge; empty for none.</param>
    /// <param name="name">Its name, for the tooltip.</param>
    /// <param name="side">Its side at the design size.</param>
    public CommandButton(CommandGlyph glyph, string key, string name, float side)
    {
        (_glyph, _key) = (glyph, key);
        CustomMinimumSize = new Vector2(side, side);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = key.Length > 0 ? $"{name} ({key})" : name;
        MouseEntered += () => Hover(true);
        MouseExited += () => Hover(false);
        GuiInput += OnInput;
    }

    /// <summary>The player clicked the button.</summary>
    public event Action? Clicked;

    /// <summary>Whether what it opens or turns on is lit.</summary>
    public bool Lit
    {
        get => _lit;
        set
        {
            if (_lit != value)
            {
                _lit = value;
                QueueRedraw();
            }
        }
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var back = _lit ? new Color(0.24f, 0.19f, 0.09f, 0.95f) : _hovered ? new Color(0.13f, 0.14f, 0.18f, 0.95f) : UiTheme.PanelBack;
        var edge = _lit ? UiTheme.Gold : _hovered ? UiTheme.Edge : UiTheme.Edge with { A = 0.45f };
        DrawStyleBox(UiTheme.Panel(back, edge, _lit ? 2 : 1, 9, margin: 0), rect);
        var glyphSize = Size.X * 0.5f;
        CommandGlyphs.Draw(this, _glyph, rect.GetCenter() + new Vector2(0f, _key.Length > 0 ? -2f : 0f), glyphSize, _lit || _hovered ? UiTheme.Gold : UiTheme.Ink);
        if (_key.Length > 0)
        {
            var fontSize = Size.X >= 56f ? 13 : 11;
            DrawString(UiTheme.Strong, new Vector2(0f, Size.Y - 3f), _key, HorizontalAlignment.Right, Size.X - 4f, fontSize, UiTheme.Gold with { A = 0.9f });
        }
    }

    /// <summary>Lights the button under the mouse.</summary>
    private void Hover(bool hovered)
    {
        _hovered = hovered;
        QueueRedraw();
    }

    /// <summary>A left click presses the button.</summary>
    private void OnInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Clicked?.Invoke();
            AcceptEvent();
        }
    }
}
