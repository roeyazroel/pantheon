using Godot;

namespace Pantheon.Client.UI.Flow;

/// <summary>The key map in play (ADR-068), opened by the god's bar's Keys button; Esc or Close puts it away.</summary>
internal sealed partial class KeysPanel : CanvasLayer
{
    private const float MapHeight = 560f;

    private PanelContainer _panel = null!;
    private VBoxContainer _column = null!;
    private Control? _map;
    private bool? _mapForDebug;

    /// <summary>Whether the debug tools are on, for the debug group.</summary>
    public System.Func<bool> DebugTools { get; set; } = static () => false;

    /// <summary>Whether the panel is shown; showing it builds the map for the debug tools' state.</summary>
    public bool PanelVisible
    {
        get => _panel.Visible;
        set
        {
            if (value)
            {
                Fill();
            }

            _panel.Visible = value;
        }
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        Layer = 4;
        _panel = UiKit.CentredPanel(this, 760, out _column);
        _panel.Visible = false;
        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 12);
        var title = UiKit.Text("The Keys", 20, UiKit.Gold);
        title.AddThemeFontOverride("font", UiTheme.Title);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        heading.AddChild(UiKit.Button("Close (Esc)", () => PanelVisible = false));
        _column.AddChild(heading);
    }

    /// <summary>Builds the map again when the debug tools were turned on or off since.</summary>
    private void Fill()
    {
        var debug = DebugTools();
        if (_mapForDebug == debug && _map is not null)
        {
            return;
        }

        _map?.QueueFree();
        _map = KeysPage.Build(debug, MapHeight);
        _mapForDebug = debug;
        _column.AddChild(_map);
    }
}
