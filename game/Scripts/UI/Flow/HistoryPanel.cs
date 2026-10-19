using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Game.History;

namespace Pantheon.Client.UI.Flow;

/// <summary>The history panel (spec §57: History; H toggles it): the world's chronicle, newest first, the player's moments in gold (ADR-020).</summary>
internal sealed partial class HistoryPanel : CanvasLayer
{
    private const int LinesShown = 40;

    private PanelContainer _panel = null!;
    private VBoxContainer _lines = null!;
    private string _shown = string.Empty;

    /// <summary>Whether the panel is shown.</summary>
    public bool PanelVisible
    {
        get => _panel.Visible;
        set => _panel.Visible = value;
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        Layer = 4;
        _panel = UiKit.CentredPanel(this, 620, out var column);
        _panel.Visible = false;
        column.AddChild(UiKit.Text("The World's History  (H to close)", 20, UiKit.Gold));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(600, 480), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _lines = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _lines.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_lines);
    }

    /// <summary>Shows the entries, rebuilding the list only when it changed.</summary>
    /// <param name="entries">The chronicle, newest first.</param>
    public void ShowEntries(IReadOnlyList<HistoryEntry> entries)
    {
        var key = entries.Count + ":" + (entries.Count > 0 ? entries[0].Text : string.Empty);
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        UiKit.Clear(_lines);
        if (entries.Count == 0)
        {
            _lines.AddChild(UiKit.Text("Nothing worth remembering has happened yet.", 15, UiKit.Muted));
            return;
        }

        foreach (var entry in entries.Take(LinesShown))
        {
            _lines.AddChild(UiKit.Text(entry.Text, 15, entry.ConcernsYou ? UiKit.Gold : UiKit.Ink));
        }
    }
}
