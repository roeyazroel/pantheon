using System.Collections.Generic;

namespace Pantheon.Game.Controls;

/// <summary>A button of the god's bar (ADR-068).</summary>
public enum BarCommand
{
    /// <summary>The player's angel: its counsel (ADR-074).</summary>
    Angel,

    /// <summary>The Book of the World.</summary>
    Book,

    /// <summary>The village at the view's centre.</summary>
    Villages,

    /// <summary>The gods: diplomacy.</summary>
    Gods,

    /// <summary>The Divine Council.</summary>
    Council,

    /// <summary>The domains and the holy sites.</summary>
    Domains,

    /// <summary>The world's history.</summary>
    History,

    /// <summary>The map's overlays, to choose from.</summary>
    Map,

    /// <summary>The whole island from afar.</summary>
    Overview,

    /// <summary>The key map.</summary>
    Keys,

    /// <summary>The debug tools' palette.</summary>
    Debug,

    /// <summary>The menu.</summary>
    Menu,
}

/// <summary>A button of the god's bar.</summary>
/// <param name="Command">What it does.</param>
/// <param name="Name">Its name, for its tooltip.</param>
/// <param name="Action">The input action it presses, whose key it shows; <c>null</c> when it has no key.</param>
public sealed record BarButton(BarCommand Command, string Name, string? Action);

/// <summary>
/// The god's bar (ADR-068): one small row of buttons for every panel, the map, the key map and the
/// menu, so nothing in play hides behind a key. A button presses its action, as its key does.
/// </summary>
public static class CommandBar
{
    /// <summary>The bar's buttons, in order: the debug palette's only with the debug tools on.</summary>
    /// <param name="debugTools">Whether the debug tools are on.</param>
    /// <returns>The buttons.</returns>
    public static IReadOnlyList<BarButton> Buttons(bool debugTools)
    {
        var buttons = new List<BarButton>
        {
            new(BarCommand.Angel, "Your angel: its counsel", "angel_call"),
            new(BarCommand.Book, "The Book of the World", "toggle_book"),
            new(BarCommand.Villages, "The village at the view's centre", "toggle_settlements"),
            new(BarCommand.Gods, "The gods: meeting, treaties, war and talk", "toggle_diplomacy"),
            new(BarCommand.Council, "The Divine Council", "toggle_council"),
            new(BarCommand.Domains, "Your domains and the holy sites", "toggle_domains"),
            new(BarCommand.History, "The world's history", "toggle_history"),
            new(BarCommand.Map, "The map's overlays", "toggle_map_overlay"),
            new(BarCommand.Overview, "The whole island from afar, and back", "camera_overview"),
            new(BarCommand.Keys, "The keys", null),
        };
        if (debugTools)
        {
            buttons.Add(new(BarCommand.Debug, "Debug tools", null));
        }

        buttons.Add(new(BarCommand.Menu, "The menu", "game_menu"));
        return buttons;
    }
}
