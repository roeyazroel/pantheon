using System;
using System.Linq;
using Godot;
using Pantheon.Client.UI.Flow;
using Pantheon.Game.Book;

namespace Pantheon.Client.UI;

/// <summary>
/// The Book of the World (spec §57: World, Religion, Miracles and Creature; Tab opens it, ADR-031):
/// a tab per page and the page's lines, with Aim beside every miracle that may be cast now. It shows
/// what <see cref="BookReadout"/> builds and reports clicks; it holds no game state.
/// </summary>
public partial class BookPanel : CanvasLayer
{
    private PanelContainer _panel = null!;
    private HBoxContainer _tabs = null!;
    private VBoxContainer _lines = null!;
    private ScrollContainer _scroll = null!;
    private string _shown = string.Empty;

    /// <summary>The player chose a page.</summary>
    public event Action<BookTab>? TabChosen;

    /// <summary>The player chose a miracle to aim.</summary>
    public event Action<string>? MiracleAimed;

    /// <summary>Whether the book is open.</summary>
    public bool PanelVisible
    {
        get => _panel.Visible;
        set
        {
            _panel.Visible = value;
            _shown = string.Empty;
        }
    }

    /// <summary>Builds the (closed) book.</summary>
    public override void _Ready()
    {
        Layer = 3;
        _panel = UiKit.CentredPanel(this, 1060, out var outer);
        _panel.Visible = false;
        outer.AddChild(UiKit.Text("The Book of the World  (Tab to close)", 20, UiKit.Gold));
        _tabs = new HBoxContainer();
        _tabs.AddThemeConstantOverride("separation", 8);
        outer.AddChild(_tabs);
        foreach (var tab in Enum.GetValues<BookTab>())
        {
            var chosen = tab;
            _tabs.AddChild(UiKit.Button(BookReadout.Name(tab), () => TabChosen?.Invoke(chosen)));
        }

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(1020, 0) };
        outer.AddChild(_scroll);
        _lines = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _lines.AddThemeConstantOverride("separation", 3);
        _scroll.AddChild(_lines);
    }

    /// <summary>Shows a page; its lines are rebuilt only when something changed.</summary>
    /// <param name="page">The page.</param>
    public void ShowPage(BookPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var signature = page.Tab + string.Concat(page.Lines.Select(l => l.Text + l.CanAim));
        if (signature == _shown)
        {
            return;
        }

        _shown = signature;
        _scroll.CustomMinimumSize = new Vector2(1020, Math.Max(200f, GetViewport().GetVisibleRect().Size.Y - 150f));
        foreach (var (button, tab) in _tabs.GetChildren().OfType<Button>().Zip(Enum.GetValues<BookTab>()))
        {
            button.Disabled = tab == page.Tab;
        }

        UiKit.Clear(_lines);
        foreach (var line in page.Lines)
        {
            _lines.AddChild(Line(line));
        }
    }

    /// <summary>One line: a heading, text, or a miracle with Aim.</summary>
    private Control Line(BookLine line)
    {
        var (size, color) = line.Tone switch
        {
            BookTone.Heading => (16, UiKit.Gold),
            BookTone.Plain => (13, UiKit.Ink),
            BookTone.Good => (13, UiKit.Good),
            BookTone.Warning => (13, UiKit.Bad),
            BookTone.Muted => (13, UiKit.Muted),
        };
        var text = UiKit.Text(line.Text, size, color);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(930, 0);
        if (line.MiracleId is not { } miracle)
        {
            return text;
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(text);
        var aim = UiKit.Button("Aim", () => MiracleAimed?.Invoke(miracle));
        aim.Disabled = !line.CanAim;
        row.AddChild(aim);
        return row;
    }
}
