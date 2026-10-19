using System.Collections.Generic;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>Builds one <see cref="CreatureView"/> per Creature in the world and keeps them in step with it.</summary>
public sealed class CreaturePresenter
{
    private readonly Node3D _parent;
    private readonly DivineViewSettings _view;
    private readonly Dictionary<int, CreatureView> _views = [];
    private TerrainField? _terrain;

    /// <summary>Creates the presenter; nothing is shown until <see cref="Rebuild"/>.</summary>
    /// <param name="parent">Node that holds the Creatures.</param>
    /// <param name="view">God colours.</param>
    public CreaturePresenter(Node3D parent, DivineViewSettings view)
    {
        _parent = parent;
        _view = view;
    }

    /// <summary>Drops every view, for a new or loaded world.</summary>
    /// <param name="terrain">Terrain of the new world.</param>
    public void Rebuild(TerrainField terrain)
    {
        _parent.FreeChildren();
        _views.Clear();
        _terrain = terrain;
    }

    /// <summary>Creates, moves and poses the views of every Creature.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="simulationRunning">Whether the simulation clock is running.</param>
    /// <param name="localGodId">The player's god, whose Creature's label shows no owner.</param>
    public void Sync(WorldState world, float delta, bool simulationRunning, int? localGodId)
    {
        if (_terrain is null)
        {
            return;
        }

        foreach (var creature in world.Creatures)
        {
            if (!_views.TryGetValue(creature.Id, out var view))
            {
                var owner = creature.GodId == localGodId ? string.Empty : world.Gods.Find(g => g.Id == creature.GodId)?.Name ?? string.Empty;
                view = new CreatureView(_parent, creature.Id, _view.GodColor(creature.GodId).ToGodot(), owner, _view.LookFor(creature.Species));
                _views[creature.Id] = view;
            }

            view.Sync(creature, world, _terrain, delta, simulationRunning);
        }
    }

    /// <summary>Where a Creature is drawn, if it has a view.</summary>
    /// <param name="creatureId">The Creature.</param>
    /// <returns>Its shown position and head height, or <c>null</c>.</returns>
    public (Vector3 Position, float HeadHeight)? Find(int creatureId) =>
        _views.TryGetValue(creatureId, out var view) ? (view.ShownPosition, view.HeadHeight) : null;

    /// <summary>Flashes a Creature's colour.</summary>
    /// <param name="creatureId">The Creature.</param>
    /// <param name="color">Flash colour.</param>
    public void Flash(int creatureId, Color color)
    {
        if (_views.TryGetValue(creatureId, out var view))
        {
            view.Flash(color);
        }
    }
}
