using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Pantheon.Client.Presentation.Disasters;
using Pantheon.Client.Presentation.Land;
using Pantheon.Client.Presentation.Regions;
using Pantheon.Client.Presentation.Settlements;
using Pantheon.Client.Presentation.Terrain;
using Pantheon.Client.Presentation.Water;
using Pantheon.Domain.Events;
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
    private readonly ForestPresenter _forests;
    private readonly GroundCoverPresenter _groundCover;
    private readonly RoadPresenter _roads;
    private readonly GreatEvents.GreatEventPresenter _greatEvents;
    private readonly RegionBoundaryOverlay _regions;
    private readonly DebrisPresenter _debris;
    private readonly Camera3D _camera;
    private TerrainField? _terrainField;

    /// <summary>Creates one child layer per kind of visual under <paramref name="root"/>.</summary>
    /// <param name="root">Node that holds the world visuals.</param>
    /// <param name="camera">Rendering camera.</param>
    /// <param name="content">Presentation content: palette and settlement view settings.</param>
    /// <param name="rules">Settlement rules, for the house count and construction progress.</param>
    /// <param name="terrainPreference">Terrain renderer preference from the command line.</param>
    public WorldPresenter(Node3D root, Camera3D camera, PresentationContent content, SettlementRules rules, TerrainBackendPreference terrainPreference)
    {
        _palette = content.Palette;
        _terrain = TerrainPresenterFactory.Create(terrainPreference, root.AddLayer("Terrain"), camera, content.SettlementView.Streaming);
        _water = new WaterPresenter(root.AddLayer("Water"));
        _freshWater = new FreshWaterPresenter(root.AddLayer("FreshWater"));
        _settlements = new SettlementPresenter(root.AddLayer("Settlements"), rules, content.SettlementView, content.DivineView);
        _forests = new ForestPresenter(root.AddLayer("Forests"), rules, content.SettlementView.Layout, content.SettlementView.Streaming, content.SettlementView.Trees);
        _groundCover = new GroundCoverPresenter(root.AddLayer("GroundCover"), content.SettlementView, rules, content.Palette.RockSlopeStart);
        _roads = new RoadPresenter(root.AddLayer("Roads"), rules.Roads);
        _greatEvents = new GreatEvents.GreatEventPresenter(root.AddLayer("GreatEvents"), content.DivineView);
        _regions = new RegionBoundaryOverlay(root.AddLayer("Regions"), content.Palette);
        _debris = new DebrisPresenter(root.AddLayer("Debris"), content.SettlementView.Debris, () => _terrainField!);
        _camera = camera;
        Style = content.SettlementView.Style;
        CrowdPace = content.SettlementView.Citizens.PaceCap;
    }

    /// <summary>The crowds' pace cap (ADR-049): the most seconds of their routines in a real second.</summary>
    public float CrowdPace { get; }

    /// <summary>How religion shows in villages (ADR-042), for the debug view.</summary>
    public Pantheon.Game.Presentation.StyleSettings Style { get; }

    /// <summary>Pieces of debris flying or lying, and those still moving (ADR-048).</summary>
    public (int Pieces, int Moving) Debris => (_debris.Count, _debris.Moving);

    /// <summary>What streams near the camera now (ADR-050): planted forest chunks, detailed terrain chunks.</summary>
    public ((int Loaded, int Chunks) Forests, (int Detailed, int Chunks)? Terrain) Streaming => (_forests.Streaming, _terrain.Detail);

    /// <summary>Ground-cover chunks planted near the camera and all of them (ADR-053).</summary>
    public (int Loaded, int Chunks) GroundCover => _groundCover.Streaming;

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
        _forests.Rebuild(world, terrain);
        _groundCover.Rebuild(world, terrain);
        _roads.Rebuild(world, terrain);
        _greatEvents.Rebuild(terrain);
        _regions.Build(world.Regions, terrain);
        _debris.Clear();
        _terrainField = terrain;
        GD.Print($"World visuals for seed {world.Seed} built in {stopwatch.ElapsedMilliseconds} ms ({TerrainBackend}).");
    }

    /// <summary>Tier and representatives of every settlement after the latest <see cref="UpdateCitizens"/>.</summary>
    public IReadOnlyList<CrowdStatus> CrowdStatuses => _settlements.CrowdStatuses;

    /// <summary>Refreshes visuals that follow the simulation, such as settlement labels, houses and fields, the forests and the roads.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Sync(WorldState world, double delta)
    {
        _terrain.Stream(_camera.GlobalPosition);
        _settlements.Sync(world);
        _debris.Houses(_settlements.Fell, _camera.GlobalPosition);
        _debris.Sync(delta);
        _freshWater.Sync(world);
        _forests.Sync(world, delta, _camera.GlobalPosition);
        _groundCover.Sync(world, delta, _camera.GlobalPosition, _forests.Wind);
        _roads.Sync(world);
        _greatEvents.Sync(world, delta);
    }

    /// <summary>Throws up stones where the hand's thrown things or falling stars struck (ADR-048).</summary>
    /// <param name="events">The events handled this frame.</param>
    public void React(IReadOnlyList<WorldEvent> events)
    {
        if (_terrainField is not null)
        {
            _debris.React(events, _camera.GlobalPosition);
        }
    }

    /// <summary>Chooses each settlement's tier from the camera and moves the representative citizens near it.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="simulationSeconds">The crowds' time (ADR-049), which drives the citizens' routines.</param>
    /// <param name="camera">Camera position in the world.</param>
    public void UpdateCitizens(WorldState world, double simulationSeconds, Vector3 camera) =>
        _settlements.UpdateCitizens(world, simulationSeconds, camera);

    /// <summary>Outlines the region under the cursor (only visible while boundaries are shown).</summary>
    /// <param name="regionId">Region id, or <c>null</c> when the cursor is not over the world.</param>
    public void SetHoveredRegion(int? regionId) => _regions.SetHighlightedRegion(regionId);
}
