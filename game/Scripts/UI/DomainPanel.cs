using System;
using System.Linq;
using Godot;
using Pantheon.Client.UI.Flow;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game.Domains;

namespace Pantheon.Client.UI;

/// <summary>
/// The Domains panel (spec §57: Domains; G toggles it, ADR-021): how many domains the god holds and
/// may hold, every domain with what it stands for, its boon and its signature miracle, a button to
/// take one up when a slot is free, and the world's holy sites with who keeps them. It shows what
/// <see cref="DomainReadout"/> builds and reports clicks; it holds no game state.
/// </summary>
public partial class DomainPanel : CanvasLayer
{
    private const int Columns = 4;

    private PanelContainer _panel = null!;
    private Label _slots = null!;
    private GridContainer _domains = null!;
    private VBoxContainer _sites = null!;
    private ScrollContainer _scroll = null!;
    private string _shown = string.Empty;

    /// <summary>The player chose to take up a domain.</summary>
    public event Action<GodDomain>? DomainChosen;

    /// <summary>The player asked to look at a holy site.</summary>
    public event Action<GroundPosition>? SiteClicked;

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
        _panel = UiKit.CentredPanel(this, 1060, out var outer);
        _panel.Visible = false;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(1020, 0) };
        outer.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);
        _scroll = scroll;
        column.AddChild(UiKit.Text("Domains and holy sites  (G to close)", 20, UiKit.Gold));
        _slots = UiKit.Text(string.Empty, 15, UiKit.Ink);
        column.AddChild(_slots);
        _domains = new GridContainer { Columns = Columns };
        _domains.AddThemeConstantOverride("h_separation", 10);
        _domains.AddThemeConstantOverride("v_separation", 10);
        column.AddChild(_domains);
        column.AddChild(UiKit.Text("Holy sites: the god whose influence holds one long enough keeps it, and gains its power and gift.", 15, UiKit.Gold));
        _sites = new VBoxContainer();
        _sites.AddThemeConstantOverride("separation", 4);
        column.AddChild(_sites);
    }

    /// <summary>Shows the panel's model; the cards are rebuilt only when something changed.</summary>
    /// <param name="model">The model.</param>
    public void ShowModel(DomainPanelModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var signature = model.Slots + string.Concat(model.Domains.Select(d => $"{d.Domain}{d.Held}{d.CanChoose}")) + string.Concat(model.Sites.Select(s => s.State));
        if (signature == _shown)
        {
            return;
        }

        _shown = signature;
        _scroll.CustomMinimumSize = new Vector2(1020, Math.Max(200f, GetViewport().GetVisibleRect().Size.Y - 90f));
        _slots.Text = model.Slots;
        UiKit.Clear(_domains);
        foreach (var domain in model.Domains)
        {
            _domains.AddChild(Card(domain));
        }

        UiKit.Clear(_sites);
        foreach (var site in model.Sites)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var text = UiKit.Text($"{site.Name}: {site.State}. {site.Description}", 13, site.Yours ? UiKit.Good : UiKit.Ink);
            text.CustomMinimumSize = new Vector2(900, 0);
            row.AddChild(text);
            var position = site.Position;
            row.AddChild(UiKit.Button("Look", () => SiteClicked?.Invoke(position)));
            _sites.AddChild(row);
        }
    }

    /// <summary>One domain's card: gold when held, with a button when it may be taken up.</summary>
    private Control Card(DomainEntry domain)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(240, 0), TooltipText = domain.Description };
        var style = UiKit.Style(domain.Held ? new Color(0.16f, 0.13f, 0.05f, 0.95f) : new Color(0.08f, 0.08f, 0.11f, 0.95f));
        (style.ContentMarginLeft, style.ContentMarginRight, style.ContentMarginTop, style.ContentMarginBottom) = (10, 10, 8, 8);
        card.AddThemeStyleboxOverride("panel", style);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        card.AddChild(column);
        column.AddChild(UiKit.Text(domain.Held ? domain.Name + " (yours)" : domain.Name, 16, domain.Held ? UiKit.Gold : UiKit.Ink));
        column.AddChild(UiKit.Text(domain.Boon, 12, UiKit.Ink));
        column.AddChild(UiKit.Text(domain.Signature, 11, UiKit.Muted));
        if (domain.CanChoose)
        {
            var chosen = domain.Domain;
            column.AddChild(UiKit.Button("Take up " + domain.Name, () => DomainChosen?.Invoke(chosen)));
        }

        return card;
    }
}
