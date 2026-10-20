using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.UI;
using Pantheon.Client.UI.Flow;
using Pantheon.Game.Controls;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Every command in view (ADR-068): the god's bar, its map chooser, the key map, the debug palette,
/// the Veil toggle and the rivals under the plaque. A button presses its input action, so it runs
/// through the very handlers its key does; the bar lights the buttons whose panels are open.
/// </summary>
internal sealed partial class CommandController : Node
{
    private readonly List<(BarCommand Command, Func<bool> Shown)> _panels = [];
    private Hud _hud = null!;
    private DivineController _divine = null!;
    private GodCameraController _camera = null!;
    private KeysPanel _keys = null!;
    private DebugPalette _palette = null!;

    /// <summary>Connects the bar and the toggles to the game.</summary>
    /// <param name="hud">The HUD, with the bar.</param>
    /// <param name="divine">The miracles and overlays.</param>
    /// <param name="politics">The diplomacy panel's owner, for the rivals.</param>
    /// <param name="camera">The camera, for the overview's light.</param>
    /// <param name="press">The bar's button to press at the start (<c>--bar</c>); none when <c>null</c>.</param>
    public void Setup(Hud hud, DivineController divine, PoliticsController politics, GodCameraController camera, BarCommand? press)
    {
        ArgumentNullException.ThrowIfNull(hud);
        ArgumentNullException.ThrowIfNull(politics);
        (_hud, _divine, _camera) = (hud, divine, camera);
        _keys = new KeysPanel { Name = "Keys", DebugTools = () => DebugTools.On };
        AddChild(_keys);
        _palette = new DebugPalette { Name = "DebugPalette" };
        AddChild(_palette);
        _palette.ActionPressed += Press;
        hud.Bar.Refresh(DebugTools.On);
        hud.Bar.Clicked += Run;
        hud.Bar.OverlayChosen += divine.ShowOverlay;
        hud.VeilClicked += () => Press("toggle_veil");
        hud.RivalClicked += _ => politics.OpenDiplomacy();
        DebugTools.Changed += DebugToolsChanged;
        Callable.From(FindPanels).CallDeferred();
        if (press is { } button)
        {
            Callable.From(() => Run(button)).CallDeferred();
        }
    }

    /// <inheritdoc />
    public override void _ExitTree() => DebugTools.Changed -= DebugToolsChanged;

    /// <summary>Lights the bar's buttons whose panels are open.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        if (_hud is null)
        {
            return;
        }

        foreach (var (command, shown) in _panels)
        {
            _hud.Bar.Light(command, shown());
        }

        _hud.Bar.Light(BarCommand.Map, _divine.Overlay is not null || _hud.Bar.ChooserOpen);
        _hud.Bar.Light(BarCommand.Overview, _camera.Model.InOverview);
        _hud.Bar.Light(BarCommand.Keys, _keys.PanelVisible);
        _hud.Bar.Light(BarCommand.Debug, _palette.PanelVisible);
    }

    /// <summary>Esc puts away the key map, the palette or the map's chooser before anything else.</summary>
    /// <param name="event">Input event not consumed by the interface.</param>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_hud is null || !@event.IsActionPressed(InputActions.GameMenu) || _divine.Aiming)
        {
            return;
        }

        if (_keys.PanelVisible || _palette.PanelVisible || _hud.Bar.ChooserOpen)
        {
            _keys.PanelVisible = false;
            _palette.PanelVisible = false;
            if (_hud.Bar.ChooserOpen)
            {
                _hud.Bar.ToggleChooser([], null);
            }

            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Presses an input action, as its key would.</summary>
    /// <param name="action">The action.</param>
    private static void Press(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    /// <summary>Runs a button of the god's bar.</summary>
    private void Run(BarCommand command)
    {
        switch (command)
        {
            case BarCommand.Map:
                _hud.Bar.ToggleChooser(OverlayChoices.Offered(DebugTools.On), _divine.Overlay);
                break;
            case BarCommand.Keys:
                _keys.PanelVisible = !_keys.PanelVisible;
                break;
            case BarCommand.Debug:
                _palette.PanelVisible = !_palette.PanelVisible;
                break;
            case BarCommand.Book or BarCommand.Villages or BarCommand.Gods or BarCommand.Council or BarCommand.Domains or BarCommand.History or BarCommand.Overview or BarCommand.Menu:
                if (System.Linq.Enumerable.FirstOrDefault(CommandBar.Buttons(DebugTools.On), b => b.Command == command)?.Action is { } action)
                {
                    Press(action);
                }

                break;
        }
    }

    /// <summary>The bar follows the debug tools being turned on or off; the palette goes with them.</summary>
    private void DebugToolsChanged()
    {
        _hud.Bar.Refresh(DebugTools.On);
        _palette.PanelVisible &= DebugTools.On;
    }

    /// <summary>Finds the panels the bar opens, once every controller has made its own.</summary>
    private void FindPanels()
    {
        var pending = new Stack<Node>([GetParent()]);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (Shown(node) is { } panel)
            {
                _panels.Add(panel);
            }

            foreach (var child in node.GetChildren())
            {
                pending.Push(child);
            }
        }
    }

    /// <summary>The bar's button a panel lights, and whether it is open.</summary>
    private static (BarCommand, Func<bool>)? Shown(Node node) => node switch
    {
        BookPanel book => (BarCommand.Book, () => book.PanelVisible),
        SettlementPanel settlement => (BarCommand.Villages, () => settlement.PanelVisible),
        DiplomacyPanel diplomacy => (BarCommand.Gods, () => diplomacy.PanelVisible),
        CouncilPanel council => (BarCommand.Council, () => council.PanelVisible),
        DomainPanel domains => (BarCommand.Domains, () => domains.PanelVisible),
        HistoryPanel history => (BarCommand.History, () => history.PanelVisible),
        _ => null,
    };
}
