using Godot;
using Pantheon.Game.Controls;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The key map (ADR-068): every key and gesture of play in its group, in words, with the keys the
/// game binds; the debug tools' only while they are on. The settings' Keys tab and the god's bar's
/// Keys button show it.
/// </summary>
internal static class KeysPage
{
    /// <summary>Builds the map, in a column that scrolls.</summary>
    /// <param name="debugTools">Whether the debug tools are on.</param>
    /// <param name="height">The height it scrolls within.</param>
    /// <returns>The map.</returns>
    public static ScrollContainer Build(bool debugTools, float height)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0f, height), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(column);
        foreach (var (group, entries) in KeyGuide.Shown(debugTools))
        {
            var heading = UiKit.Text(KeyGuide.Heading(group), UiTheme.SmallSize, UiTheme.Gold);
            heading.AddThemeFontOverride("font", UiTheme.Title);
            column.AddChild(heading);
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 16);
            grid.AddThemeConstantOverride("v_separation", 3);
            foreach (var entry in entries)
            {
                var keys = UiKit.Text(KeyText.Of(entry), UiTheme.CaptionSize, UiTheme.Ink);
                keys.AddThemeFontOverride("font", UiTheme.Strong);
                keys.CustomMinimumSize = new Vector2(230f, 0f);
                keys.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                keys.VerticalAlignment = VerticalAlignment.Top;
                keys.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
                grid.AddChild(keys);
                var text = UiKit.Text(entry.Text, UiTheme.CaptionSize, UiTheme.Muted);
                text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                text.VerticalAlignment = VerticalAlignment.Top;
                text.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
                text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                grid.AddChild(text);
            }

            column.AddChild(grid);
        }

        if (!debugTools)
        {
            column.AddChild(UiKit.Text("The debug tools and their keys are off; Settings, under Interface, turns them on.", UiTheme.CaptionSize, UiTheme.Muted));
        }

        return scroll;
    }
}
