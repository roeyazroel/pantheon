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
/// Draws the roads (spec §29, ADR-033): a ribbon of beaten earth along the part of each road built
/// so far, darker where a disaster has cut it. A road is redrawn as it grows or is cut or mended.
/// </summary>
public sealed class RoadPresenter
{
    // A road is redrawn once this many more metres are built.
    private const float RedrawMetres = 4f;

    private static readonly Color Open = new(0.55f, 0.45f, 0.31f);
    private static readonly Color Cut = new(0.33f, 0.25f, 0.2f);

    private readonly Node3D _parent;
    private readonly RoadRules _rules;
    private readonly StandardMaterial3D _material = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1f };
    private readonly Dictionary<int, (MeshInstance3D Node, float Built, bool Open)> _views = [];
    private readonly HashSet<int> _seen = [];
    private TerrainField? _terrain;

    /// <summary>Creates the presenter; nothing is shown until <see cref="Rebuild"/>.</summary>
    /// <param name="parent">Node that holds the roads.</param>
    /// <param name="rules">Road rules, for whether a road is cut.</param>
    public RoadPresenter(Node3D parent, RoadRules rules)
    {
        _parent = parent;
        _rules = rules;
    }

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

    /// <summary>A road's ribbon.</summary>
    private MeshInstance3D Draw(RoadState road, bool open)
    {
        var ribbon = RoadLayout.Ribbon(road, _terrain!);
        var node = new MeshInstance3D { Name = "Road_" + road.Id, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        if (ribbon.Count >= 2)
        {
            var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            tool.SetColor(open ? Open : Cut);
            tool.SetNormal(Vector3.Up);
            for (var i = 1; i < ribbon.Count; i++)
            {
                var (a, b) = (Point(ribbon[i - 1].Left), Point(ribbon[i - 1].Right));
                var (c, d) = (Point(ribbon[i].Left), Point(ribbon[i].Right));
                Quad(tool, a, b, c, d);
            }

            node.Mesh = tool.Commit();
            node.MaterialOverride = _material;
        }

        _parent.AddChild(node);
        return node;
    }

    /// <summary>Two triangles of the ribbon between two cross-sections (a-b, then c-d), each facing up.</summary>
    private static void Quad(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Upward(tool, a, b, c);
        Upward(tool, b, d, c);
    }

    /// <summary>
    /// A triangle seen from above: Godot's front faces are clockwise, so their right-hand normal
    /// points away from the viewer, down.
    /// </summary>
    private static void Upward(SurfaceTool tool, Vector3 p, Vector3 q, Vector3 r)
    {
        var (second, third) = (q - p).Cross(r - p).Y < 0f ? (q, r) : (r, q);
        tool.AddVertex(p);
        tool.AddVertex(second);
        tool.AddVertex(third);
    }

    /// <summary>A layout point as a Godot vector.</summary>
    private static Vector3 Point(System.Numerics.Vector3 point) => new(point.X, point.Y, point.Z);
}
