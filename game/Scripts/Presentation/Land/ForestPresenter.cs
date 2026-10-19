using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Land;

/// <summary>
/// Draws the forests (spec §93, ADR-033): the trees <see cref="ForestLayout"/> places, in square
/// chunks of the island, one MultiMesh per chunk and tree shape. The chunks stream (spec §84,
/// ADR-050): only those near the camera are planted, nearest first and a few a frame, and those
/// left far behind are cleared. Every second it looks for regions whose timber, snow, villages or
/// roads have changed and redraws their planted chunks, a few a frame.
/// </summary>
public sealed class ForestPresenter
{
    private const string ShaderPath = "res://Shaders/tree.gdshader";
    private const float ChunkMetres = 256f;
    private const float RefreshSeconds = 1f;
    private const float HideDistance = 1600f;
    private const float FadeMargin = 300f;

    private static readonly Color Snowy = new(0.9f, 0.92f, 0.95f);

    private readonly Node3D _parent;
    private readonly SettlementRules _rules;
    private readonly SettlementLayoutSettings _layout;
    private readonly ShaderMaterial _material;
    private readonly StreamingSettings _streaming;
    private readonly TreeSettings _species;
    private readonly Dictionary<TreeKind, ArrayMesh> _meshes = [];
    private readonly List<TreePlacement> _trees = [];
    private readonly HashSet<int> _dirty = [];
    private readonly Dictionary<int, int> _housesShown = [];
    private readonly HashSet<int> _roadsShown = [];
    private Dictionary<TreeKind, MultiMeshInstance3D>?[] _chunks = [];
    private ChunkStreamer? _streamer;
    private bool _firstStream;
    private int[] _shown = [];
    private List<Clearing> _clearings = [];
    private TerrainField? _terrain;
    private int _regionsPerChunk;
    private int _chunksPerSide;
    private double _sinceRefresh;

    /// <summary>Creates the presenter; nothing is shown until <see cref="Rebuild"/>.</summary>
    /// <param name="parent">Node that holds the forests.</param>
    /// <param name="rules">Settlement rules, for the villages' houses.</param>
    /// <param name="layout">Settlement layout, for the villages' clearings.</param>
    /// <param name="streaming">How near the camera forests are planted.</param>
    /// <param name="species">The trees' species, colours and sway (ADR-053).</param>
    public ForestPresenter(Node3D parent, SettlementRules rules, SettlementLayoutSettings layout, StreamingSettings streaming, TreeSettings species)
    {
        _species = species;
        foreach (var kind in Enum.GetValues<TreeKind>())
        {
            _meshes[kind] = Meshes.ToArrayMesh(TreeMesh.Build(kind, detailed: true));
        }

        _parent = parent;
        _rules = rules;
        _layout = layout;
        _streaming = streaming;
        _material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
    }

    /// <summary>The wind's sway at the camera, on the ground, in metres: for the ground cover too.</summary>
    public Vector2 Wind { get; private set; }

    /// <summary>Planted chunks and all chunks, for the debug panel.</summary>
    public (int Loaded, int Chunks) Streaming => (_streamer?.Loaded.Count ?? 0, _streamer?.Count ?? 0);

    /// <summary>Prepares the forests of a new or loaded world; they are planted as the camera comes near.</summary>
    /// <param name="world">World to show.</param>
    /// <param name="terrain">Terrain derived from the world.</param>
    public void Rebuild(WorldState world, TerrainField terrain)
    {
        _parent.FreeChildren();
        _terrain = terrain;
        var grid = world.Regions;
        _regionsPerChunk = Math.Max(1, (int)MathF.Round(ChunkMetres / grid.RegionSize));
        _chunksPerSide = (grid.Columns + _regionsPerChunk - 1) / _regionsPerChunk;
        _chunks = new Dictionary<TreeKind, MultiMeshInstance3D>?[_chunksPerSide * _chunksPerSide];
        _streamer = new ChunkStreamer(_chunksPerSide, _regionsPerChunk * grid.RegionSize, _streaming.ForestLoadRadius, _streaming.ForestUnloadRadius);
        _firstStream = true;
        _shown = new int[grid.Cells.Count];
        _dirty.Clear();
        _housesShown.Clear();
        _roadsShown.Clear();
        _clearings = Clearings(world);
    }

