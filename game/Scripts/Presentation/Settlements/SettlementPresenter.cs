using System.Collections.Generic;
using Godot;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>Builds one <see cref="SettlementView"/> per settlement in the world and keeps them in sync.</summary>
public sealed class SettlementPresenter
{
    private readonly Node3D _parent;
    private readonly SettlementRules _rules;
    private readonly SettlementViewSettings _settings;
    private readonly DivineViewSettings _divine;
    private readonly SettlementAssets _assets = new();
    private readonly Dictionary<int, SettlementView> _views = [];
    private TerrainField? _terrain;
    private WorldSeed _seed;

    /// <summary>Creates the presenter; nothing is built until <see cref="Build"/>.</summary>
    /// <param name="parent">Node that will hold the settlements.</param>
    /// <param name="rules">Settlement rules, for the house count, construction progress and labour split.</param>
    /// <param name="settings">Settlement presentation settings.</param>
    /// <param name="divine">God colours, for the labels.</param>
    public SettlementPresenter(Node3D parent, SettlementRules rules, SettlementViewSettings settings, DivineViewSettings divine)
    {
        _parent = parent;
        _rules = rules;
        _settings = settings;
        _divine = divine;
    }

    /// <summary>Tier and representatives of every settlement after the latest <see cref="UpdateCitizens"/>.</summary>
    public IReadOnlyList<CrowdStatus> CrowdStatuses { get; private set; } = [];

    /// <summary>Replaces all settlement visuals with ones built from the world.</summary>
    /// <param name="world">World whose settlements are shown.</param>
    /// <param name="terrain">Terrain they stand on.</param>
    public void Build(WorldState world, TerrainField terrain)
    {
        _parent.FreeChildren();
        _views.Clear();
        CrowdStatuses = [];
        _terrain = terrain;
        _seed = world.Seed;
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
                view.Sync(settlement, world);
            }
            else
            {
                _views[settlement.Id] = new SettlementView(_parent, _assets, _seed, settlement, _terrain, _rules, _settings, _divine, world);
            }
        }
    }

    /// <summary>Chooses each settlement's tier from the camera and moves the representative citizens of those in Tier 2.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="simulationSeconds">Simulation time, which drives the citizens' routines.</param>
    /// <param name="camera">Camera position in the world.</param>
    public void UpdateCitizens(WorldState world, double simulationSeconds, Vector3 camera)
    {
        var statuses = new List<CrowdStatus>(world.Settlements.Count);
        foreach (var settlement in world.Settlements)
        {
            if (_views.TryGetValue(settlement.Id, out var view))
            {
                statuses.Add(view.UpdateCitizens(settlement, simulationSeconds, camera, world.Villagers));
            }
        }

        CrowdStatuses = statuses;
    }
}
