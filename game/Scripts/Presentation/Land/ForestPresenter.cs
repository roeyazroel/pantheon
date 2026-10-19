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
/// chunks of the island, one MultiMesh per chunk and tree shape. Every second it looks for regions
/// whose timber, snow, villages or roads have changed and redraws their chunks, a few a frame.
/// </summary>
public sealed class ForestPresenter
{
    private const string ShaderPath = "res://Shaders/tree.gdshader";
    private const float ChunkMetres = 256f;
    private const float RefreshSeconds = 1f;
    private const int ChunksPerFrame = 6;
    private const float HideDistance = 1600f;
    private const float FadeMargin = 300f;

    private static readonly Color ConiferLeaves = new(0.17f, 0.34f, 0.2f);
    private static readonly Color BroadLeaves = new(0.33f, 0.52f, 0.24f);
    private static readonly Color Snowy = new(0.9f, 0.92f, 0.95f);

    private readonly Node3D _parent;
    private readonly SettlementRules _rules;
    private readonly SettlementLayoutSettings _layout;
    private readonly ShaderMaterial _material;
    private readonly ArrayMesh _conifer = TreeMeshes.Conifer();
    private readonly ArrayMesh _broadleaf = TreeMeshes.Broadleaf();
    private readonly List<TreePlacement> _trees = [];
    private readonly HashSet<int> _dirty = [];
    private readonly Dictionary<int, int> _housesShown = [];
    private readonly HashSet<int> _roadsShown = [];
    private (MultiMeshInstance3D Conifers, MultiMeshInstance3D Broadleaves)[] _chunks = [];
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
    public ForestPresenter(Node3D parent, SettlementRules rules, SettlementLayoutSettings layout)
    {
        _parent = parent;
        _rules = rules;
        _layout = layout;
        _material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
    }

    /// <summary>Draws every tree of a new or loaded world.</summary>
    /// <param name="world">World to show.</param>
    /// <param name="terrain">Terrain derived from the world.</param>
    public void Rebuild(WorldState world, TerrainField terrain)
    {
        _parent.FreeChildren();
        _terrain = terrain;
        var grid = world.Regions;
        _regionsPerChunk = Math.Max(1, (int)MathF.Round(ChunkMetres / grid.RegionSize));
        _chunksPerSide = (grid.Columns + _regionsPerChunk - 1) / _regionsPerChunk;
        _chunks = new (MultiMeshInstance3D, MultiMeshInstance3D)[_chunksPerSide * _chunksPerSide];
        for (var i = 0; i < _chunks.Length; i++)
        {
            _chunks[i] = (Instance(_conifer, "Conifers_" + i), Instance(_broadleaf, "Broadleaves_" + i));
        }

        _shown = new int[grid.Cells.Count];
        _housesShown.Clear();
        _roadsShown.Clear();
        _clearings = Clearings(world);
        for (var i = 0; i < _chunks.Length; i++)
        {
            Draw(world, i);
        }
    }

    /// <summary>Redraws the chunks whose trees have changed, a few each frame.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Sync(WorldState world, double delta)
    {
        if (_terrain is null)
        {
            return;
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
            if (drawn++ >= ChunksPerFrame)
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
            if (Signature(grid, region) != _shown[region.Id])
            {
                _dirty.Add(ChunkOf(grid, region.Id));
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
                _dirty.Add(i);
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
                ForestLayout.Place(world, _terrain!, region, _clearings, _trees);
                for (var i = before; i < _trees.Count; i++)
                {
                    snow.Add(Math.Clamp(region.Snow, 0f, 1f));
                }

                _shown[region.Id] = Signature(grid, region);
            }
        }

        Fill(_chunks[chunk].Conifers.Multimesh, TreeKind.Conifer, ConiferLeaves, snow);
        Fill(_chunks[chunk].Broadleaves.Multimesh, TreeKind.Broadleaf, BroadLeaves, snow);
    }

    /// <summary>Sets a MultiMesh to the chunk's trees of one shape.</summary>
    private void Fill(MultiMesh mesh, TreeKind kind, Color leaves, List<float> snow)
    {
        var count = 0;
        foreach (var tree in _trees)
        {
            count += tree.Kind == kind ? 1 : 0;
        }

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
            mesh.SetInstanceCustomData(index, new Color(tint.R, tint.G, tint.B, 1f));
            index++;
        }
    }

    /// <summary>A chunk's node for one tree shape.</summary>
    private MultiMeshInstance3D Instance(ArrayMesh mesh, string name)
    {
        var instance = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = mesh },
            MaterialOverride = _material,
            VisibilityRangeEnd = HideDistance,
            VisibilityRangeEndMargin = FadeMargin,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
        };
        _parent.AddChild(instance);
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
