using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Flow;
using Pantheon.Networking;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The title screen's page for a new world (ADR-060, ADR-082), in four tabs: the world (its name,
/// seed, size, villages and climate), the player's god (its name, Creature and colour), the rivals
/// (how many gods, their temper and strength, and how they begin, ADR-068) and the victory (how it
/// is won, how long it is held, and the calamities). It opens on the last choices and keeps the new
/// ones. To host a shared world (ADR-018) it also asks the host's name and port.
/// </summary>
internal static class NewWorldPage
{
    private static readonly (string Name, string Description)[] Climates =
    [
        ("Four seasons", "Snowy winters and stores to last them, harvests in autumn, heat and fire in summer."),
        ("An even climate", "No seasons: the weather comes and goes the year round."),
    ];

    private static readonly (string Name, string Description)[] Acquaintances =
    [
        ("Strangers", "The gods meet as their lands, roads, believers or Creatures meet; until then none can deal with another."),
        ("Known to one another", "Every god knows every other from the start: treaties, wars and talk from the first moment."),
    ];

    private static readonly (string Name, string Description)[] VillageChoices =
    [
        ("Few villages", "A sparser island: fewer people to win, and more room between them."),
        ("The size's villages", "As many villages as the island's size holds."),
        ("Many villages", "A crowded island: more people to win, and neighbours close at hand."),
    ];

    private static readonly (string Name, string Description)[] Strengths =
    [
        ("Weak", "Your rivals begin with less Divine Power, and choose slowly and unsurely."),
        ("Even", "Your rivals play as the game makes them."),
        ("Strong", "Your rivals begin with more Divine Power, and choose quickly and surely."),
    ];

    private static readonly (string Name, string Description)[] CalamityChoices =
    [
        ("Gentle", "Earthquakes, eruptions, plagues and swarms of locusts come rarely."),
        ("Normal", "Disasters come as often as the land and the weather make them."),
        ("Harsh", "Disasters and plagues come often: prepare your villages, and be ready to save them."),
    ];

    /// <summary>Builds the page.</summary>
    /// <param name="menu">The menu, for its choices and to report the world.</param>
    /// <param name="seed">The seed offered.</param>
    /// <param name="host">Whether the world is hosted for friends to join.</param>
    /// <param name="tab">The tab shown first: 0 the world, 1 the player's god, 2 the rivals, 3 the victory.</param>
    /// <returns>The page.</returns>
    public static Control Build(MenuScreen menu, WorldSeed seed, bool host = false, int tab = 0)
    {
        var content = menu.Content;
        var kept = menu.Choices;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        column.AddChild(UiKit.Heading(host ? "A Shared World" : "A New World"));
        column.AddChild(UiKit.Text(
            host ? "Friends join your world from Play Together, each taking one of the rival gods. No server or account: your computer runs the world."
                : "An island of villages, each believing in one god or another, or in none. You are a minor spirit with a handful of followers and a Creature of your own.",
            UiTheme.SmallSize,
            UiKit.Muted));
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(600, 400) };
        column.AddChild(tabs);
        var summary = UiKit.Text(string.Empty, UiTheme.SmallSize, UiTheme.Ink);
        var page = new Page(menu, content, summary);

        var world = Tab(tabs, "The world");
        var name = UiKit.Field(string.Empty, "named after its first village");
        world.AddChild(UiKit.Row("World name", name));
        var seedField = SeedRow(world, seed);
        page.Size = page.Add(world, "Size", menu.Sizes, Index(menu.Sizes, kept.Size));
        page.Villages = page.Add(world, "Villages", VillageChoices, (int)kept.Villages);
        page.Climate = page.Add(world, "Climate", Climates, kept.Seasons ? 0 : 1);

        var god = Tab(tabs, "Your god");
        var (playerName, playerCreature) = PlayerNames(content);
        page.GodName = NameRow(god, "Your god's name", kept.GodName, playerName);
        page.Species = page.Add(god, "Your Creature", menu.Species, Index(menu.Species, kept.Species));
        page.CreatureName = NameRow(god, "Its name", kept.CreatureName, playerCreature);
        page.Color = ColorRow(god, menu.GodColors, kept.Color ?? 0);

