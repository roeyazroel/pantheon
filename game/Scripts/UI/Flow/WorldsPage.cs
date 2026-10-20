using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Pantheon.Game.Flow;
using Pantheon.Persistence.Profile;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The player's worlds (ADR-060): a card for each, the most recently played first, with its god,
/// rank and followers, size, seed, year and time played; Continue loads its newest save, Saves lists
/// them all, and Forget deletes it after a second click.
/// </summary>
internal static class WorldsPage
{
    /// <summary>Builds the page.</summary>
    /// <param name="menu">The menu, to report choices.</param>
    /// <param name="worlds">The worlds.</param>
    /// <returns>The page.</returns>
    public static Control Build(MenuScreen menu, IReadOnlyList<WorldCard> worlds)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        column.AddChild(UiKit.Heading("Your Worlds"));
        if (worlds.Count == 0)
        {
            column.AddChild(UiKit.Text("No world yet. Begin one from New World: it is saved as you play, every five minutes and whenever you save.", UiTheme.SmallSize, UiKit.Muted));
            return column;
        }

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(600, 520), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(list);
        foreach (var world in worlds)
        {
            list.AddChild(Card(menu, world));
        }

        return column;
    }

    /// <summary>One world's card.</summary>
    private static Control Card(MenuScreen menu, WorldCard card)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(0.09f, 0.1f, 0.13f, 0.95f), UiTheme.Edge with { A = 0.5f }, 1, 10, margin: 14));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        panel.AddChild(column);
        column.AddChild(UiKit.Heading(card.World.Name, 22));
        column.AddChild(UiKit.Text(card.Summary, UiTheme.SmallSize, UiTheme.Ink));
        column.AddChild(UiKit.Text(card.Details, UiTheme.CaptionSize, UiKit.Muted));

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        column.AddChild(buttons);
        var saves = new VBoxContainer { Visible = false };
        saves.AddThemeConstantOverride("separation", 4);
        if (card.Saves.Count > 0)
        {
            var newest = card.Saves[0];
            buttons.AddChild(UiKit.Button("Continue", () => menu.ChooseLoad(newest.Id)));
            var toggle = UiKit.Button($"Saves ({card.Saves.Count}) ▾", () => saves.Visible = !saves.Visible);
            buttons.AddChild(toggle);
            foreach (var save in card.Saves)
            {
                saves.AddChild(SaveRow(menu, save));
            }
        }

        var forget = UiKit.Button("Forget", () => { });
        forget.TooltipText = "Delete this world and all its saves";
        forget.Pressed += () =>
        {
            if (forget.Text == "Forget")
            {
                forget.Text = "Delete it for good?";
                forget.AddThemeColorOverride("font_color", UiTheme.Bad);
                return;
            }

            menu.ChooseDelete(card.World.Id);
        };
        buttons.AddChild(forget);
        column.AddChild(saves);
        return panel;
    }

    /// <summary>One save: its kind, name and time, and a button to load it.</summary>
    private static Control SaveRow(MenuScreen menu, SaveRecord save)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var kind = UiKit.Text(save.Kind switch { SaveKind.Manual => "Saved", SaveKind.Auto => "Auto", SaveKind.Quick => "Quick" }, UiTheme.CaptionSize, UiTheme.Gold);
        kind.CustomMinimumSize = new Vector2(60, 0);
        kind.AutowrapMode = TextServer.AutowrapMode.Off;
        row.AddChild(kind);
        var when = UiKit.Text(string.Create(CultureInfo.InvariantCulture, $"{save.Name} · {save.SavedUtc.ToLocalTime():d MMM, HH:mm}"), UiTheme.CaptionSize, UiTheme.Ink);
        when.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        when.AutowrapMode = TextServer.AutowrapMode.Off;
        row.AddChild(when);
        var load = UiKit.Button("Load", () => menu.ChooseLoad(save.Id));
        load.CustomMinimumSize = new Vector2(80, 32);
        row.AddChild(load);
        return row;
    }
}
