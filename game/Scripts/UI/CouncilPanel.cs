using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Game.Politics;

namespace Pantheon.Client.UI;

/// <summary>
/// The council panel (spec §42, §57; U toggles it): the motion in session, the votes, the player's
/// ballot, the decrees in force, the motions waiting and the motions the player may table. It
/// shows what <see cref="CouncilReadout"/> builds and reports clicks; it holds no game state.
/// </summary>
public partial class CouncilPanel : CanvasLayer
{
    private static readonly Color Ink = new(0.95f, 0.93f, 0.86f);
    private static readonly Color Muted = new(0.7f, 0.7f, 0.68f);
    private static readonly Color Gold = new(1f, 0.84f, 0.38f);

    private PanelContainer _panel = null!;
    private VBoxContainer _body = null!;
    private string _shown = string.Empty;

    /// <summary>The player clicked a vote or a motion.</summary>
    public event Action<PoliticsAction>? ActionClicked;

    /// <summary>Whether the panel is shown.</summary>
    public bool PanelVisible
    {
        get => _panel.Visible;
        set => _panel.Visible = value;
    }

    /// <summary>Whether the player's motions ask for a secret ballot (panel state, not game state).</summary>
    public bool SecretBallot { get; private set; }

    /// <summary>Builds the (hidden) panel.</summary>
    public override void _Ready()
    {
        Layer = 3;
        _panel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(620, 0) };
        var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.08f, 0.92f), BorderColor = new Color(0.5f, 0.42f, 0.25f), ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10 };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(8);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.Minsize);
        _panel.GrowHorizontal = Control.GrowDirection.Both;
        _panel.GrowVertical = Control.GrowDirection.Both;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(column);
        var title = Text("The Divine Council  (U to close)", 20, Gold);
        column.AddChild(title);
        _body = new VBoxContainer();
        _body.AddThemeConstantOverride("separation", 6);
        column.AddChild(_body);
    }

    /// <summary>Shows the model; the panel is rebuilt only when something changed.</summary>
    /// <param name="model">The council's model.</param>
    public void ShowModel(CouncilModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var key = string.Join("|", model.Session, model.Votes, model.Note, model.Waiting, SecretBallot, string.Join(",", model.Decrees), string.Join(",", Labels(model.Ballot)), string.Join(",", Labels(model.Motions)));
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        foreach (var child in _body.GetChildren())
        {
            child.QueueFree();
        }

        if (model.Session.Length > 0)
        {
            _body.AddChild(Wrapped(model.Session, 16, Ink));
            _body.AddChild(Wrapped(model.Votes, 14, Muted));
        }

        _body.AddChild(Wrapped(model.Note, 14, new Color(0.62f, 0.8f, 1f)));
        _body.AddChild(Buttons(model.Ballot));
        _body.AddChild(Text("Decrees in force", 15, Gold));
        foreach (var decree in model.Decrees.Count == 0 ? ["None."] : model.Decrees)
        {
            _body.AddChild(Wrapped(decree, 14, Ink));
        }

        _body.AddChild(Wrapped(model.Waiting, 14, Muted));
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        header.AddChild(Text("Table a motion for the next session:", 15, Gold));
        var secret = new Button { Text = SecretBallot ? "Secret ballot: on" : "Secret ballot: off", FocusMode = Control.FocusModeEnum.None };
        secret.Pressed += () =>
        {
            SecretBallot = !SecretBallot;
            _shown = string.Empty;
        };
        header.AddChild(secret);
        _body.AddChild(header);
        _body.AddChild(Grid(model.Motions));
    }

    /// <summary>A row of buttons.</summary>
    private HBoxContainer Buttons(IReadOnlyList<PoliticsAction> actions)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        foreach (var action in actions)
        {
            row.AddChild(Button(action));
        }

        return row;
    }

    /// <summary>Buttons in three columns.</summary>
    private GridContainer Grid(IReadOnlyList<PoliticsAction> actions)
    {
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var action in actions)
        {
            grid.AddChild(Button(action));
        }

        return grid;
    }

    /// <summary>A button that reports its action.</summary>
    private Button Button(PoliticsAction action)
    {
        var button = new Button { Text = action.Label, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += () => ActionClicked?.Invoke(action);
        return button;
    }

    /// <summary>A label that wraps within the panel.</summary>
    private static Label Wrapped(string text, int size, Color color)
    {
        var label = Text(text, size, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(590, 0);
        return label;
    }

    /// <summary>A label.</summary>
    private static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>The labels of actions, for change detection.</summary>
    private static IEnumerable<string> Labels(IReadOnlyList<PoliticsAction> actions)
    {
        foreach (var action in actions)
        {
            yield return action.Label;
        }
    }
}
