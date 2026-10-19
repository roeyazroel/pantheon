using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Pantheon.Client.Presentation.Regions;
using Pantheon.Client.Presentation.Settlements;
using Pantheon.Client.Presentation.Terrain;
using Pantheon.Client.Presentation.Water;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Launch;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation;

/// <summary>
/// Owns every world visual and builds them from <see cref="WorldState"/> (ADR-006): the nodes are
/// disposable views, recreated by <see cref="Rebuild"/> whenever the world is replaced.
/// </summary>
public sealed class WorldPresenter
{
    private readonly BiomePalette _palette;
    private readonly ITerrainPresenter _terrain;
    private readonly WaterPresenter _water;
    private readonly FreshWaterPresenter _freshWater;
    private readonly SettlementPresenter _settlements;
    private readonly RegionBoundaryOverlay _regions;

    /// <summary>Creates one child layer per kind of visual under <paramref name="root"/>.</summary>
    /// <param name="root">Node that holds the world visuals.</param>
    /// <param name="camera">Rendering camera.</param>
    /// <param name="content">Presentation content: palette and settlement view settings.</param>
    /// <param name="rules">Settlement rules, for the house count and construction progress.</param>
    /// <param name="terrainPreference">Terrain renderer preference from the command line.</param>
    public WorldPresenter(Node3D root, Camera3D camera, PresentationContent content, SettlementRules rules, TerrainBackendPreference terrainPreference)
    {
        _palette = content.Palette;
        _terrain = TerrainPresenterFactory.Create(terrainPreference, root.AddLayer("Terrain"), camera);
        _water = new WaterPresenter(root.AddLayer("Water"));
        _freshWater = new FreshWaterPresenter(root.AddLayer("FreshWater"));
        _settlements = new SettlementPresenter(root.AddLayer("Settlements"), rules, content.SettlementView, content.DivineView);
        _regions = new RegionBoundaryOverlay(root.AddLayer("Regions"), content.Palette);
    }

    /// <summary>Name of the active terrain renderer.</summary>
    public string TerrainBackend => _terrain.BackendName;

    /// <summary>Whether the fallback terrain renderer is active.</summary>
    public bool TerrainIsFallback => _terrain.IsFallback;

    /// <summary>Whether region boundaries are drawn.</summary>
    public bool RegionBoundariesVisible
    {
        get => _regions.Visible;
        set => _regions.Visible = value;
    }

    /// <summary>Discards all world visuals and builds new ones from the state.</summary>
    /// <param name="world">World to show.</param>
    /// <param name="terrain">Terrain derived from the world.</param>
    public void Rebuild(WorldState world, TerrainField terrain)
    {
        var stopwatch = Stopwatch.StartNew();
        _terrain.Build(terrain, _palette);
        _water.Build(terrain, _palette);
        _freshWater.Build(terrain, world);
        _settlements.Build(world, terrain);
        _regions.Build(world.Regions, terrain);
        GD.Print($"World visuals for seed {world.Seed} built in {stopwatch.ElapsedMilliseconds} ms ({TerrainBackend}).");
    }

    /// <summary>Tier and representatives of every settlement after the latest <see cref="UpdateCitizens"/>.</summary>
    public IReadOnlyList<CrowdStatus> CrowdStatuses => _settlements.CrowdStatuses;

    /// <summary>Refreshes visuals that follow the simulation, such as settlement labels, houses and fields.</summary>
    /// <param name="world">Current world.</param>
    public void Sync(WorldState world)
    {
        _settlements.Sync(world);
        _freshWater.Sync(world);
    }

    /// <summary>Chooses each settlement's tier from the camera and moves the representative citizens near it.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="simulationSeconds">Simulation time, which drives the citizens' routines.</param>
    /// <param name="camera">Camera position in the world.</param>
    public void UpdateCitizens(WorldState world, double simulationSeconds, Vector3 camera) =>
        _settlements.UpdateCitizens(world, simulationSeconds, camera);

    /// <summary>Outlines the region under the cursor (only visible while boundaries are shown).</summary>
    /// <param name="regionId">Region id, or <c>null</c> when the cursor is not over the world.</param>
    public void SetHoveredRegion(int? regionId) => _regions.SetHighlightedRegion(regionId);
}
