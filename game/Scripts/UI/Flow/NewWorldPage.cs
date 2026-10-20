using System;
using System.Globalization;
using System.Linq;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Flow;

namespace Pantheon.Client.UI.Flow;

/// <summary>The title screen's page for a new world (ADR-060): its name, seed, Creature, size, victory, climate and how the gods begin (ADR-068).</summary>
internal static class NewWorldPage
{
    /// <summary>The climates offered: seasons first (ADR-028).</summary>
    private static readonly (string Name, string Description)[] Climates =
    [
        ("Four seasons", "Snowy winters and stores to last them, harvests in autumn, heat and fire in summer."),
        ("An even climate", "No seasons: the weather comes and goes the year round."),
    ];

    /// <summary>How the gods begin (ADR-068): strangers first.</summary>
    private static readonly (string Name, string Description)[] Acquaintances =
    [
        ("Strangers", "The gods meet as their lands, roads, believers or Creatures meet; until then none can deal with another."),
        ("Known to one another", "Every god knows every other from the start: treaties, wars and talk from the first moment."),
    ];

    /// <summary>Builds the page.</summary>
    /// <param name="menu">The menu, for its choices and to report the world.</param>
    /// <param name="seed">The seed offered.</param>
    /// <returns>The page.</returns>
    public static Control Build(MenuScreen menu, WorldSeed seed)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        column.AddChild(UiKit.Heading("A New World"));
        column.AddChild(UiKit.Text("An island of villages, each believing in one god or another, or in none. You are a minor spirit with a handful of followers and a Creature of your own.", UiTheme.SmallSize, UiKit.Muted));
        var name = UiKit.Field(string.Empty, "named after its first village");
        column.AddChild(UiKit.Row("World name", name));

        var seedRow = new HBoxContainer();
        seedRow.AddThemeConstantOverride("separation", 6);
        var seedField = UiKit.Field(seed.Value.ToString(CultureInfo.InvariantCulture), "seed");
        seedField.CustomMinimumSize = new Vector2(268, 38);
        seedRow.AddChild(seedField);
        var roll = UiKit.Button("↻", () => seedField.Text = Random.Shared.NextInt64(1, 1_000_000_000).ToString(CultureInfo.InvariantCulture));
        roll.TooltipText = "Another seed: every seed makes its own island";
        roll.CustomMinimumSize = new Vector2(46, 38);
        seedRow.AddChild(roll);
        column.AddChild(UiKit.Row("Seed", seedRow));

        var species = UiKit.Choice(menu.Species, out var speciesAbout);
        column.AddChild(UiKit.Row("Your Creature", species));
        column.AddChild(speciesAbout);
        var size = UiKit.Choice(menu.Sizes, out var sizeAbout);
        column.AddChild(UiKit.Row("Size", size));
        column.AddChild(sizeAbout);
        var victories = VictoryText.Choices.Select(c => (c.Name, c.Description)).ToList();
        var victory = UiKit.Choice(victories, out var victoryAbout);
        column.AddChild(UiKit.Row("Victory", victory));
        column.AddChild(victoryAbout);
        var climate = UiKit.Choice(Climates, out var climateAbout);
        column.AddChild(UiKit.Row("Climate", climate));
        column.AddChild(climateAbout);
        var acquaintance = UiKit.Choice(Acquaintances, out var acquaintanceAbout);
        column.AddChild(UiKit.Row("The gods", acquaintance));
        column.AddChild(acquaintanceAbout);

        var begin = UiKit.Button("Begin", () => menu.ChooseNewWorld(
            ParseSeed(seedField.Text, seed),
            new Pantheon.Game.NewWorldOptions(Chosen(menu.Species, species), Chosen(menu.Sizes, size), victory.Selected > 0 ? VictoryText.Choices[victory.Selected].Settings : null, climate.Selected <= 0, GodsMet: acquaintance.Selected == 1),
            name.Text.Trim()));
        begin.AddThemeFontOverride("font", UiTheme.Title);
        begin.AddThemeFontSizeOverride("font_size", 24);
        begin.CustomMinimumSize = new Vector2(0, 52);
        column.AddChild(begin);
        return column;
    }

    /// <summary>The name chosen, or <c>null</c> when there is no choice.</summary>
    private static string? Chosen(System.Collections.Generic.IReadOnlyList<(string Name, string Description)> items, OptionButton choice) =>
        choice.Selected >= 0 && choice.Selected < items.Count ? items[choice.Selected].Name : null;

    /// <summary>A seed typed by the player: digits are the seed and other text is hashed, as <c>--seed</c> reads it; nothing keeps the one offered.</summary>
    private static WorldSeed ParseSeed(string text, WorldSeed fallback) => text.Trim() is { Length: > 0 } typed ? WorldSeed.Parse(typed) : fallback;
}
