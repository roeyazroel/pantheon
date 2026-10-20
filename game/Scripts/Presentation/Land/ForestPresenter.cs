using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Game.Presentation.Nature;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Land;

/// <summary>
/// Draws the forests (spec §93, ADR-033): the trees <see cref="ForestLayout"/> places, in square
/// chunks of the island, grown by <see cref="TreeGrowth"/> (ADR-073): one MultiMesh per chunk,
/// species and variant for the trees near the camera, and one per chunk and species of their lighter
/// forms beyond <see cref="TreeSettings.DetailDistance"/>. The chunks stream (spec §84, ADR-050):
/// only those near the camera are planted, nearest first and a few a frame, and those left far
/// behind are cleared. Every second it looks for regions whose timber, snow, villages or roads have
/// changed and redraws their planted chunks, a few a frame.
/// </summary>
public sealed class ForestPresenter
{
    private const float ChunkMetres = 256f;
    private const float RefreshSeconds = 1f;
    private const float HideDistance = 1600f;
    private const float FadeMargin = 300f;

    // Near and far trees cross-fade over this many metres about the detail distance.
    private const float DetailMargin = 60f;

    private static readonly Color Snowy = new(0.9f, 0.92f, 0.95f);

    private readonly Node3D _parent;
    private readonly SettlementRules _rules;
    private readonly SettlementLayoutSettings _layout;
    private readonly ShaderMaterial _material;
    private readonly StreamingSettings _streaming;
    private readonly TreeSettings _species;
    private readonly Dictionary<(TreeKind Kind, int Variant, PlantDetail Detail), ArrayMesh> _meshes = [];
    private readonly List<TreePlacement> _trees = [];
    private readonly HashSet<int> _dirty = [];
    private readonly Dictionary<int, int> _housesShown = [];
    private readonly HashSet<int> _roadsShown = [];
    private Dictionary<(TreeKind Kind, int Variant, PlantDetail Detail), MultiMeshInstance3D>?[] _chunks = [];
    private ChunkStreamer? _streamer;
    private bool _firstStream;
    private int[] _shown = [];
    private List<Clearing> _clearings = [];
    private readonly Dictionary<int, Clearing> _clearingOf = [];
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
            for (var variant = 0; variant < TreeGrowth.Variants; variant++)
            {
                _meshes[(kind, variant, PlantDetail.Near)] = Meshes.ToArrayMesh(TreeGrowth.Grow(kind, variant, PlantDetail.Near));
            }