    /// <summary>Plants the chunks the camera has come near, clears those it has left, and redraws planted chunks whose trees have changed.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="camera">The camera's place.</param>
    public void Sync(WorldState world, double delta, Vector3 camera)
    {
        if (_terrain is null || _streamer is null)
        {
            return;
        }

        // The first frame of a world plants everything in view at once, so nothing pops in.
        var step = _streamer.Update(new System.Numerics.Vector3(camera.X, camera.Y, camera.Z), _firstStream ? int.MaxValue : _streaming.ForestChunksPerFrame);
        _firstStream = false;
        foreach (var chunk in step.Unload)
        {
            if (_chunks[chunk] is { } nodes)
            {
                foreach (var node in nodes.Values)
                {
                    node.QueueFree();
                }

                _chunks[chunk] = null;
            }

            _dirty.Remove(chunk);
        }

        // Trees bend with the weather's wind over the camera's ground (ADR-053).
        var (strength, windX, windZ) = Breeze.At(world.Weather, new GroundPosition(camera.X, camera.Z));
        var sway = _species.Sway + (_species.StormSway * strength);
        Wind = new Vector2(windX, windZ) * sway;
        _material.SetShaderParameter("wind", Wind);

        foreach (var chunk in step.Load)
        {
            _chunks[chunk] = [];
            _dirty.Remove(chunk);
            Draw(world, chunk);
        }

        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshSeconds)
        {
            _sinceRefresh = 0;
            FindChanges(world);
        }