        var rivals = Tab(tabs, "The rivals");
        page.Gods = new OptionButton { CustomMinimumSize = new Vector2(320, 38), FocusMode = Control.FocusModeEnum.None };
        rivals.AddChild(UiKit.Row("Gods", page.Gods));
        page.GodsAbout = UiKit.Text(string.Empty, UiTheme.CaptionSize, UiKit.Muted);
        rivals.AddChild(page.GodsAbout);
        page.Temper = page.Add(rivals, "Their temper", Tempers(content), (int)kept.Temper);
        page.Strength = page.Add(rivals, "Their strength", Strengths, (int)kept.Rivals);
        page.Met = page.Add(rivals, "How they begin", Acquaintances, kept.GodsMet ? 1 : 0);

        var victory = Tab(tabs, "Victory");
        page.Victory = page.Add(victory, "Victory", VictoryText.Choices.Select(c => (c.Name, c.Description)).ToList(), kept.VictoryIndex);
        page.Holds = content is null ? [] : WorldChoices.HoldMinutes(content);
        page.Hold = page.Add(victory, "Time to hold", page.Holds.Select(m => (Capitalized(VictoryText.Span(m * 60f)), "How long a victory must be held without a break.")).ToList(), HoldIndex(page.Holds, kept.HoldMinutes ?? (content is null ? 10 : WorldChoices.DefaultHoldMinutes(content))));
        page.Calamities = page.Add(victory, "Calamities", CalamityChoices, (int)kept.Calamities);

        page.GodsPicked = kept.Gods is not null;
        page.WantedGods = kept.Gods;
        page.Gods.ItemSelected += index =>
        {
            page.GodsPicked = true;
            page.WantedGods = WorldChoices.MinGods + (int)index;
            page.Refresh();
        };
        page.Refresh();
        tabs.CurrentTab = Math.Clamp(tab, 0, tabs.GetTabCount() - 1);
        column.AddChild(summary);

