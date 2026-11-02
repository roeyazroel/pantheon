using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Domain.Roads;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Roads;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Land;

/// <summary>
/// Draws the roads (spec §29, ADR-033, ADR-073): a track worn into the ground along the part of each
/// road built so far (<see cref="RoadLayout"/>), its edges, ruts and verges drawn by the road shader,
/// darker where a disaster has cut it. A road is redrawn as it grows or is cut or mended.
/// </summary>
public sealed class RoadPresenter
{
    // A road is redrawn once this many more metres are built.
    private const float RedrawMetres = 4f;

    private readonly Node3D _parent;
    private readonly RoadRules _rules;
    private readonly Dictionary<int, (MeshInstance3D Node, float Built, bool Open)> _views = [];
    private readonly HashSet<int> _seen = [];
    private readonly List<(BridgeSpan Span, int Road)> _crossings = [];
    private TerrainField? _terrain;

    /// <summary>Creates the presenter; nothing is shown until <see cref="Rebuild"/>.</summary>
    /// <param name="parent">Node that holds the roads.</param>
    /// <param name="rules">Road rules, for whether a road is cut.</param>
    /// <param name="buildings">The buildings made in code, for the bridges' timber (ADR-054).</param>
    public RoadPresenter(Node3D parent, RoadRules rules, Buildings.BuildingAssets buildings)
    {
        _parent = parent;
        _rules = rules;
        _buildings = buildings;
    }

    private readonly Buildings.BuildingAssets _buildings;

    /// <summary>Drops every road, for a new or loaded world.</summary>
    /// <param name="world">World to show.</param>
    /// <param name="terrain">Terrain of the new world.</param>
    public void Rebuild(WorldState world, TerrainField terrain)
    {
        _parent.FreeChildren();
        _views.Clear();
        _crossings.Clear();
        _terrain = terrain;
        Sync(world);
    }

    /// <summary>Draws new roads, redraws those that grew, were cut or mended, and drops those gone.</summary>
    /// <param name="world">Current world.</param>
    public void Sync(WorldState world)
    {
        if (_terrain is null)
        {
            return;
        }

        _seen.Clear();
        foreach (var road in world.Roads)
        {
            _seen.Add(road.Id);
            var open = RoadModel.IsOpen(_rules, road) || road.Built < road.Length;
            var exists = _views.TryGetValue(road.Id, out var view);
            var grown = road.Built - view.Built >= RedrawMetres || (road.Built >= road.Length && view.Built < road.Length);
            if (exists && view.Open == open && !grown)
            {
                continue;
            }

            view.Node?.QueueFree();
            _views[road.Id] = (Draw(road, open), road.Built, open);
        }

        foreach (var id in new List<int>(_views.Keys))
        {
            if (!_seen.Contains(id))
            {
                _views[id].Node.QueueFree();
                _views.Remove(id);
                _crossings.RemoveAll(crossing => crossing.Road == id);
            }
        }
    }

    /// <summary>A road's track: two strips, its left edge to its middle and its middle to its right edge.</summary>
    private MeshInstance3D Draw(RoadState road, bool open)
    {
        var ribbon = RoadLayout.Ribbon(road, _terrain!);
        var node = new MeshInstance3D { Name = "Road_" + road.Id, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        if (ribbon.Count >= 2)
        {
            var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            tool.SetNormal(Vector3.Up);
            var vertices = 0;
            RoadMesh.Add(tool, ribbon, ref vertices, (a, b) => BridgeLayout.IsWet(_terrain!, a.Centre) && BridgeLayout.IsWet(_terrain!, b.Centre));
            node.Mesh = tool.Commit();
            node.MaterialOverride = RoadMesh.Material;
            node.SetInstanceShaderParameter("cut", open ? 0f : 1f);
        }

        _parent.AddChild(node);
        Bridges(road, node);
        return node;
    }

    /// <summary>A layout point as a Godot vector.</summary>
    private static Vector3 Point(System.Numerics.Vector3 point) => new(point.X, point.Y, point.Z);

    /// <summary>
    /// A plank bridge over each stretch of water the road crosses (ADR-054, ADR-077), from the road on
    /// one bank to the road on the other. Roads sharing a crossing share the first one's bridge.
    /// </summary>
    private void Bridges(RoadState road, Node3D under)
    {
        _crossings.RemoveAll(crossing => crossing.Road == road.Id);
        foreach (var span in BridgeLayout.Spans(road, _terrain!))
        {
            if (_crossings.Exists(crossing => crossing.Span.SameCrossing(span)))
            {
                continue;
            }

            _crossings.Add((span, road.Id));
            var (a, b) = (Point(span.From), Point(span.To));
            var multimesh = Buildings.BuildingAssets.NewMultiMesh(Meshes.ToArrayMesh(BridgeLayout.Mesh(span, _terrain!), _buildings.Material));
            multimesh.InstanceCount = 1;
            var basis = new Basis(Vector3.Up, Mathf.Atan2(b.X - a.X, b.Z - a.Z));
            multimesh.SetInstanceTransform(0, new Transform3D(basis, (a + b) / 2f));
            multimesh.SetInstanceColor(0, Buildings.BuildingAssets.InstanceColor(_buildings.Settings.Styles[HouseStyle.Longhouse].Walls[0]));
            multimesh.SetInstanceCustomData(0, Buildings.BuildingAssets.InstanceCustom(_buildings.Settings.Trim, SurfacePattern.Planks, SurfacePattern.Planks));
            under.AddChild(new MultiMeshInstance3D { Name = "Bridge", Multimesh = multimesh });
        }
    }
}
