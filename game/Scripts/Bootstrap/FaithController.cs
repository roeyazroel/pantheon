using System;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.Presentation.HolySites;
using Pantheon.Client.UI;
using Pantheon.Game;
using Pantheon.Game.Domains;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Domains and holy sites in the client (ADR-021): the Domains panel (G), the holy sites in the
/// world, the gods' territories on the land (ADR-069), and the launch options that try them out (<c>--rank</c>, <c>--domains</c>,
/// <c>--domains-panel</c>). It reads the session and sends the player's choices to it.
/// </summary>
internal sealed partial class FaithController : Node
{
    private const float SiteDistance = 160f;

    private GameSession _session = null!;
    private GodCameraController _camera = null!;
    private Action<string> _setStatus = null!;
    private DomainPanel _panel = null!;
    private HolySitePresenter _sites = null!;
    private Presentation.Divine.TerritoryPresenter _territories = null!;

    /// <summary>Raised with a Divine Act chosen in the panel, to aim it like a miracle (ADR-030).</summary>
    public event Action<string>? ActChosen;

    /// <summary>Connects the panel and the sites to the session and applies the launch options.</summary>
    /// <param name="session">The session.</param>
    /// <param name="layer">Node that holds the holy sites.</param>
    /// <param name="view">God colours.</param>
    /// <param name="camera">The camera, to look at a site.</param>
    /// <param name="setStatus">Shows a status line.</param>
    /// <param name="options">Launch options.</param>
    public void Setup(GameSession session, Node3D layer, DivineViewSettings view, GodCameraController camera, Action<string> setStatus, LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        (_session, _camera, _setStatus) = (session, camera, setStatus);
        _panel = new DomainPanel { Name = "Domains" };
        AddChild(_panel);
        _panel.DomainChosen += domain => _setStatus(DomainCommands.Choose(_session, domain));
        _panel.SiteClicked += position => _camera.FocusOn(position, SiteDistance, snap: false);
        _panel.ActChosen += act =>
        {
            _panel.PanelVisible = false;
            ActChosen?.Invoke(act);
        };
        _sites = new HolySitePresenter(layer, view);
        _territories = new Presentation.Divine.TerritoryPresenter(Presentation.NodeExtensions.AddLayer(layer.GetParent<Node3D>(), "Territories"), view);
        session.WorldReplaced += Rebuild;
        Rebuild();
        if (options.Rank is { } rank && session.LocalGod is { } god && !session.IsReplica && rank > god.Rank)
        {
            god.Rank = rank;
        }

        foreach (var domain in options.Domains)
        {
            _setStatus(DomainCommands.Choose(session, domain));
        }

        CallDeferred(nameof(OpenPanel), options.DomainsPanel);
        if (options.FocusSite is { } site)
        {
            CallDeferred(nameof(LookAtSite), site, options.CameraDistance ?? SiteDistance);
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_session is null)
        {
            return;
        }

        _sites.Sync(_session.World);
        _territories.Sync(_session, _camera.Model.Distance, delta);
        if (_panel.PanelVisible)
        {
            _panel.ShowModel(DomainReadout.Build(_session));
        }
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session is not null && @event.IsActionPressed(InputActions.ToggleDomains))
        {
            _panel.PanelVisible = !_panel.PanelVisible;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Opens the panel when the options ask for it, once it is ready.</summary>
    private void OpenPanel(bool open) => _panel.PanelVisible = open;

    /// <summary>Points the camera at a holy site, the first being 1.</summary>
    private void LookAtSite(int site, float distance)
    {
        var sites = _session.World.HolySites;
        if (sites.Count > 0)
        {
            _camera.FocusOn(sites[System.Math.Clamp(site, 1, sites.Count) - 1].Position, distance, snap: true);
        }
    }

    /// <summary>Rebuilds the sites for a new or loaded world.</summary>
    private void Rebuild()
    {
        _sites.Rebuild(_session.World, _session.Terrain);
        _territories?.Reset();
    }
}
