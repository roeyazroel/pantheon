using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Migration;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.Migration;
using Pantheon.Game.Politics;

namespace Pantheon.Client.UI;

/// <summary>
/// The diplomacy panel (spec §57: Diplomacy; P toggles it): one card per other god with your
/// status, how it feels about you and why (spec §49), what it has proposed, and buttons for the
/// moves the authority would accept now, and your refugee policy (ADR-016); the treaties in force,
/// favours owed, and the treaties and requests you could put to it (ADR-025). It shows what
/// <see cref="DiplomacyReadout"/> builds and reports clicks; it holds no game state.
/// </summary>
public partial class DiplomacyPanel : CanvasLayer
{
    private static readonly Color Ink = new(0.95f, 0.93f, 0.86f);
    private static readonly Color Muted = new(0.7f, 0.7f, 0.68f);

    private readonly Dictionary<DiplomaticStatus, Color> _statusColors = new()
    {
        [DiplomaticStatus.Alliance] = new Color(0.55f, 0.9f, 0.55f),
        [DiplomaticStatus.Friendly] = new Color(0.7f, 0.9f, 0.6f),
        [DiplomaticStatus.Neutral] = new Color(0.85f, 0.85f, 0.8f),
        [DiplomaticStatus.Hostile] = new Color(1f, 0.7f, 0.4f),
        [DiplomaticStatus.War] = new Color(1f, 0.45f, 0.35f),
    };

    private readonly HashSet<int> _expanded = [];
    private PanelContainer _panel = null!;
    private HBoxContainer _refugees = null!;
    private HBoxContainer _terms = null!;
    private VBoxContainer _cards = null!;
    private string _shown = string.Empty;
    private int _duration = 1;

    /// <summary>The player clicked a treaty, request or break (ADR-025).</summary>
    public event Action<PoliticsAction>? ActionClicked;

    /// <summary>How the player will sign the treaties it proposes (panel state, not game state).</summary>
    public TreatyChoice Choice { get; private set; } = TreatyChoice.Default;

    /// <summary>The player clicked a move towards a god.</summary>
    public event Action<int, DiplomaticCommandKind>? CommandClicked;

    /// <summary>The player chose a refugee policy.</summary>
    public event Action<RefugeePolicy>? PolicyClicked;

    /// <summary>The player asked to speak with a god (ADR-066).</summary>
    public event Action<int>? SpeakClicked;

    /// <summary>Whether the panel is shown.</summary>
    public bool PanelVisible
    {
        get => _panel.Visible;
        set => _panel.Visible = value;
    }

