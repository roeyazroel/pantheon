using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Land;

/// <summary>
/// The ground cover near the camera (spec §61, §84, ADR-053): grass, flowers, bushes, stones and
/// reeds laid out by <see cref="GroundCoverLayout"/>, streamed in small chunks around the camera,
/// one multimesh per chunk and kind, swaying in the weather's wind. Every few seconds the planted
/// chunks are laid out again, for the snow, the vegetation and the villages' clearings.
/// </summary>
public sealed class GroundCoverPresenter
{
    private const string ShaderPath = "res://Shaders/tree.gdshader";
    private const int ChunksPerFrame = 8;
    private const float RefreshSeconds = 5f;
    private const float FadeMargin = 30f;

    private static readonly Dictionary<GroundCoverKind, Color> Stems = new()
    {
        [GroundCoverKind.Grass] = new Color(0.3f, 0.45f, 0.2f),
        [GroundCoverKind.Flowers] = new Color(0.29f, 0.42f, 0.17f),
        [GroundCoverKind.Bush] = new Color(0.25f, 0.2f, 0.12f),
        [GroundCoverKind.Rock] = new Color(0.4f, 0.4f, 0.38f),
        [GroundCoverKind.Reeds] = new Color(0.42f, 0.29f, 0.17f),
    };

    private readonly Node3D _parent;
    private readonly SettlementViewSettings _view;
    private readonly SettlementRules _rules;
    private readonly float _rockSlope;
    private readonly ShaderMaterial _swaying;
    private readonly ShaderMaterial _still;
    private readonly Dictionary<GroundCoverKind, ArrayMesh> _meshes = [];
    private readonly List<GroundCoverPlacement> _placed = [];
    private Dictionary<GroundCoverKind, MultiMeshInstance3D>?[] _chunks = [];
    private ChunkStreamer? _streamer;
    private List<Clearing> _clearings = [];
    private TerrainField? _terrain;
    private int _perSide;
    private bool _firstStream;
    private double _sinceRefresh;
    private readonly Queue<int> _stale = [];

    /// <summary>Creates the presenter; nothing is shown until <see cref="Rebuild"/>.</summary>
    /// <param name="parent">Node that holds the ground cover.</param>
    /// <param name="view">Settlement view settings: the ground cover and the villages' layout.</param>
    /// <param name="rules">Settlement rules, for the villages' clearings.</param>
    /// <param name="rockSlope">The slope at which the ground is bare rock.</param>
    public GroundCoverPresenter(Node3D parent, SettlementViewSettings view, SettlementRules rules, float rockSlope)
    {
        (_parent, _view, _rules, _rockSlope) = (parent, view, rules, rockSlope);
        _swaying = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        _swaying.SetShaderParameter("reference_height", 0.8f);
        _swaying.SetShaderParameter("gust_speed", 1.8f);
        _still = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        _still.SetShaderParameter("wind", Vector2.Zero);
        foreach (var kind in Enum.GetValues<GroundCoverKind>())
        {
            _meshes[kind] = Meshes.ToArrayMesh(GroundCoverMesh.Build(kind));
        }
    }

    /// <summary>Planted chunks and all chunks, for the debug panel.</summary>
    public (int Loaded, int Chunks) Streaming => (_streamer?.Loaded.Count ?? 0, _streamer?.Count ?? 0);

    /// <summary>Clears the ground cover for a new or loaded world; it is planted as the camera comes near.</summary>
    /// <param name="world">The world.</param>
    /// <param name="terrain">Its terrain.</param>
    public void Rebuild(WorldState world, TerrainField terrain)
    {
        _parent.FreeChildren();
        _terrain = terrain;
        var settings = _view.GroundCover;
        _perSide = (int)MathF.Ceiling(world.Regions.Columns * world.Regions.RegionSize / settings.ChunkMetres);
        _chunks = new Dictionary<GroundCoverKind, MultiMeshInstance3D>?[_perSide * _perSide];
        _streamer = new ChunkStreamer(_perSide, settings.ChunkMetres, settings.Radius, settings.Radius + settings.ChunkMetres);
        _clearings = Clearings(world);
        (_firstStream, _sinceRefresh) = (true, 0.0);
        _stale.Clear();
    }

