using System.Collections.Generic;
using Godot;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>Builds one <see cref="SettlementView"/> per settlement in the world and keeps them in sync.</summary>
public sealed class SettlementPresenter
{
    private readonly Node3D _parent;
    private readonly SettlementRules _rules;
    private readonly SettlementViewSettings _settings;
    private readonly SettlementAssets _assets = new();
    private readonly Dictionary<int, SettlementView> _views = [];
    private TerrainField? _terrain;

    /// <summary>Creates the presenter; nothing is built until <see cref="Build"/>.</summary>
    /// <param name="parent">Node that will hold the settlements.</param>
    /// <param name="rules">Settlement rules, for the house count and construction progress.</param>
    /// <param name="settings">Settlement presentation settings.</param>
    public SettlementPresenter(Node3D parent, SettlementRules rules, SettlementViewSettings settings)
    {
        _parent = parent;
        _rules = rules;
        _settings = settings;
    }

    /// <summary>Replaces all settlement visuals with ones built from the world.</summary>
    /// <param name="world">World whose settlements are shown.</param>
    /// <param name="terrain">Terrain they stand on.</param>
    public void Build(WorldState world, TerrainField terrain)
    {
        _parent.FreeChildren();
        _views.Clear();
        _terrain = terrain;
        Sync(world);
    }

    /// <summary>Updates every view from the current state, creating views for settlements that have none yet.</summary>
    /// <param name="world">Current world.</param>
    public void Sync(WorldState world)
    {
        if (_terrain is null)
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
                _views[settlement.Id] = new SettlementView(_parent, _assets, settlement, _terrain, _rules, _settings);
            }
        }
    }
}
