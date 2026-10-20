using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;
using Pantheon.Game.Minds;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The title screen's pages of a new world's gods (ADR-065): the gods waking while a language model
/// writes them, and then the gods of the world, each with its epithet, its Creature, its history and
/// its nature, before the player enters it.
/// </summary>
internal static class PantheonPages
{
    // The tallest the list of gods grows before it scrolls.
    private const float ListHeight = 560f;

    /// <summary>The gods waking: who writes them, and a way to let the scribes write them instead.</summary>
    /// <param name="modelName">The model writing them.</param>
    /// <param name="skip">Lets the scribes write them.</param>
    /// <returns>The page.</returns>
    public static Control Birth(string modelName, Action skip)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(UiKit.Heading("The gods are waking"));
        column.AddChild(UiKit.Text($"{modelName} is writing this world's rival gods: their names, their histories and their tempers. It can take a minute.", UiTheme.BodySize, UiTheme.Ink));
        column.AddChild(new ProgressBar { Indeterminate = true, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14) });
        column.AddChild(UiKit.Text("What they become decides how they will deal with you: whom they court, whom they betray and whom they war on.", UiTheme.SmallSize, UiTheme.Muted));
        column.AddChild(UiKit.Button("Let the scribes write them", skip));
        return column;
    }

    /// <summary>The gods of a world, the player's rivals first, and the way in.</summary>
    /// <param name="world">The world.</param>
    /// <param name="localGodId">The player's god.</param>
    /// <param name="note">Why the scribes wrote some gods, when they did; shown under the heading.</param>
    /// <param name="enter">Enters the world.</param>
    /// <returns>The page.</returns>
    public static Control Gods(WorldState world, int? localGodId, string? note, Action enter)
    {
        ArgumentNullException.ThrowIfNull(world);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        column.AddChild(UiKit.Heading("The Gods of This World"));
        var rivals = world.Gods.Where(g => g.Id != localGodId).ToList();
        column.AddChild(UiKit.Text(Authors(rivals), UiTheme.CaptionSize, UiTheme.Muted));
        if (note is { Length: > 0 })
        {
            column.AddChild(UiKit.Text(note, UiTheme.CaptionSize, UiTheme.Warning));
        }

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(600, Math.Min(ListHeight, 150f * rivals.Count)), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(list);
        column.AddChild(scroll);
        foreach (var god in rivals)
        {
            list.AddChild(Card(world, god));
        }

        var begin = UiKit.Button("Enter the world", enter);
        begin.AddThemeFontOverride("font", UiTheme.Title);
        begin.AddThemeFontSizeOverride("font_size", 24);
        begin.CustomMinimumSize = new Vector2(0, 52);
        column.AddChild(begin);
        return column;
    }

    /// <summary>One god: its title, personality and Creature, its history and its nature.</summary>
    private static Control Card(WorldState world, GodState god)
    {
        var card = new VBoxContainer();
        card.AddThemeConstantOverride("separation", 4);
        var title = UiKit.Text(PersonaText.Title(god), 24, UiTheme.Gold);
        title.AddThemeFontOverride("font", UiTheme.Title);
        card.AddChild(title);
        var creature = world.Creatures.Find(c => c.GodId == god.Id);
        var kind = DivineText.Personality(god.Personality) is { Length: > 0 } personality ? personality : "A god";
        card.AddChild(UiKit.Text(creature is null ? kind : $"{kind} · its Creature, {creature.Name} the {creature.Species.ToLowerInvariant()}", UiTheme.SmallSize, UiTheme.Muted));
        if (god.Persona is { } persona)
        {
            card.AddChild(UiKit.Text(persona.History, UiTheme.SmallSize, UiTheme.Ink));
            var nature = UiKit.Text(PersonaText.Nature(god), UiTheme.CaptionSize, UiTheme.Muted);
            nature.AddThemeFontOverride("font", UiTheme.Italic);
            card.AddChild(nature);
        }

        return card;
    }

    /// <summary>Who wrote the gods, in words.</summary>
    private static string Authors(IReadOnlyList<GodState> gods)
    {
        var authors = gods.Select(g => g.Persona?.Author).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        return authors.Count == 0 ? "The gods as the old tales tell them."
            : "Written by " + string.Join(" and ", authors) + ". What they are decides how they will deal with you.";
    }
}