        var (hostName, port) = host ? HostRows(column) : (null, null);
        var begin = UiKit.Button(host ? "Host" : "Begin", () =>
        {
            var choices = page.Choices();
            menu.KeepChoices(choices);
            var options = content is null ? new NewWorldOptions() : choices.Options(content);
            var chosenSeed = ParseSeed(seedField.Text, seed);
            if (host)
            {
                menu.ChooseHost(chosenSeed, options, name.Text.Trim(), hostName!.Text.Trim(), Port(port!.Text));
            }
            else
            {
                menu.ChooseNewWorld(chosenSeed, options, name.Text.Trim());
            }
        });
        begin.AddThemeFontOverride("font", UiTheme.Title);
        begin.AddThemeFontSizeOverride("font_size", 24);
        begin.CustomMinimumSize = new Vector2(0, 52);
        column.AddChild(begin);
        return column;
    }

    /// <summary>A tab with a column of rows.</summary>
    private static VBoxContainer Tab(TabContainer tabs, string name)
    {
        var margin = new MarginContainer { Name = name };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 12);
        tabs.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        return column;
    }

    /// <summary>The seed, with a button for another.</summary>
    private static LineEdit SeedRow(VBoxContainer tab, WorldSeed seed)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var field = UiKit.Field(seed.Value.ToString(CultureInfo.InvariantCulture), "seed");
        field.CustomMinimumSize = new Vector2(268, 38);
        row.AddChild(field);
        var roll = UiKit.Button("↻", () => field.Text = System.Random.Shared.NextInt64(1, 1_000_000_000).ToString(CultureInfo.InvariantCulture));
        roll.TooltipText = "Another seed: every seed makes its own island";
        roll.CustomMinimumSize = new Vector2(46, 38);
        row.AddChild(roll);
        tab.AddChild(UiKit.Row("Seed", row));
        return field;
    }

    /// <summary>A name the player may give, the content's shown while it is empty.</summary>
    private static LineEdit NameRow(VBoxContainer tab, string caption, string kept, string placeholder)
    {
        var field = UiKit.Field(kept, placeholder);
        field.MaxLength = WorldChoices.NameLength;
        tab.AddChild(UiKit.Row(caption, field));
        return field;
    }

    /// <summary>The god colours, each with a swatch.</summary>
    private static OptionButton ColorRow(VBoxContainer tab, IReadOnlyList<(string Name, Color Color)> colors, int kept)
    {
        var choice = new OptionButton { CustomMinimumSize = new Vector2(320, 38), FocusMode = Control.FocusModeEnum.None };
        foreach (var (colorName, color) in colors)
        {
            var image = Image.CreateEmpty(28, 18, false, Image.Format.Rgba8);
            image.Fill(color);
            choice.AddIconItem(ImageTexture.CreateFromImage(image), colorName);
        }

        choice.Selected = colors.Count == 0 ? -1 : Math.Clamp(kept, 0, colors.Count - 1);
        tab.AddChild(UiKit.Row("Colour", choice));
        tab.AddChild(UiKit.Text("Your borders, temples and Creature wear it. A rival that would have worn it takes yours.", UiTheme.CaptionSize, UiKit.Muted));
        return choice;
    }

    /// <summary>The host's name and port, under the tabs.</summary>
    private static (LineEdit Name, LineEdit Port) HostRows(VBoxContainer column)
    {
        var name = UiKit.Field("Host", "your name");
        var port = UiKit.Field(NetworkProtocol.DefaultPort.ToString(CultureInfo.InvariantCulture), "port");
        column.AddChild(UiKit.Row("Your name", name));
        column.AddChild(UiKit.Row("Port", port));
        return (name, port);
    }

    /// <summary>The tempers offered, each with the personalities it allows.</summary>
    private static List<(string Name, string Description)> Tempers(GameContent? content)
    {
        string Of(RivalTemper temper) => content is null ? string.Empty
            : string.Join(", ", content.Sizes.PersonalitiesOf(temper).Select(p => Pantheon.Game.Divine.DivineText.Personality(p)));
        return
        [
            ("Mixed", "Any personality: " + Of(RivalTemper.Mixed) + "."),
            ("Peaceful", Of(RivalTemper.Peaceful) + ": they court, protect and keep to themselves more than they fight."),
            ("Warlike", Of(RivalTemper.Warlike) + ": expect fear, zealous conversion and war."),
        ];
    }

    /// <summary>The player's god's and Creature's names as the content gives them.</summary>
    private static (string God, string Creature) PlayerNames(GameContent? content)
    {
        var slot = content?.Pantheon.Gods.FirstOrDefault(g => g.Controller == Pantheon.Domain.Gods.GodController.Human);
        return (slot?.Name ?? "your god", slot?.CreatureName ?? "your Creature");
    }

    /// <summary>The index of a name among items; the first when it is not there.</summary>
    private static int Index(IReadOnlyList<(string Name, string Description)> items, string? name) =>
        Math.Max(0, items.ToList().FindIndex(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)));

    /// <summary>The index of the hold nearest a time.</summary>
    private static int HoldIndex(IReadOnlyList<int> holds, int minutes) =>
        holds.Count == 0 ? 0 : holds.Select((m, i) => (Gap: Math.Abs(m - minutes), Index: i)).OrderBy(h => h.Gap).ThenBy(h => h.Index).First().Index;

    /// <summary>Text with its first letter upper case: "Ten minutes".</summary>
    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>A seed typed by the player: digits are the seed and other text is hashed, as <c>--seed</c> reads it; nothing keeps the one offered.</summary>
    private static WorldSeed ParseSeed(string text, WorldSeed fallback) => text.Trim() is { Length: > 0 } typed ? WorldSeed.Parse(typed) : fallback;

    /// <summary>A port typed by the player, or the default.</summary>
    private static int Port(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535 ? port : NetworkProtocol.DefaultPort;

    /// <summary>The page's controls, and what follows from them: the gods a size and villages allow, and the summary.</summary>
    private sealed class Page
    {
        private readonly MenuScreen _menu;
        private readonly GameContent? _content;
        private readonly Label _summary;

        public Page(MenuScreen menu, GameContent? content, Label summary) => (_menu, _content, _summary) = (menu, content, summary);

        public OptionButton Size { get; set; } = null!;

        public OptionButton Villages { get; set; } = null!;

        public OptionButton Climate { get; set; } = null!;

        public LineEdit GodName { get; set; } = null!;

        public OptionButton Species { get; set; } = null!;

        public LineEdit CreatureName { get; set; } = null!;

        public OptionButton Color { get; set; } = null!;

        public OptionButton Gods { get; set; } = null!;

        public Label GodsAbout { get; set; } = null!;

        public OptionButton Temper { get; set; } = null!;

        public OptionButton Strength { get; set; } = null!;

        public OptionButton Met { get; set; } = null!;

        public OptionButton Victory { get; set; } = null!;

        public OptionButton Hold { get; set; } = null!;

        public IReadOnlyList<int> Holds { get; set; } = [];

        public OptionButton Calamities { get; set; } = null!;

        /// <summary>Whether the player chose a number of gods, rather than the size's own.</summary>
        public bool GodsPicked { get; set; }

        /// <summary>The number of gods the player chose.</summary>
        public int? WantedGods { get; set; }

        /// <summary>A choice with its line saying what the chosen item means, refreshing the page when it changes.</summary>
        public OptionButton Add(VBoxContainer tab, string caption, IReadOnlyList<(string Name, string Description)> items, int selected)
        {
            var choice = UiKit.Choice(items, out var about);
            if (items.Count > 0)
            {
                choice.Selected = Math.Clamp(selected, 0, items.Count - 1);
                choice.EmitSignal(OptionButton.SignalName.ItemSelected, choice.Selected);
            }

            choice.ItemSelected += _ => Refresh();
            tab.AddChild(UiKit.Row(caption, choice));
            tab.AddChild(about);
            return choice;
        }

        /// <summary>The gods the size and villages allow, the choice kept within them, and the summary.</summary>
        public void Refresh()
        {
            if (_content is null || Gods is null || Calamities is null)
            {
                return;
            }

            var size = SizeName();
            var villages = (VillageCount)Math.Max(0, Villages.Selected);
            var most = WorldChoices.MaxGods(_content, size, villages);
            var own = Math.Clamp(WorldChoices.SizeOf(_content, size)?.Gods ?? WorldChoices.MinGods, WorldChoices.MinGods, most);
            var count = GodsPicked && WantedGods is { } wanted ? Math.Clamp(wanted, WorldChoices.MinGods, most) : own;
            Gods.Clear();
            for (var gods = WorldChoices.MinGods; gods <= most; gods++)
            {
                Gods.AddItem(gods == own ? $"{gods} gods (the size's own)" : $"{gods} gods");
            }

            Gods.Selected = count - WorldChoices.MinGods;
            var held = WorldChoices.VillagesAt(_content, size, villages);
            GodsAbout.Text = $"You and {count - 1} {(count == 2 ? "rival" : "rivals")}. An island of {held} villages has room for up to {most} gods, so some villages begin free.";
            var hold = Holds.Count > 0 ? Holds[Math.Clamp(Hold.Selected, 0, Holds.Count - 1)] : WorldChoices.DefaultHoldMinutes(_content);
            var sandbox = VictoryText.Choices[Math.Clamp(Victory.Selected, 0, VictoryText.Choices.Count - 1)].Settings.IsSandbox();
            var won = sandbox ? "no victory: a sandbox" : "a victory held for " + VictoryText.Span(hold * 60f);
            _summary.Text = $"A {(size ?? "new").ToLowerInvariant()} island of {held} villages; you and {count - 1} {(count == 2 ? "rival" : "rivals")}; {won}.";
        }

        /// <summary>The choices as they stand.</summary>
        public NewWorldChoices Choices() => new()
        {
            GodName = GodName.Text.Trim(),
            CreatureName = CreatureName.Text.Trim(),
            Species = Name(_menu.Species, Species),
            Size = SizeName(),
            Villages = (VillageCount)Math.Max(0, Villages.Selected),
            Seasons = Climate.Selected <= 0,
            Color = Color.Selected >= 0 ? Color.Selected : null,
            Gods = GodsPicked ? WorldChoices.MinGods + Gods.Selected : null,
            Temper = (RivalTemper)Math.Max(0, Temper.Selected),
            Rivals = (RivalStrength)Math.Max(0, Strength.Selected),
            GodsMet = Met.Selected == 1,
            Victory = VictoryText.Choices[Math.Clamp(Victory.Selected, 0, VictoryText.Choices.Count - 1)].Name,
            HoldMinutes = Holds.Count > 0 ? Holds[Math.Clamp(Hold.Selected, 0, Holds.Count - 1)] : null,
            Calamities = (CalamityLevel)Math.Max(0, Calamities.Selected),
        };

        /// <summary>The size chosen, by name.</summary>
        private string? SizeName() => Name(_menu.Sizes, Size);

        /// <summary>The name chosen, or <c>null</c> when there is no choice.</summary>
        private static string? Name(IReadOnlyList<(string Name, string Description)> items, OptionButton choice) =>
            choice.Selected >= 0 && choice.Selected < items.Count ? items[choice.Selected].Name : null;
    }
}
