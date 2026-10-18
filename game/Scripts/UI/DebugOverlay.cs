using Godot;
using Pantheon.Game.Diagnostics;

namespace Pantheon.Client.UI;

/// <summary>
/// Debug panel (F1) showing the lines built by <see cref="DebugReadout"/>, plus a badge that stays
/// visible whenever the fallback terrain renderer is active, even with the panel hidden.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private Control _panel = null!;
    private Label _text = null!;
    private Label _fallbackBadge = null!;

    /// <summary>Whether the debug panel is shown.</summary>
    public bool PanelVisible
    {
        get => _panel.Visible;
        set => _panel.Visible = value;
    }

    /// <summary>Finds the panel and badge nodes.</summary>
    public override void _Ready()
    {
        _panel = GetNode<Control>("Panel");
        _text = GetNode<Label>("Panel/Text");
        _fallbackBadge = GetNode<Label>("FallbackBadge");
    }

    /// <summary>Shows new panel content; the label is only touched when the text changed.</summary>
    /// <param name="model">Formatted overlay lines.</param>
    public void ShowModel(DebugPanelModel model)
    {
        var text = model.ToText();
        if (_text.Text != text)
        {
            _text.Text = text;
        }
    }

    /// <summary>Shows or hides the fallback renderer badge.</summary>
    /// <param name="visible">Whether the fallback renderer is active.</param>
    public void SetFallbackBadgeVisible(bool visible) => _fallbackBadge.Visible = visible;
}
