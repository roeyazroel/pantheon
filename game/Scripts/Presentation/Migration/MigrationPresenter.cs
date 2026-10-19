using System.Collections.Generic;
using Godot;
using Pantheon.Client.Presentation.Notables;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Migration;

/// <summary>
/// Builds one <see cref="MigrationView"/> per group on the road (ADR-016) and drops it when the group
/// arrives; the prophets and elders who walk the same roads (ADR-032) come with it.
/// </summary>
public sealed class MigrationPresenter
{
    private readonly Node3D _parent;
    private readonly DivineViewSettings _view;
    private readonly Dictionary<int, MigrationView> _views = [];
    private readonly HashSet<int> _seen = [];
    private readonly NotablePresenter _notables;
    private TerrainField? _terrain;

    /// <summary>Creates the presenter; nothing is shown until <see cref="Rebuild"/>.</summary>
    /// <param name="parent">Node that holds the groups.</param>
    /// <param name="view">God colours.</param>
    public MigrationPresenter(Node3D parent, DivineViewSettings view)
    {
        _parent = parent;
        _view = view;
        _notables = new NotablePresenter(parent, view);
    }

    /// <summary>Drops every view, for a new or loaded world.</summary>
    /// <param name="terrain">Terrain of the new world.</param>
    public void Rebuild(TerrainField terrain)
    {
        _parent.FreeChildren();
        _views.Clear();
        _notables.Clear();
        _terrain = terrain;
    }

    /// <summary>Creates, moves and removes the views of the groups on the road.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="running">Whether the simulation clock is running.</param>
    public void Sync(WorldState world, float delta, bool running)
    {
        if (_terrain is null)
        {
            return;
        }

        _notables.Sync(world, _terrain, delta, running);
        _seen.Clear();
        foreach (var group in world.Migration.Groups)
        {
            _seen.Add(group.Id);
            var color = (MigrationModel.MainGod(group.Beliefs) is { } god ? _view.GodColor(god) : _view.UnaffiliatedColor).ToGodot();
            if (!_views.TryGetValue(group.Id, out var view) || view.Color != color)
            {
                view?.Free();
                view = new MigrationView(_parent, group.Id, color);
                _views[group.Id] = view;
            }

            view.Sync(group, world, _terrain, delta, running);
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
