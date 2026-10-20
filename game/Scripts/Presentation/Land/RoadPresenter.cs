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

    private const string ShaderPath = "res://Shaders/road.gdshader";

    private readonly Node3D _parent;
    private readonly RoadRules _rules;
    private readonly ShaderMaterial _material;
    private readonly Dictionary<int, (MeshInstance3D Node, float Built, bool Open)> _views = [];
    private readonly HashSet<int> _seen = [];
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
        _material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        _material.SetShaderParameter("noise", PlantTextures.Noise);

    }

    private readonly Buildings.BuildingAssets _buildings;

    /// <summary>Drops every road, for a new or loaded world.</summary>
    /// <param name="world">World to show.</param>
    /// <param name="terrain">Terrain of the new world.</param>
    public void Rebuild(WorldState world, TerrainField terrain)
    {
        _parent.FreeChildren();
        _views.Clear();
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
            for (var i = 0; i < ribbon.Count; i++)
            {
                var section = ribbon[i];
                Vertex(tool, section.Left, 0f, section.Along);
                Vertex(tool, section.Centre, 0.5f, section.Along);
                Vertex(tool, section.Right, 1f, section.Along);
            }

            for (var i = 1; i < ribbon.Count; i++)
            {
                var (a, b) = ((i - 1) * 3, i * 3);
                for (var strip = 0; strip < 2; strip++)
                {
                    tool.AddIndex(a + strip);
                    tool.AddIndex(b + strip);
                    tool.AddIndex(a + strip + 1);
                    tool.AddIndex(a + strip + 1);
                    tool.AddIndex(b + strip);
                    tool.AddIndex(b + strip + 1);
                }
            }

            node.Mesh = tool.Commit();
            node.MaterialOverride = _material;
            node.SetInstanceShaderParameter("cut", open ? 0f : 1f);
        }

        _parent.AddChild(node);
        Bridges(road, node);
        return node;
    }

    /// <summary>A vertex of the track: across it (0 left, 0.5 middle, 1 right) and metres along it.</summary>
    private static void Vertex(SurfaceTool tool, System.Numerics.Vector3 point, float across, float along)
    {
        tool.SetUV(new Vector2(across, along));
        tool.AddVertex(Point(point));
    }

    /// <summary>A layout point as a Godot vector.</summary>
    private static Vector3 Point(System.Numerics.Vector3 point) => new(point.X, point.Y, point.Z);

    /// <summary>A plank bridge over each stretch of water the road crosses (ADR-054), stretched bank to bank.</summary>
    private void Bridges(RoadState road, Node3D under)
    {
        foreach (var (start, finish) in BridgeLayout.Spans(road, _terrain!))
        {
            var (a, b) = (new Vector3(start.X, start.Y, start.Z), new Vector3(finish.X, finish.Y, finish.Z));
            var length = a.DistanceTo(b) + 2f;
            var multimesh = Buildings.BuildingAssets.NewMultiMesh(Meshes.ToArrayMesh(BridgeLayout.Mesh(length), _buildings.Material));
            multimesh.InstanceCount = 1;
            var basis = new Basis(Vector3.Up, Mathf.Atan2(b.X - a.X, b.Z - a.Z));
            multimesh.SetInstanceTransform(0, new Transform3D(basis, (a + b) / 2f));
            multimesh.SetInstanceColor(0, Buildings.BuildingAssets.InstanceColor(_buildings.Settings.Styles[Pantheon.Game.Presentation.HouseStyle.Longhouse].Walls[0]));
            multimesh.SetInstanceCustomData(0, Buildings.BuildingAssets.InstanceCustom(_buildings.Settings.Trim, Pantheon.Game.Presentation.SurfacePattern.Planks, Pantheon.Game.Presentation.SurfacePattern.Planks));
            under.AddChild(new MultiMeshInstance3D { Name = "Bridge", Multimesh = multimesh });
        }
    }
}
