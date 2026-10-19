using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Migration;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.Migration;

namespace Pantheon.Client.UI;

/// <summary>
/// The diplomacy panel (spec §57: Diplomacy; P toggles it): one card per other god with your
/// status, how it feels about you and why (spec §49), what it has proposed, and buttons for the
/// moves the authority would accept now, and your refugee policy (ADR-016). It shows what
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

    private PanelContainer _panel = null!;
    private HBoxContainer _refugees = null!;
    private VBoxContainer _cards = null!;
    private string _shown = string.Empty;

    /// <summary>The player clicked a move towards a god.</summary>
    public event Action<int, DiplomaticCommandKind>? CommandClicked;

    /// <summary>The player chose a refugee policy.</summary>
    public event Action<RefugeePolicy>? PolicyClicked;

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
        _cards = new VBoxContainer();
        _cards.AddThemeConstantOverride("separation", 12);
        column.AddChild(_cards);
    }

    /// <summary>Shows the entries and your refugee policy; the cards are rebuilt only when something changed.</summary>
    /// <param name="entries">One entry per other god.</param>
    /// <param name="policy">Your refugee policy, or <c>null</c> without a god.</param>
    public void ShowEntries(IReadOnlyList<DiplomacyEntry> entries, RefugeePolicy? policy)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var key = policy + "#" + string.Join("|", Describe(entries));
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        BuildRefugeeRow(policy);
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
        header.AddChild(Text(DiplomacyText.Status(entry.Status), 16, _statusColors[entry.Status]));
        card.AddChild(header);
        var practice = MigrationText.Practice(entry.RefugeePolicy);
        card.AddChild(Text((entry.Ties.Length > 0 ? entry.Ties + ". " : string.Empty) + char.ToUpperInvariant(practice[0]) + practice[1..] + ".", 14, new Color(0.62f, 0.8f, 1f)));

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

        if (entry.Proposal is { } proposal)
        {
            card.AddChild(Text(proposal == ProposalKind.Alliance ? "It proposes an alliance." : "It asks for peace.", 15, new Color(1f, 0.85f, 0.4f)));
        }

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        foreach (var move in entry.Available)
        {
            var button = new Button { Text = DiplomacyText.Command(move), FocusMode = Control.FocusModeEnum.None };
            var god = entry.GodId;
            button.Pressed += () => CommandClicked?.Invoke(god, move);
            buttons.AddChild(button);
        }

        card.AddChild(buttons);
        return card;
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
            yield return $"{entry.GodId}:{entry.Status}:{entry.Attitude}:{entry.Explanation}:{entry.Grievances}:{entry.Ties}:{entry.RefugeePolicy}:{entry.Proposal}:{string.Join(",", entry.Available)}";
        }
    }
}
