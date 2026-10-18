using System.Collections.Generic;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>Builds one <see cref="SettlementView"/> per settlement in the world and keeps them in sync.</summary>
public sealed class SettlementPresenter
{
    private readonly Node3D _parent;
    private readonly SettlementAssets _assets = new();
    private readonly Dictionary<int, SettlementView> _views = [];
    private TerrainField? _field;

    /// <summary>Creates the presenter; nothing is built until <see cref="Build"/>.</summary>
    /// <param name="parent">Node that will hold the settlements.</param>
    public SettlementPresenter(Node3D parent) => _parent = parent;

    /// <summary>Replaces all settlement visuals with ones built from the world.</summary>
    /// <param name="world">World whose settlements are shown.</param>
    /// <param name="field">Terrain they stand on.</param>
    public void Build(WorldState world, TerrainField field)
    {
        _parent.FreeChildren();
        _views.Clear();
        _field = field;
        Sync(world);
    }

    /// <summary>Updates every view from the current state, creating views for settlements that have none yet.</summary>
    /// <param name="world">Current world.</param>
    public void Sync(WorldState world)
    {
        if (_field is null)
        {
            return;
        }

        foreach (var settlement in world.Settlements)
        {
            if (_views.TryGetValue(settlement.Id, out var view))
            {
                view.Sync(settlement);
            }
            else
            {
                _views[settlement.Id] = new SettlementView(_parent, _assets, settlement, _field);
            }
        }
    }
}
