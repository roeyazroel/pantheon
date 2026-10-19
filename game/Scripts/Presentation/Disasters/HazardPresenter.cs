using System.Collections.Generic;
using Godot;
using Pantheon.Client.Presentation.Divine;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Disasters;

/// <summary>
/// Draws the hazards of ADR-029 from <see cref="WorldState"/>: blight and rubble draped over the
/// land, a funnel for each tornado, a cloud for each locust swarm, the volcano's plume while it
/// rumbles or erupts, and the cracks, slides and craters left on the land, fading with age.
/// </summary>
internal sealed class HazardPresenter
{
    private const double CoverRefreshSeconds = 1.0;
    private const float MarkDepth = 80f;
    private const float SwarmHeight = 18f;
    private const float FollowRate = 3f;

    private readonly Node3D _moving;
    private readonly Node3D _marks;
    private readonly MapOverlay _cover;
    private readonly HazardRules _rules;
    private readonly Dictionary<int, Node3D> _tornadoViews = [];
    private readonly Dictionary<int, GpuParticles3D> _swarmViews = [];
    private readonly Dictionary<(TerrainMarkKind, float, float, long), Decal> _markViews = [];
    private readonly HashSet<int> _seen = [];
    private readonly HashSet<(TerrainMarkKind, float, float, long)> _seenMarks = [];
    private Node3D? _plume;
    private TerrainField? _terrain;
    private float _regionSize;
    private float _worldSize;
    private double _coverAge;

    /// <summary>Creates the presenter's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the hazard visuals.</param>
    /// <param name="rules">Hazard rules, for the reach of tornadoes and swarms and how long marks last.</param>
    public HazardPresenter(Node3D parent, HazardRules rules)
    {
        _moving = parent.AddLayer("Hazards");
        _marks = parent.AddLayer("Marks");
        _cover = new MapOverlay(parent.AddLayer("Blight"));
        _rules = rules;
    }

    /// <summary>Forgets the old world's hazards.</summary>
    /// <param name="terrain">Terrain of the new world.</param>
    /// <param name="regionSize">Side of a region in metres.</param>
    public void Rebuild(TerrainField terrain, float regionSize)
    {
        _terrain = terrain;
        _regionSize = regionSize;
        _moving.FreeChildren();
        _marks.FreeChildren();
        _tornadoViews.Clear();
        _swarmViews.Clear();
        _markViews.Clear();
        _plume = null;
        _worldSize = 0f;
        _coverAge = CoverRefreshSeconds;
    }

    /// <summary>Brings the visuals in line with the hazards.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Sync(WorldState world, float delta)
    {
        SyncCover(world, delta);
        SyncTornadoes(world, delta);
        SyncSwarms(world, delta);
        SyncVolcano(world);
        SyncMarks(world);
    }

    /// <summary>Drapes blight and rubble over the land, redrawn once a second.</summary>
    private void SyncCover(WorldState world, float delta)
    {
        var size = world.Regions.Columns * world.Regions.RegionSize;
        if (size != _worldSize)
        {
            _worldSize = size;
            _cover.Fit(size);
        }

        _coverAge += delta;
        if (_coverAge >= CoverRefreshSeconds)
        {
            _coverAge = 0.0;
            _cover.Show(HazardCover.Build(world));
        }
    }

    /// <summary>A spinning funnel for each tornado, gliding to where it is.</summary>
    private void SyncTornadoes(WorldState world, float delta)
    {
        _seen.Clear();
        foreach (var tornado in world.Disasters.Tornadoes)
        {
            _seen.Add(tornado.Id);
            var target = Ground(tornado.X, tornado.Z);
            if (!_tornadoViews.TryGetValue(tornado.Id, out var view))
            {
                view = HazardViews.Tornado(_rules.Tornado.Radius);
                view.Position = target;
                _tornadoViews[tornado.Id] = view;
                _moving.AddChild(view);
            }

            view.Position = view.Position.Lerp(target, 1f - Mathf.Exp(-FollowRate * delta));
            view.RotateY(4f * delta);
        }

        Prune(_tornadoViews, _seen);
    }

    /// <summary>A cloud for each locust swarm, as thick as the swarm is large.</summary>
    private void SyncSwarms(WorldState world, float delta)
    {
        _seen.Clear();
        foreach (var swarm in world.Disasters.Swarms)
        {
            _seen.Add(swarm.Id);
            var target = Ground(swarm.X, swarm.Z) + new Vector3(0f, SwarmHeight, 0f);
            if (!_swarmViews.TryGetValue(swarm.Id, out var view))
            {
                view = HazardViews.Swarm(_rules.Locusts.Radius);
                view.Position = target;
                _swarmViews[swarm.Id] = view;
                _moving.AddChild(view);
            }

            view.Position = view.Position.Lerp(target, 1f - Mathf.Exp(-FollowRate * delta));
            view.AmountRatio = Mathf.Clamp(swarm.Size, 0.1f, 1f);
        }

        Prune(_swarmViews, _seen);
    }

    /// <summary>The volcano's plume while it rumbles or erupts.</summary>
    private void SyncVolcano(WorldState world)
    {
        var volcano = world.Disasters.Volcano;
        if (volcano is null || volcano.Phase == VolcanoPhase.Dormant)
        {
            _plume?.QueueFree();
            _plume = null;
            return;
        }

        if (_plume is null)
        {
            var crater = world.Regions.GetRegionCenter(volcano.RegionId);
            _plume = HazardViews.Plume(_regionSize);
            _plume.Position = Ground(crater.X, crater.Z);
            _moving.AddChild(_plume);
        }

        HazardViews.ShowPlume(_plume, volcano.Phase == VolcanoPhase.Erupting);
    }

    /// <summary>A dark patch for each mark on the land, fading as it ages.</summary>
    private void SyncMarks(WorldState world)
    {
        _seenMarks.Clear();
        foreach (var mark in world.Disasters.Marks)
        {
            var key = (mark.Kind, mark.X, mark.Z, mark.Tick);
            _seenMarks.Add(key);
            if (!_markViews.TryGetValue(key, out var decal))
            {
                decal = new Decal { Name = mark.Kind.ToString(), TextureAlbedo = Weather.SoftDiscTexture.Texture, Size = new Vector3(mark.Radius * 2f, MarkDepth, mark.Radius * 2f), Position = Ground(mark.X, mark.Z), UpperFade = 0.1f, LowerFade = 0.1f, CullMask = DeclutteredLabel.GroundOnly };
                _markViews[key] = decal;
                _marks.AddChild(decal);
            }

            var color = HazardCover.MarkColor(mark.Kind);
            decal.Modulate = new Color(color.R, color.G, color.B, 0.9f * HazardCover.MarkOpacity(mark, world.Tick, _rules));
        }

        foreach (var key in new List<(TerrainMarkKind, float, float, long)>(_markViews.Keys))
        {
            if (!_seenMarks.Contains(key) && _markViews.Remove(key, out var faded))
            {
                faded.QueueFree();
            }
        }
    }

    /// <summary>Frees the views of hazards that are gone.</summary>
    private static void Prune<TView>(Dictionary<int, TView> views, HashSet<int> seen)
        where TView : Node
    {
        foreach (var id in new List<int>(views.Keys))
        {
            if (!seen.Contains(id) && views.Remove(id, out var gone))
            {
                gone.QueueFree();
            }
        }
    }

    /// <summary>The ground point (or sea surface) below a position.</summary>
    private Vector3 Ground(float x, float z) => new(x, _terrain is null ? 0f : Mathf.Max(_terrain.SampleHeight(x, z), 0f), z);
}
