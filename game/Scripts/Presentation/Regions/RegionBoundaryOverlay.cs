using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;
using NVector3 = System.Numerics.Vector3;

namespace Pantheon.Client.Presentation.Regions;

/// <summary>
/// Region grid lines draped over the terrain, plus an outline around the region under the cursor.
/// Both are hidden until the player toggles them on.
/// </summary>
public sealed class RegionBoundaryOverlay
{
    private const string BoundaryMaterialPath = "res://Materials/region_boundary.tres";
    private const string HighlightMaterialPath = "res://Materials/region_highlight.tres";
    private const string LineColorParameter = "line_color";
    private const float BoundaryAlpha = 0.55f;
    private const float HighlightAlpha = 1f;

    // Lines float a little above the surface and follow it every two terrain samples.
    private const float GridLift = 0.6f;
    private const float HighlightLift = 0.9f;
    private const float SampleSpacing = 4f;

    private readonly MeshInstance3D _grid;
    private readonly MeshInstance3D _highlight;
    private RegionGrid? _regions;
    private TerrainField? _field;
    private int? _highlightedRegion;

    /// <summary>Creates the (empty, hidden) line meshes.</summary>
    /// <param name="parent">Node that will hold the overlay.</param>
    /// <param name="palette">Line colours.</param>
    public RegionBoundaryOverlay(Node3D parent, BiomePalette palette)
    {
        _grid = CreateLines(parent, "RegionGrid", BoundaryMaterialPath, palette.RegionBoundary.ToGodot(BoundaryAlpha));
        _highlight = CreateLines(parent, "RegionHighlight", HighlightMaterialPath, palette.RegionHighlight.ToGodot(HighlightAlpha));
        Visible = false;
    }

    /// <summary>Whether the grid and the hover outline are drawn.</summary>
    public bool Visible
    {
        get => _grid.Visible;
        set
        {
            _grid.Visible = value;
            _highlight.Visible = value;
        }
    }

    /// <summary>Rebuilds the grid lines for a new world.</summary>
    /// <param name="regions">Region grid.</param>
    /// <param name="field">Terrain the lines follow.</param>
    public void Build(RegionGrid regions, TerrainField field)
    {
        _regions = regions;
        _field = field;
        _grid.Mesh = ToLineMesh(RegionBoundaryBuilder.BuildGridLines(regions, field, GridLift, SampleSpacing));
        _highlightedRegion = null;
        _highlight.Mesh = null;
    }

    /// <summary>Outlines a region, or clears the outline when <paramref name="regionId"/> is <c>null</c>.</summary>
    /// <param name="regionId">Region under the cursor.</param>
    public void SetHighlightedRegion(int? regionId)
    {
        if (regionId == _highlightedRegion || _regions is null || _field is null)
        {
            return;
        }

        _highlightedRegion = regionId;
        _highlight.Mesh = regionId is { } id
            ? ToLineMesh(RegionBoundaryBuilder.BuildOutline(_regions, id, _field, HighlightLift, SampleSpacing))
            : null;
    }

    /// <summary>Adds a line mesh node whose material colour comes from the palette.</summary>
    private static MeshInstance3D CreateLines(Node3D parent, string name, string materialPath, Color color)
    {
        var material = GD.Load<ShaderMaterial>(materialPath);
        material.SetShaderParameter(LineColorParameter, color);
        var lines = new MeshInstance3D
        {
            Name = name,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(lines);
        return lines;
    }

    /// <summary>Uploads line-list vertices (pairs form segments).</summary>
    private static ArrayMesh ToLineMesh(NVector3[] vertices)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToGodot();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        return mesh;
    }
}
