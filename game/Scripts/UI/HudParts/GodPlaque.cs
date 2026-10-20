using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Pantheon.Domain.Diplomacy;
using Pantheon.Game.Hud;

namespace Pantheon.Client.UI;

/// <summary>
/// The top left of the HUD (ADR-059): the god's name and rank, its Divine Power and what it earns,
/// its followers on the way to the next rank, and below, a chip for every rival god.
/// </summary>
internal sealed partial class GodPlaque : VBoxContainer
{
    private readonly Func<int, Color> _godColor;
    private readonly Label _name;
    private readonly Label _rank;
    private readonly Label _power;
    private readonly Label _income;
    private readonly Label _followers;
    private readonly Label _next;
    private readonly Meter _toNext;
    private readonly VBoxContainer _rivals;
    private readonly ColorRect _emblem;
    private readonly List<(Label Name, Label Status)> _chips = [];
    private IReadOnlyList<RivalChip> _shownRivals = [];

    /// <summary>Builds the plaque.</summary>
    /// <param name="godColor">A god's colour by its id.</param>
    public GodPlaque(Func<int, Color> godColor)
    {
        _godColor = godColor;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 6);
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(330f, 0f) };
        AddChild(panel);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 2);
        panel.AddChild(column);

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 10);
        column.AddChild(header);
        _emblem = new ColorRect { CustomMinimumSize = new Vector2(6f, 40f), MouseFilter = MouseFilterEnum.Ignore };
        header.AddChild(_emblem);
        var names = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        names.AddThemeConstantOverride("separation", -4);
        header.AddChild(names);
        _name = UiTheme.Label(names, 26, UiTheme.Gold, UiTheme.Title);
        _rank = UiTheme.Label(names, UiTheme.SmallSize, UiTheme.Muted, UiTheme.Italic);

        var powerRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, TooltipText = "Divine Power: what your miracles cost. Worship earns it." };
        powerRow.AddThemeConstantOverride("separation", 8);
        column.AddChild(powerRow);
        UiTheme.Label(powerRow, 22, UiTheme.Gold, UiTheme.Strong).Text = "✦";
        _power = UiTheme.Label(powerRow, 30, UiTheme.Gold, UiTheme.Strong);
        _income = UiTheme.Label(powerRow, UiTheme.SmallSize, UiTheme.Good);
        _income.SizeFlagsVertical = SizeFlags.ShrinkEnd;

        var followRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(followRow);
        _followers = UiTheme.Label(followRow, UiTheme.BodySize, UiTheme.Ink);
        _next = UiTheme.Label(followRow, UiTheme.CaptionSize, UiTheme.Muted);
        _next.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _next.HorizontalAlignment = HorizontalAlignment.Right;
        _next.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _toNext = new Meter(6f);
        column.AddChild(_toNext);

        _rivals = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _rivals.AddThemeConstantOverride("separation", 0);
        AddChild(_rivals);
    }

    /// <summary>Shows the model; labels change only where their text did.</summary>
    /// <param name="model">The HUD's model.</param>
    /// <param name="godId">The player's god, for its colour.</param>
    public void ShowModel(HudModel model, int? godId)
    {
        ArgumentNullException.ThrowIfNull(model);
        Set(_name, model.GodName.ToUpperInvariant());
        Set(_rank, model.RankName);
        Set(_power, model.Power.ToString("0", CultureInfo.InvariantCulture));
        Set(_income, model.IncomeText);
        Set(_followers, string.Create(CultureInfo.InvariantCulture, $"{model.Followers:N0} followers"));
        Set(_next, model.NextRankName is { } next ? string.Create(CultureInfo.InvariantCulture, $"{next} at {model.NextRankFollowers:N0}") : "the highest rank");
        _toNext.Show(model.NextRankFollowers > 0 ? model.Followers / (float)model.NextRankFollowers : 1f, UiTheme.Gold);
        var color = godId is { } id ? _godColor(id) : UiTheme.Gold;
        if (_emblem.Color != color)
        {
            _emblem.Color = color;
        }

        ShowRivals(model.RivalChips);
    }

    /// <summary>One chip per rival: its colour, name and how it stands with the player; its following in the tooltip.</summary>
    private void ShowRivals(IReadOnlyList<RivalChip> rivals)
    {
        if (ReferenceEquals(rivals, _shownRivals) || Same(rivals, _shownRivals))
        {
            return;
        }

        _shownRivals = rivals;
        while (_chips.Count < rivals.Count)
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass };
            row.AddThemeConstantOverride("separation", 8);
            var back = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
            back.AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(0.05f, 0.06f, 0.08f, 0.7f), new Color(0f, 0f, 0f, 0f), 0, 6, margin: 4));
            back.AddChild(row);
            _rivals.AddChild(back);
            var name = UiTheme.Label(row, UiTheme.SmallSize, UiTheme.Ink, UiTheme.Strong);
            var status = UiTheme.Label(row, UiTheme.CaptionSize, UiTheme.Muted);
            _chips.Add((name, status));
        }

        for (var i = 0; i < _chips.Count; i++)
        {
            var back = (Control)_rivals.GetChild(i);
            back.Visible = i < rivals.Count;
            if (i >= rivals.Count)
            {
                continue;
            }

            var rival = rivals[i];
            var (name, status) = _chips[i];
            name.Text = "● " + rival.Name;
            name.AddThemeColorOverride("font_color", _godColor(rival.GodId).Lightened(0.2f));
            status.Text = StatusText(rival.Status);
            status.AddThemeColorOverride("font_color", StatusColor(rival.Status));
            back.TooltipText = rival.Detail;
        }
    }

    /// <summary>Words for a status.</summary>
    private static string StatusText(DiplomaticStatus? status) => status is not { } known ? "not yet met" : known switch
    {
        DiplomaticStatus.Alliance => "allied",
        DiplomaticStatus.Friendly => "friendly",
        DiplomaticStatus.Neutral => "neutral",
        DiplomaticStatus.Hostile => "hostile",
        DiplomaticStatus.War => "at war",
    };

    /// <summary>A status's colour.</summary>
    private static Color StatusColor(DiplomaticStatus? status) => status is not { } known ? UiTheme.Muted : known switch
    {
        DiplomaticStatus.Alliance or DiplomaticStatus.Friendly => UiTheme.Good,
        DiplomaticStatus.Neutral => UiTheme.Ink,
        DiplomaticStatus.Hostile => UiTheme.Warning,
        DiplomaticStatus.War => UiTheme.Bad,
    };

    /// <summary>Whether two lists of chips say the same.</summary>
    private static bool Same(IReadOnlyList<RivalChip> a, IReadOnlyList<RivalChip> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sets a label's text when it changed.</summary>
    private static void Set(Label label, string text)
    {
        if (label.Text != text)
        {
            label.Text = text;
        }
    }
}
