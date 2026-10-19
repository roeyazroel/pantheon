using System.Collections.Generic;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Domains;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.HolySites;

/// <summary>Builds one <see cref="HolySiteView"/> per holy site (ADR-021) and shows who keeps it and who is taking it.</summary>
public sealed class HolySitePresenter
{
    private readonly Node3D _parent;
    private readonly DivineViewSettings _view;
    private readonly Dictionary<int, HolySiteView> _views = [];

    /// <summary>Creates the presenter; nothing is shown until <see cref="Rebuild"/>.</summary>
    /// <param name="parent">Node that holds the sites.</param>
    /// <param name="view">God colours.</param>
    public HolySitePresenter(Node3D parent, DivineViewSettings view)
    {
        _parent = parent;
        _view = view;
    }

    /// <summary>Builds the sites of a new or loaded world on its terrain.</summary>
    /// <param name="world">The world.</param>
    /// <param name="terrain">Its terrain.</param>
    public void Rebuild(WorldState world, TerrainField terrain)
    {
        _parent.FreeChildren();
        _views.Clear();
        foreach (var site in world.HolySites)
        {
            var ground = new Vector3(site.Position.X, terrain.SampleHeight(site.Position.X, site.Position.Z), site.Position.Z);
            var view = HolySiteView.Create(site.Kind, ground);
            _parent.AddChild(view);
            _views[site.Id] = view;
        }
    }

    /// <summary>Shows every site's keeper and claim.</summary>
    /// <param name="world">The world.</param>
    public void Sync(WorldState world)
    {
        foreach (var site in world.HolySites)
        {
            if (!_views.TryGetValue(site.Id, out var view))
            {
                continue;
            }

            var keeper = site.KeeperGodId is { } k ? world.Gods.Find(g => g.Id == k)?.Name : null;
            var text = keeper is null ? site.Name : site.Name + "\n" + keeper;
            view.Show(text, site.KeeperGodId is { } kept ? _view.GodColor(kept).ToGodot() : null, site.ClaimantGodId is { } claimant ? _view.GodColor(claimant).ToGodot() : null);
        }
    }
}
