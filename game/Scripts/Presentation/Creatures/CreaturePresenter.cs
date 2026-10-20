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

    /// <summary>Starts making the bodies of a world's species on worker threads, so they are ready when first shown (ADR-061).</summary>
    /// <param name="world">The world.</param>
    public void Prepare(WorldState world)
    {
        foreach (var creature in world.Creatures)
        {
            _ = AnimalMeshes.For(_view.LookFor(creature.Species));
        }
    }

    /// <summary>Creates, moves and poses the views of every Creature.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="simulationRunning">Whether the simulation clock is running.</param>
    /// <param name="localGodId">The player's god, whose Creature's label shows no owner.</param>
    /// <param name="heldAt">Where the player's hand holds its Creature, if it does (ADR-030).</param>
    /// <param name="alpha">How far the simulation's clock has run towards the next tick (0..1), to draw between ticks (ADR-061).</param>
    public void Sync(WorldState world, float delta, bool simulationRunning, int? localGodId, Vector3? heldAt = null, float alpha = 1f)
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

            view.Sync(creature, world, _terrain, (delta, simulationRunning, alpha), creature.GodId == localGodId ? heldAt : null);
        }
    }

    /// <summary>Where a Creature is drawn, if it has a view.</summary>
    /// <param name="creatureId">The Creature.</param>
    /// <returns>Its shown position and head height, or <c>null</c>.</returns>
    public (Vector3 Position, float HeadHeight)? Find(int creatureId) =>
        _views.TryGetValue(creatureId, out var view) ? (view.ShownPosition, view.HeadHeight) : null;

    /// <summary>Flashes a Creature's colour, and its face shows the god's word a while.</summary>
    /// <param name="creatureId">The Creature.</param>
    /// <param name="color">Flash colour.</param>
    /// <param name="feedback">The reward or punishment.</param>
    public void Flash(int creatureId, Color color, Pantheon.Domain.Creatures.CreatureFeedback feedback)
    {
        if (_views.TryGetValue(creatureId, out var view))
        {
            view.Flash(color, feedback);
        }
    }
}
