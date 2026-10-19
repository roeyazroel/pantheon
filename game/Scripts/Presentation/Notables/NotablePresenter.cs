using System.Collections.Generic;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Notables;

/// <summary>Builds one <see cref="NotableView"/> per prophet and elder (ADR-032) and drops it when they die.</summary>
public sealed class NotablePresenter
{
    private readonly Node3D _parent;
    private readonly DivineViewSettings _view;
    private readonly Dictionary<int, NotableView> _views = [];
    private readonly HashSet<int> _seen = [];

    /// <summary>Creates the presenter.</summary>
    /// <param name="parent">Node that holds the notables; whoever owns it frees its children for a new world.</param>
    /// <param name="view">God colours.</param>
    public NotablePresenter(Node3D parent, DivineViewSettings view)
    {
        _parent = parent;
        _view = view;
    }

    /// <summary>Forgets every view, whose nodes the owner has freed, for a new or loaded world.</summary>
    public void Clear() => _views.Clear();

    /// <summary>Creates, moves and removes the views of the notables.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="terrain">Terrain, for the ground height.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="running">Whether the simulation clock is running.</param>
    public void Sync(WorldState world, TerrainField terrain, float delta, bool running)
    {
        _seen.Clear();
        foreach (var notable in world.Notables)
        {
            _seen.Add(notable.Id);
            var color = _view.GodColor(notable.GodId).ToGodot();
            if (!_views.TryGetValue(notable.Id, out var view) || view.Color != color)
            {
                // An elder who turns takes their new god's colour.
                view?.Free();
                view = new NotableView(_parent, notable, color);
                _views[notable.Id] = view;
            }

            view.Sync(notable, world, terrain, delta, running);
        }

        foreach (var id in new List<int>(_views.Keys))
        {
            if (!_seen.Contains(id))
            {
                _views[id].Free();
                _views.Remove(id);
            }
        }
    }
}