    /// <summary>Plants the chunks the camera has come near, clears those it has left, and sways with the wind.</summary>
    /// <param name="world">The world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="camera">The camera's place.</param>
    /// <param name="wind">The wind's sway at the camera, on the ground, in metres.</param>
    public void Sync(WorldState world, double delta, Vector3 camera, Vector2 wind)
    {
        if (_streamer is null || _terrain is null)
        {
            return;
        }

        _swaying.SetShaderParameter("wind", wind * 0.8f);
        var step = _streamer.Update(new System.Numerics.Vector3(camera.X, camera.Y, camera.Z), _firstStream ? int.MaxValue : ChunksPerFrame);
        _firstStream = false;
        foreach (var chunk in step.Unload)
        {
            foreach (var node in _chunks[chunk]?.Values ?? Enumerable.Empty<MultiMeshInstance3D>())
            {
                node.QueueFree();
            }

            _chunks[chunk] = null;
        }

        foreach (var chunk in step.Load)
        {
            _chunks[chunk] = [];
            Draw(world, chunk);
        }

        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshSeconds)
        {
            _sinceRefresh = 0.0;
            _clearings = Clearings(world);
            foreach (var chunk in _streamer.Loaded)
            {
                _stale.Enqueue(chunk);
            }
        }

        for (var i = 0; i < ChunksPerFrame && _stale.TryDequeue(out var chunk); i++)
        {
            if (_chunks[chunk] is not null)
            {
                Draw(world, chunk);
            }
        }
    }

    /// <summary>Lays a chunk out and fills its nodes, one per kind present.</summary>
    private void Draw(WorldState world, int chunk)
    {
        var settings = _view.GroundCover;
        var (minX, minZ) = (chunk % _perSide * settings.ChunkMetres, chunk / _perSide * settings.ChunkMetres);
        _placed.Clear();
        GroundCoverLayout.Place(world, _terrain!, minX, minZ, settings.ChunkMetres, _clearings, settings, _rockSlope, _placed);
        var nodes = _chunks[chunk]!;
        foreach (var kind in Enum.GetValues<GroundCoverKind>())
        {
            var count = _placed.Count(p => p.Kind == kind);
            if (count == 0 && !nodes.ContainsKey(kind))
            {
                continue;
            }

            if (!nodes.TryGetValue(kind, out var node))
            {
                node = Node(kind, $"{kind}_{chunk}");
                nodes[kind] = node;
            }

            var mesh = node.Multimesh;
            mesh.InstanceCount = count;
            var index = 0;
            foreach (var thing in _placed.Where(p => p.Kind == kind))
            {
                var basis = new Basis(Vector3.Up, thing.Yaw).Scaled(Vector3.One * thing.Scale);
                mesh.SetInstanceTransform(index, new Transform3D(basis, new Vector3(thing.Position.X, thing.Position.Y, thing.Position.Z)));
                var tint = thing.Tint.ToGodot().SrgbToLinear();
                mesh.SetInstanceCustomData(index, new Color(tint.R, tint.G, tint.B, thing.Yaw / Mathf.Tau));
                index++;
            }
        }
    }

    /// <summary>A chunk's node for one kind.</summary>
    private MultiMeshInstance3D Node(GroundCoverKind kind, string name)
    {
        var node = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = _meshes[kind] },
            MaterialOverride = kind == GroundCoverKind.Rock ? _still : _swaying,
            CastShadow = kind is GroundCoverKind.Bush or GroundCoverKind.Rock or GroundCoverKind.Reeds ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = _view.GroundCover.Radius,
            VisibilityRangeEndMargin = FadeMargin,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
        };
        _parent.AddChild(node);
        var stem = Stems[kind].SrgbToLinear();
        node.SetInstanceShaderParameter("trunk_color", new Vector3(stem.R, stem.G, stem.B));
        return node;
    }

    /// <summary>The ground the villages keep bare: by their houses, on their fields and on their squares.</summary>
    private List<Clearing> Clearings(WorldState world)
    {
        var bare = new List<Clearing>();
        foreach (var settlement in world.Settlements.Where(s => s.Population > 0 || s.Housing > 0))
        {
            GroundCoverLayout.BareGround(SettlementLayout.ComputeSite(settlement, _rules, _terrain!, _view.Layout), _view.Layout.FieldSize, _view.Layout.ShrineClearance, bare);
        }

        return bare;
    }
}
