using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Client.Presentation.Land;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// A village's fields (ADR-077): each a patch of the ground itself, its corners and middle laid on the
/// land so that no slope pokes through it, tinted by its crop and worked by the field shader. One mesh
/// for the village, rebuilt when its fields change.
/// </summary>
internal sealed class FieldView
{
    private const string ShaderPath = "res://Shaders/field.gdshader";

    // Cells per side of a field's patch.
    private const int Cells = 6;

    private static ShaderMaterial? _material;

    private readonly float _size;
    private readonly Color[] _crops;
    private readonly TerrainField _terrain;
    private readonly MeshInstance3D _node;

    /// <summary>Creates the empty fields under <paramref name="root"/>.</summary>
    /// <param name="root">The settlement's node.</param>
    /// <param name="layout">Layout settings, for the fields' size and crops.</param>
    /// <param name="terrain">Terrain they lie on.</param>
    public FieldView(Node3D root, SettlementLayoutSettings layout, TerrainField terrain)
    {
        (_size, _terrain) = (layout.FieldSize, terrain);
        _crops = layout.CropColors.Select(color => color.ToGodot().SrgbToLinear()).ToArray();
        _node = new MeshInstance3D { Name = "Fields", MaterialOverride = _material ??= Make(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(_node);
    }

    /// <summary>Draws the fields.</summary>
    /// <param name="fields">Where they lie.</param>
    public void Show(IReadOnlyList<FieldPlacement> fields)
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        tool.SetNormal(Vector3.Up);
        var vertices = 0;
        foreach (var field in fields)
        {
            tool.SetColor(_crops[Math.Clamp((int)(field.Variant * _crops.Length), 0, _crops.Length - 1)]);
            var (along, across) = (new Vector2(Mathf.Sin(field.Yaw), Mathf.Cos(field.Yaw)), new Vector2(Mathf.Cos(field.Yaw), -Mathf.Sin(field.Yaw)));
            for (var v = 0; v <= Cells; v++)
            {
                for (var u = 0; u <= Cells; u++)
                {
                    var (s, t) = ((float)u / Cells, (float)v / Cells);
                    var (x, z) = (field.Position.X + (((across.X * (s - 0.5f)) + (along.X * (t - 0.5f))) * _size), field.Position.Z + (((across.Y * (s - 0.5f)) + (along.Y * (t - 0.5f))) * _size));
                    tool.SetUV(new Vector2(s, t));
                    tool.AddVertex(new Vector3(x, Mathf.Max(_terrain.SampleHeight(x, z), 0f) + RoadLayout.Lift, z));
                }
            }

            for (var v = 0; v < Cells; v++)
            {
                for (var u = 0; u < Cells; u++)
                {
                    var (a, b) = (vertices + (v * (Cells + 1)) + u, vertices + ((v + 1) * (Cells + 1)) + u);
                    foreach (var index in new[] { a, b, a + 1, a + 1, b, b + 1 })
                    {
                        tool.AddIndex(index);
                    }
                }
            }

            vertices += (Cells + 1) * (Cells + 1);
        }

        _node.Mesh = fields.Count == 0 ? null : tool.Commit();
    }

    private static ShaderMaterial Make()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        material.SetShaderParameter("noise", PlantTextures.Noise);
        return material;
    }
}