            // In the distance one variant of each species is enough.
            _meshes[(kind, 0, PlantDetail.Far)] = Meshes.ToArrayMesh(TreeGrowth.Grow(kind, 0, PlantDetail.Far));
        }

        _parent = parent;
        _rules = rules;
        _layout = layout;
        _streaming = streaming;
        _material = PlantTextures.Material();
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
        _chunks = new Dictionary<(TreeKind, int, PlantDetail), MultiMeshInstance3D>?[_chunksPerSide * _chunksPerSide];
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

    /// <summary>
    /// Marks the chunks of regions whose tree count or snow changed, those about a village whose
    /// clearing grew or shrank, and those along a new road (ADR-056: in a busy world at ×16 some
    /// village grows every second, and redrawing every chunk for it took a frame's share).
    /// </summary>
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

        var changed = false;
        foreach (var settlement in world.Settlements)
        {
            var houses = SettlementLayout.ComputeHouseCount(settlement, _rules);
            if (_housesShown.TryGetValue(settlement.Id, out var shown) && shown == houses)
            {
                continue;
            }

            _housesShown[settlement.Id] = houses;
            if (_clearingOf.Remove(settlement.Id, out var before))
            {
                MarkAround(grid, before);
            }

            if (settlement.Population > 0 || settlement.Housing > 0)
            {
                var now = ClearingFor(settlement);
                _clearingOf[settlement.Id] = now;
                MarkAround(grid, now);
            }

            changed = true;
        }

        foreach (var road in world.Roads)
        {
            if (_roadsShown.Add(road.Id))
            {
                foreach (var point in road.Path)
                {
                    MarkAround(grid, new Clearing(new System.Numerics.Vector2(point.X, point.Z), ForestLayout.RoadClearance + 1f));
                }
            }
        }

        if (changed)
        {
            _clearings = [.. _clearingOf.Values];
        }
    }

    /// <summary>Marks the planted chunks a clearing touches as needing a redraw.</summary>
    private void MarkAround(RegionGrid grid, Clearing clearing)
    {
        var size = _regionsPerChunk * grid.RegionSize;
        var last = _chunksPerSide - 1;
        var (fromColumn, toColumn) = (Math.Clamp((int)MathF.Floor((clearing.Center.X - clearing.Radius) / size), 0, last), Math.Clamp((int)MathF.Floor((clearing.Center.X + clearing.Radius) / size), 0, last));
        var (fromRow, toRow) = (Math.Clamp((int)MathF.Floor((clearing.Center.Y - clearing.Radius) / size), 0, last), Math.Clamp((int)MathF.Floor((clearing.Center.Y + clearing.Radius) / size), 0, last));
        for (var row = fromRow; row <= toRow; row++)
        {
            for (var column = fromColumn; column <= toColumn; column++)
            {
                var chunk = (row * _chunksPerSide) + column;
                if (_chunks[chunk] is not null)
                {
                    _dirty.Add(chunk);
                }
            }
        }
    }

    /// <summary>A village's clearing: its houses and fields, kept free of trees.</summary>
    private Clearing ClearingFor(SettlementState settlement) =>
        ForestLayout.ClearingOf(SettlementLayout.ComputeSite(settlement, _rules, _terrain!, _layout), _layout.FieldSize);

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

        var size = _regionsPerChunk * grid.RegionSize;
        var (midX, midZ) = ((chunkColumn + 0.5f) * size, (chunkRow + 0.5f) * size);
        var centre = new Vector3(midX, MathF.Max(0f, _terrain!.SampleHeight(midX, midZ)), midZ);
        foreach (var kind in Enum.GetValues<TreeKind>())
        {
            for (var variant = 0; variant < TreeGrowth.Variants; variant++)
            {
                Show(nodes, (kind, variant, PlantDetail.Near), chunk, centre, snow, tree => tree.Kind == kind && tree.Variant == variant);
            }

            Show(nodes, (kind, 0, PlantDetail.Far), chunk, centre, snow, tree => tree.Kind == kind);
        }
    }

    /// <summary>Fills one of a chunk's nodes with the trees it shows, making it when there are some.</summary>
    private void Show(Dictionary<(TreeKind, int, PlantDetail), MultiMeshInstance3D> nodes, (TreeKind Kind, int Variant, PlantDetail Detail) key, int chunk, Vector3 centre, List<float> snow, Func<TreePlacement, bool> shows)
    {
        var count = 0;
        foreach (var tree in _trees)
        {
            count += shows(tree) ? 1 : 0;
        }

        if (count == 0 && !nodes.ContainsKey(key))
        {
            return;
        }

        if (!nodes.TryGetValue(key, out var node))
        {
            node = Instance(key, $"{key.Kind}_{key.Variant}_{key.Detail}_{chunk}", centre);
            nodes[key] = node;
        }

        Fill(node.Multimesh, shows, count, _species.Colors[key.Kind].Leaves.ToGodot(), snow, centre);
    }

    /// <summary>Sets a MultiMesh to the chunk's trees it shows, about the chunk's middle.</summary>
    private void Fill(MultiMesh mesh, Func<TreePlacement, bool> shows, int count, Color leaves, List<float> snow, Vector3 centre)
    {
        mesh.InstanceCount = count;
        var index = 0;
        for (var i = 0; i < _trees.Count; i++)
        {
            var tree = _trees[i];
            if (!shows(tree))
            {
                continue;
            }

            var basis = new Basis(Vector3.Up, tree.Yaw).Scaled(Vector3.One * tree.Scale);
            mesh.SetInstanceTransform(index, new Transform3D(basis, new Vector3(tree.Position.X, tree.Position.Y, tree.Position.Z) - centre));
            // The shader takes colours as linear.
            var tint = (leaves * tree.Shade).Lerp(Snowy, snow[i] * 0.75f).SrgbToLinear();
            mesh.SetInstanceCustomData(index, new Color(tint.R, tint.G, tint.B, (tree.Yaw / Mathf.Tau) % 1f));
            index++;
        }
    }

    /// <summary>
    /// A chunk's node for one species, variant and detail, at the chunk's middle so its distance from the
    /// camera is the chunk's: near trees fade out past the detail distance as the far ones fade in.
    /// </summary>
    private MultiMeshInstance3D Instance((TreeKind Kind, int Variant, PlantDetail Detail) key, string name, Vector3 centre)
    {
        var near = key.Detail == PlantDetail.Near;
        var instance = new MultiMeshInstance3D
        {
            Name = name,
            Position = centre,
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = _meshes[key] },
            MaterialOverride = _material,
            // Far trees' shadows are a pixel or two; the shadow maps are spared them.
            CastShadow = near ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeBegin = near ? 0f : _species.DetailDistance,
            VisibilityRangeBeginMargin = near ? 0f : DetailMargin,
            VisibilityRangeEnd = near ? _species.DetailDistance : HideDistance,
            VisibilityRangeEndMargin = near ? DetailMargin : FadeMargin,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
        };
        _parent.AddChild(instance);
        var trunk = _species.Colors[key.Kind].Trunk.ToGodot().SrgbToLinear();
        instance.SetInstanceShaderParameter("trunk_color", new Vector3(trunk.R, trunk.G, trunk.B));
        instance.SetInstanceShaderParameter("bark_style", key.Kind switch
        {
            TreeKind.Birch => 1f,
            TreeKind.Palm => 2f,
            TreeKind.Pine or TreeKind.Fir or TreeKind.Oak or TreeKind.Willow or TreeKind.Snag => 0f,
        });
        return instance;
    }

    /// <summary>The villages' clearings, from their sites as drawn now.</summary>
    private List<Clearing> Clearings(WorldState world)
    {
        _clearingOf.Clear();
        foreach (var settlement in world.Settlements)
        {
            _housesShown[settlement.Id] = SettlementLayout.ComputeHouseCount(settlement, _rules);
            if (settlement.Population > 0 || settlement.Housing > 0)
            {
                _clearingOf[settlement.Id] = ClearingFor(settlement);
            }
        }

        _roadsShown.Clear();
        foreach (var road in world.Roads)
        {
            _roadsShown.Add(road.Id);
        }

        return [.. _clearingOf.Values];
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