        var drawn = 0;
        foreach (var chunk in new List<int>(_dirty))
        {
            if (drawn++ >= _streaming.ForestChunksPerFrame)
            {
                break;
            }

            _dirty.Remove(chunk);
            Draw(world, chunk);
        }
    }

    /// <summary>Marks the chunks of regions whose tree count or snow changed, and every chunk when a village grows or a road is planned.</summary>
    private void FindChanges(WorldState world)
    {
        var grid = world.Regions;
        foreach (var region in grid.Cells)
        {
            if (Signature(grid, region) != _shown[region.Id] && ChunkOf(grid, region.Id) is var chunk && _chunks[chunk] is not null)
            {
                _dirty.Add(chunk);
            }
        }

        var grown = false;
        foreach (var settlement in world.Settlements)
        {
            var houses = SettlementLayout.ComputeHouseCount(settlement, _rules);
            grown |= !_housesShown.TryGetValue(settlement.Id, out var shown) || shown != houses;
        }

        var newRoad = world.Roads.Count != _roadsShown.Count || world.Roads.Exists(r => !_roadsShown.Contains(r.Id));
        if (grown || newRoad)
        {
            _clearings = Clearings(world);
            for (var i = 0; i < _chunks.Length; i++)
            {
                if (_chunks[i] is not null)
                {
                    _dirty.Add(i);
                }
            }
        }
    }

    /// <summary>Draws one chunk's trees.</summary>
    private void Draw(WorldState world, int chunk)
    {
        var grid = world.Regions;
        var (chunkColumn, chunkRow) = (chunk % _chunksPerSide, chunk / _chunksPerSide);
        _trees.Clear();
        var snow = new List<float>();
        for (var row = chunkRow * _regionsPerChunk; row < Math.Min(grid.Rows, (chunkRow + 1) * _regionsPerChunk); row++)
        {
            for (var column = chunkColumn * _regionsPerChunk; column < Math.Min(grid.Columns, (chunkColumn + 1) * _regionsPerChunk); column++)
            {
                var region = grid.Cells[(row * grid.Columns) + column];
                var before = _trees.Count;
                ForestLayout.Place(world, _terrain!, region, _clearings, _trees, _species);
                for (var i = before; i < _trees.Count; i++)
                {
                    snow.Add(Math.Clamp(region.Snow, 0f, 1f));
                }

                _shown[region.Id] = Signature(grid, region);
            }
        }

        if (_chunks[chunk] is not { } nodes)
        {
            return;
        }

        foreach (var kind in Enum.GetValues<TreeKind>())
        {
            var count = 0;
            foreach (var tree in _trees)
            {
                count += tree.Kind == kind ? 1 : 0;
            }

            if (count == 0 && !nodes.ContainsKey(kind))
            {
                continue;
            }

            if (!nodes.TryGetValue(kind, out var node))
            {
                node = Instance(kind, $"{kind}_{chunk}");
                nodes[kind] = node;
            }

            Fill(node.Multimesh, kind, count, _species.Colors[kind].Leaves.ToGodot(), snow);
        }
    }

    /// <summary>Sets a MultiMesh to the chunk's trees of one species.</summary>
    private void Fill(MultiMesh mesh, TreeKind kind, int count, Color leaves, List<float> snow)
    {
        mesh.InstanceCount = count;
        var index = 0;
        for (var i = 0; i < _trees.Count; i++)
        {
            var tree = _trees[i];
            if (tree.Kind != kind)
            {
                continue;
            }

            var basis = new Basis(Vector3.Up, tree.Yaw).Scaled(Vector3.One * tree.Scale);
            mesh.SetInstanceTransform(index, new Transform3D(basis, new Vector3(tree.Position.X, tree.Position.Y, tree.Position.Z)));
            // The shader takes colours as linear.
            var tint = (leaves * tree.Shade).Lerp(Snowy, snow[i] * 0.75f).SrgbToLinear();
            mesh.SetInstanceCustomData(index, new Color(tint.R, tint.G, tint.B, (tree.Yaw / Mathf.Tau) % 1f));
            index++;
        }
    }

    /// <summary>A chunk's node for one species, its trunk in the species' colour.</summary>
    private MultiMeshInstance3D Instance(TreeKind kind, string name)
    {
        var instance = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = _meshes[kind] },
            MaterialOverride = _material,
            VisibilityRangeEnd = HideDistance,
            VisibilityRangeEndMargin = FadeMargin,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
        };
        _parent.AddChild(instance);
        var trunk = _species.Colors[kind].Trunk.ToGodot().SrgbToLinear();
        instance.SetInstanceShaderParameter("trunk_color", new Vector3(trunk.R, trunk.G, trunk.B));
        return instance;
    }

    /// <summary>The villages' clearings, from their sites as drawn now.</summary>
    private List<Clearing> Clearings(WorldState world)
    {
        var clearings = new List<Clearing>(world.Settlements.Count);
        foreach (var settlement in world.Settlements)
        {
            _housesShown[settlement.Id] = SettlementLayout.ComputeHouseCount(settlement, _rules);
            if (settlement.Population > 0 || settlement.Housing > 0)
            {
                clearings.Add(ForestLayout.ClearingOf(SettlementLayout.ComputeSite(settlement, _rules, _terrain!, _layout), _layout.FieldSize));
            }
        }

        _roadsShown.Clear();
        foreach (var road in world.Roads)
        {
            _roadsShown.Add(road.Id);
        }

        return clearings;
    }

    /// <summary>What a region shows, to tell when it changes: its tree count and whether snow lies on it.</summary>
    private static int Signature(RegionGrid grid, RegionState region) => (ForestLayout.TreeCount(grid, region) * 4) + (int)MathF.Round(Math.Clamp(region.Snow, 0f, 1f) * 3f);

    /// <summary>The chunk a region lies in.</summary>
    private int ChunkOf(RegionGrid grid, int regionId)
    {
        var (column, row) = grid.GetCoordinates(regionId);
        return ((row / _regionsPerChunk) * _chunksPerSide) + (column / _regionsPerChunk);
    }
}