    /// <summary>Builds the (hidden) panel.</summary>
    public override void _Ready()
    {
        Layer = 3;
        _panel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(520, 0) };
        var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.08f, 0.92f), BorderColor = new Color(0.5f, 0.42f, 0.25f), ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10 };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(8);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.Minsize);
        _panel.GrowHorizontal = Control.GrowDirection.Both;
        _panel.GrowVertical = Control.GrowDirection.Both;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(column);
        var title = new Label { Text = "Diplomacy  (P to close)" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.84f, 0.38f));
        column.AddChild(title);
        _refugees = new HBoxContainer();
        _refugees.AddThemeConstantOverride("separation", 8);
        column.AddChild(_refugees);
        _terms = new HBoxContainer();
        _terms.AddThemeConstantOverride("separation", 8);
        column.AddChild(_terms);
        _cards = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _cards.AddThemeConstantOverride("separation", 12);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(540, 640), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(_cards);
        column.AddChild(scroll);
    }

    /// <summary>Shows the entries and your refugee policy; the cards are rebuilt only when something changed.</summary>
    /// <param name="entries">One entry per other god.</param>
    /// <param name="policy">Your refugee policy, or <c>null</c> without a god.</param>
    public void ShowEntries(IReadOnlyList<DiplomacyEntry> entries, RefugeePolicy? policy)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var key = policy + "#" + Choice + "#" + string.Join(",", _expanded) + "#" + string.Join("|", Describe(entries));
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        BuildRefugeeRow(policy);
        BuildTermsRow();
        foreach (var child in _cards.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var entry in entries)
        {
            _cards.AddChild(BuildCard(entry));
        }

        if (entries.Count == 0)
        {
            _cards.AddChild(Text("There are no other gods.", 15, Muted));
        }
    }

    /// <summary>A card for one god.</summary>
    private Control BuildCard(DiplomacyEntry entry)
    {
        var card = new VBoxContainer();
        card.AddThemeConstantOverride("separation", 4);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        header.AddChild(Text(entry.Title, 18, Ink));
        header.AddChild(entry.Met ? Text(DiplomacyText.Status(entry.Status), 16, _statusColors[entry.Status]) : Text("Not yet met", 16, Muted));
        card.AddChild(header);
        if (entry.Lore.Length > 0)
        {
            // Who it is (ADR-065): the tale the island tells of it, and its nature.
            var lore = Text(entry.Lore, 13, new Color(0.82f, 0.76f, 0.62f));
            lore.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            lore.CustomMinimumSize = new Vector2(480, 0);
            card.AddChild(lore);
        }

        if (!entry.Met)
        {
            card.AddChild(Text(DiplomacyText.Unmet, 13, Muted));
            return card;
        }
        var practice = MigrationText.Practice(entry.RefugeePolicy);
        card.AddChild(Text((entry.Ties.Length > 0 ? entry.Ties + ". " : string.Empty) + char.ToUpperInvariant(practice[0]) + practice[1..] + ".", 14, new Color(0.62f, 0.8f, 1f)));
        if (entry.Creatures.Length > 0)
        {
            card.AddChild(Text(entry.Creatures, 14, new Color(0.75f, 0.85f, 0.6f)));
        }

        card.AddChild(Text(char.ToUpperInvariant(entry.Attitude[0]) + entry.Attitude[1..] + ".", 14, Muted));
        if (entry.Explanation.Length > 0)
        {
            var why = Text("It " + entry.Explanation + ".", 13, Muted);
            why.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            why.CustomMinimumSize = new Vector2(480, 0);
            card.AddChild(why);
        }

        if (entry.Grievances.Length > 0)
        {
            var grievances = Text("Your people remember: " + entry.Grievances + ".", 13, new Color(1f, 0.7f, 0.55f));
            grievances.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            grievances.CustomMinimumSize = new Vector2(480, 0);
            card.AddChild(grievances);
        }

        if (entry.War.Length > 0)
        {
            var war = Text(entry.War, 14, new Color(1f, 0.55f, 0.45f));
            war.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            war.CustomMinimumSize = new Vector2(480, 0);
            card.AddChild(war);
        }

        card.AddChild(Text(entry.Intelligence, 13, new Color(0.7f, 0.8f, 0.9f)));
        var politics = entry.Politics;
        foreach (var treaty in politics.Treaties)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(Text(treaty.Text, 14, new Color(0.75f, 0.9f, 0.75f)));
            row.AddChild(ActionButton(treaty.Break));
            card.AddChild(row);
        }

        if (politics.Others.Length > 0)
        {
            var others = Text(politics.Others, 13, Muted);
            others.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            others.CustomMinimumSize = new Vector2(480, 0);
            card.AddChild(others);
        }

        if (politics.Favours.Length > 0)
        {
            card.AddChild(Text(politics.Favours, 14, new Color(0.9f, 0.8f, 1f)));
        }

        if (politics.Proposal.Length > 0)
        {
            var proposal = Text(politics.Proposal, 15, new Color(1f, 0.85f, 0.4f));
            proposal.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            proposal.CustomMinimumSize = new Vector2(480, 0);
            card.AddChild(proposal);
        }

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        var speak = new Button { Text = "Speak", FocusMode = Control.FocusModeEnum.None, TooltipText = "Speak with this god: ask, bargain or threaten" };
        var speaker = entry.GodId;
        speak.Pressed += () => SpeakClicked?.Invoke(speaker);
        buttons.AddChild(speak);
        foreach (var move in entry.Available)
        {
            var button = new Button { Text = DiplomacyText.Command(move), FocusMode = Control.FocusModeEnum.None };
            var god = entry.GodId;
            button.Pressed += () => CommandClicked?.Invoke(god, move);
            buttons.AddChild(button);
        }

        var cardGod = entry.GodId;
        var open = _expanded.Contains(cardGod);
        var more = new Button { Text = open ? "Treaties and requests ▾" : "Treaties and requests ▸", FocusMode = Control.FocusModeEnum.None };
        more.Pressed += () =>
        {
            if (!_expanded.Remove(cardGod))
            {
                _expanded.Add(cardGod);
            }

            _shown = string.Empty;
        };
        buttons.AddChild(more);
        card.AddChild(buttons);
        if (open)
        {
            card.AddChild(Grid(politics.Offers, "No treaty is possible with it now."));
            if (politics.Requests.Count > 0)
            {
                card.AddChild(Grid(politics.Requests, string.Empty));
            }

            if (politics.Aims.Count > 0)
            {
                card.AddChild(Grid(politics.Aims, string.Empty));
            }
        }

        return card;
    }

    /// <summary>The row choosing how long treaties last and whether they are secret.</summary>
    private void BuildTermsRow()
    {
        foreach (var child in _terms.GetChildren())
        {
            child.QueueFree();
        }

        _terms.AddChild(Text("Treaties you propose last", 14, Muted));
        var seconds = TreatyChoice.Durations[_duration];
        var term = new Button { Text = seconds > 0f ? PoliticsText.Duration(seconds) : "until broken", FocusMode = Control.FocusModeEnum.None };
        term.Pressed += () =>
        {
            _duration = (_duration + 1) % TreatyChoice.Durations.Count;
            Choice = Choice with { DurationSeconds = TreatyChoice.Durations[_duration] };
            _shown = string.Empty;
        };
        _terms.AddChild(term);
        var secret = new Button { Text = Choice.Secret ? "in secret" : "in public", FocusMode = Control.FocusModeEnum.None };
        secret.Pressed += () =>
        {
            Choice = Choice with { Secret = !Choice.Secret };
            _shown = string.Empty;
        };
        _terms.AddChild(secret);
    }

    /// <summary>Buttons in three columns, or a note when there are none.</summary>
    private Control Grid(IReadOnlyList<PoliticsAction> actions, string none)
    {
        if (actions.Count == 0)
        {
            return Text(none, 13, Muted);
        }

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var action in actions)
        {
            grid.AddChild(ActionButton(action));
        }

        return grid;
    }

    /// <summary>A button that reports a politics action.</summary>
    private Button ActionButton(PoliticsAction action)
    {
        var button = new Button { Text = action.Label, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += () => ActionClicked?.Invoke(action);
        return button;
    }

    /// <summary>The row of refugee policies, the current one shown pressed.</summary>
    private void BuildRefugeeRow(RefugeePolicy? current)
    {
        foreach (var child in _refugees.GetChildren())
        {
            child.QueueFree();
        }

        _refugees.Visible = current is not null;
        _refugees.AddChild(Text("Migrants arriving among your people:", 14, Muted));
        foreach (var policy in Enum.GetValues<RefugeePolicy>())
        {
            // The current policy reads as chosen (gold), not as unavailable.
            var button = new Button { Text = MigrationText.Policy(policy), FocusMode = Control.FocusModeEnum.None, Disabled = policy == current };
            button.AddThemeColorOverride("font_disabled_color", new Color(1f, 0.84f, 0.38f));
            button.Pressed += () => PolicyClicked?.Invoke(policy);
            _refugees.AddChild(button);
        }
    }

    /// <summary>A label.</summary>
    private static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>A key that changes when anything shown changes.</summary>
    private static IEnumerable<string> Describe(IReadOnlyList<DiplomacyEntry> entries)
    {
        foreach (var entry in entries)
        {
            var politics = entry.Politics;
            yield return $"{entry.GodId}:{entry.Met}:{entry.Status}:{entry.Attitude}:{entry.Explanation}:{entry.Grievances}:{entry.Ties}:{entry.RefugeePolicy}:{entry.Creatures}:{entry.Proposal}:{string.Join(",", entry.Available)}"
                + $":{entry.War}:{entry.Intelligence}:{string.Join(",", politics.Aims.Select(a => a.Label))}"
                + $":{string.Join(",", politics.Treaties.Select(t => t.Text))}:{politics.Others}:{politics.Favours}:{politics.Proposal}:{string.Join(",", politics.Offers.Select(o => o.Label))}:{string.Join(",", politics.Requests.Select(r => r.Label))}";
        }
    }
}
