using System;
using System.Linq;
using Godot;
using Pantheon.Client.UI.Flow;
using Pantheon.Domain.Society;
using Pantheon.Game.Society;

namespace Pantheon.Client.UI;

/// <summary>
/// The Settlements panel (spec §57: Settlements; N toggles it, ADR-023): one settlement's faiths,
/// stores, society, technology, buildings and work, and what it puts first, with a button for each
/// priority the player may set. It shows what <see cref="SettlementPanelReadout"/> builds and
/// reports clicks; it holds no game state.
/// </summary>
public partial class SettlementPanel : CanvasLayer
{
    private PanelContainer _panel = null!;
    private Label _title = null!;
    private VBoxContainer _lines = null!;
    private Label _priority = null!;
    private Label _note = null!;
    private GridContainer _options = null!;
    private HBoxContainer _actions = null!;
    private string _shown = string.Empty;

    /// <summary>The player clicked a word to a holy war or a proxy act (ADR-026).</summary>
    public event Action<Pantheon.Game.Politics.PoliticsAction>? ActionClicked;

    /// <summary>The player chose a priority for the settlement shown.</summary>
    public event Action<int, SettlementPriority>? PriorityChosen;

    /// <summary>Whether the panel is shown.</summary>
    public bool PanelVisible
    {
        get => _panel.Visible;
        set
        {
            _panel.Visible = value;
            _shown = string.Empty;
        }
    }

    /// <summary>Builds the (hidden) panel.</summary>
    public override void _Ready()
    {
        Layer = 3;
        _panel = UiKit.CentredPanel(this, 900, out var column);
        _panel.Visible = false;
        column.AddThemeConstantOverride("separation", 6);
        _title = UiKit.Text(string.Empty, 20, UiKit.Gold);
        column.AddChild(_title);
        _lines = new VBoxContainer();
        _lines.AddThemeConstantOverride("separation", 2);
        column.AddChild(_lines);
        _priority = UiKit.Text(string.Empty, 16, UiKit.Gold);
        column.AddChild(_priority);
        _note = UiKit.Text(string.Empty, 13, UiKit.Muted);
        column.AddChild(_note);
        _options = new GridContainer { Columns = 3 };
        _options.AddThemeConstantOverride("h_separation", 8);
        _options.AddThemeConstantOverride("v_separation", 8);
        column.AddChild(_options);
        _actions = new HBoxContainer();
        _actions.AddThemeConstantOverride("separation", 8);
        column.AddChild(_actions);
        column.AddChild(UiKit.Text("N closes this panel; point at another village and press N to see it.", 12, UiKit.Muted));
    }

    /// <summary>Shows the panel's model; the controls are rebuilt only when something changed.</summary>
    /// <param name="model">The model.</param>
    public void ShowModel(SettlementPanelModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var signature = model.Title + string.Concat(model.Lines) + model.Priority + model.Note + string.Concat(model.Options.Select(o => $"{o.Priority}{o.Current}{o.Enabled}")) + string.Concat(model.Actions.Select(a => a.Label));
        if (signature == _shown)
        {
            return;
        }

        _shown = signature;
        _title.Text = model.Title + "  (N to close)";
        UiKit.Clear(_lines);
        foreach (var line in model.Lines)
        {
            var label = UiKit.Text(line, 14, UiKit.Ink);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(860, 0);
            _lines.AddChild(label);
        }

        _priority.Text = model.Priority;
        _note.Text = model.Note;
        UiKit.Clear(_options);
        foreach (var option in model.Options)
        {
            var settlement = model.SettlementId;
            var priority = option.Priority;
            var button = UiKit.Button(option.Current ? "• " + option.Name : option.Name, () => PriorityChosen?.Invoke(settlement, priority));
            button.TooltipText = option.Description;
            button.Disabled = !option.Enabled;
            button.CustomMinimumSize = new Vector2(280, 34);
            _options.AddChild(button);
        }

        UiKit.Clear(_actions);
        foreach (var action in model.Actions)
        {
            _actions.AddChild(UiKit.Button(action.Label, () => ActionClicked?.Invoke(action)));
        }
    }
}
