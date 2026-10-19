using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Water;

/// <summary>
/// Draws the rivers and lakes, and the floods over the land (ADR-022). The rivers and lakes are
/// built once from the terrain's hydrology; the floods follow the regions' flood each frame, rebuilt
/// only when a flood's depth changes visibly.
/// </summary>
public sealed class FreshWaterPresenter
{
    private const string ShaderPath = "res://Shaders/fresh_water.gdshader";

    // Height samples per side of one water quad.
    private const int RiverStep = 1;
    private const int FloodStep = 2;

    // How far from a river, lake or the sea a flood spreads, in metres.
    private const float FloodReach = 60f;

    // Metres a full flood rises above the river, lake or sea it comes from.
    private const float FloodRise = 3.5f;

    // Floods are drawn in steps of this share, so the sheet is rebuilt only when it changes visibly.
    private const float FloodQuantum = 0.05f;

    private readonly Node3D _parent;
    private readonly ShaderMaterial _rivers;
    private readonly ShaderMaterial _floods;
    private MeshInstance3D? _floodSheet;
    private FloodSurface? _floodSurface;
    private string _floodKey = string.Empty;

    /// <summary>Creates the presenter; nothing is drawn until <see cref="Build"/>.</summary>
    /// <param name="parent">Node that holds the water.</param>
    public FreshWaterPresenter(Node3D parent)
    {
        _parent = parent;
        var shader = GD.Load<Shader>(ShaderPath);
        _rivers = new ShaderMaterial { Shader = shader };
        _floods = new ShaderMaterial { Shader = shader };
        _floods.SetShaderParameter("muddiness", 0.55f);
        _floods.SetShaderParameter("shallow_alpha", 0.55f);
    }

    /// <summary>Replaces the rivers and lakes with those of a terrain, and clears the floods.</summary>
    /// <param name="terrain">Terrain with its hydrology.</param>
    /// <param name="world">The world, for its regions.</param>
    public void Build(TerrainField terrain, WorldState world)
    {
        _parent.FreeChildren();
        _floodSurface = FloodSurface.Build(terrain, FloodStep, FloodReach);
        _floodKey = string.Empty;
        var mesh = WaterSurfaceMesh.BuildFreshWater(terrain, RiverStep);
        if (!mesh.IsEmpty)
        {
            _parent.AddChild(new MeshInstance3D
            {
                Name = "RiversAndLakes",
                Mesh = ToArrayMesh(mesh),
                MaterialOverride = _rivers,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        _floodSheet = new MeshInstance3D { Name = "Floods", MaterialOverride = _floods, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _parent.AddChild(_floodSheet);
    }

    /// <summary>Redraws the floods when they have changed visibly.</summary>
    /// <param name="world">Current world.</param>
    public void Sync(WorldState world)
    {
        if (_floodSurface is null || _floodSheet is null)
        {
            return;
        }

        var floods = world.Regions.Cells.Select(region => region.Flood >= FloodQuantum ? Mathf.Round(region.Flood / FloodQuantum) * FloodQuantum : 0f).ToArray();
        var key = string.Join(';', floods.Select((flood, id) => (flood, id)).Where(f => f.flood > 0f).Select(f => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{f.id}:{f.flood:0.00}")));
        if (key == _floodKey)
        {
            return;
        }

        _floodKey = key;
        var mesh = key.Length == 0 ? null : _floodSurface.Mesh(world.Regions, floods, FloodRise);
        _floodSheet.Mesh = mesh is null || mesh.IsEmpty ? null : ToArrayMesh(mesh);
    }

    /// <summary>A Godot mesh of the water quads, the depth in the vertex colour.</summary>
    private static ArrayMesh ToArrayMesh(WaterSurfaceMeshData data)
    {
        var positions = new Vector3[data.Positions.Length];
        var colors = new Color[data.Positions.Length];
        var normals = new Vector3[data.Positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            positions[i] = new Vector3(data.Positions[i].X, data.Positions[i].Y, data.Positions[i].Z);
            colors[i] = new Color(data.Depths[i], 0f, 0f);
            normals[i] = Vector3.Up;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
