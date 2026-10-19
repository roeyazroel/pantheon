using System;
using Godot;

namespace Pantheon.Client.Presentation.Land;

/// <summary>
/// The two low-poly tree meshes (ADR-033), made in code: a conifer of two stacked cones and a
/// broadleaf with a faceted crown, each on a trunk. The trunk's vertices carry its brown and UV.x 0;
/// the crown's carry UV.x 1, for the tree shader to tint.
/// </summary>
internal static class TreeMeshes
{
    private const int Sides = 7;
    // The shader takes colours as linear.
    private static readonly Color Trunk = new Color(0.36f, 0.26f, 0.16f).SrgbToLinear();
    private static readonly Color Crown = new(1f, 1f, 1f);

    /// <summary>A conifer about 5.5 m tall.</summary>
    public static ArrayMesh Conifer()
    {
        var tool = Begin();
        Frustum(tool, 0f, 0.22f, 1.6f, 0.16f, Trunk);
        Frustum(tool, 1.1f, 1.55f, 4.3f, 0f, Crown);
        Frustum(tool, 2.6f, 1.1f, 5.6f, 0f, Crown);
        return End(tool);
    }

    /// <summary>A broadleaf about 5 m tall.</summary>
    public static ArrayMesh Broadleaf()
    {
        var tool = Begin();
        Frustum(tool, 0f, 0.25f, 2.3f, 0.18f, Trunk);
        Frustum(tool, 1.8f, 0.3f, 3.4f, 1.9f, Crown);
        Frustum(tool, 3.4f, 1.9f, 5.0f, 0f, Crown);
        return End(tool);
    }

    /// <summary>A surface of triangles with flat shading.</summary>
    private static SurfaceTool Begin()
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        tool.SetSmoothGroup(uint.MaxValue);
        return tool;
    }

    /// <summary>The finished mesh, with normals.</summary>
    private static ArrayMesh End(SurfaceTool tool)
    {
        tool.GenerateNormals();
        return tool.Commit();
    }

    /// <summary>The sides of a frustum from one height and radius to another (a cone at radius 0), with its bottom capped.</summary>
    private static void Frustum(SurfaceTool tool, float bottom, float bottomRadius, float top, float topRadius, Color color)
    {
        tool.SetColor(color);
        tool.SetUV(new Vector2(color == Crown ? 1f : 0f, 0f));
        for (var i = 0; i < Sides; i++)
        {
            var (a, b) = (MathF.Tau * i / Sides, MathF.Tau * (i + 1) / Sides);
            var (lowA, lowB) = (Ring(a, bottomRadius, bottom), Ring(b, bottomRadius, bottom));
            var (highA, highB) = (Ring(a, topRadius, top), Ring(b, topRadius, top));
            Triangle(tool, lowA, lowB, highB);
            if (topRadius > 0f)
            {
                Triangle(tool, lowA, highB, highA);
            }

            Triangle(tool, new Vector3(0f, bottom, 0f), lowB, lowA);
        }
    }

    /// <summary>A point on a horizontal ring.</summary>
    private static Vector3 Ring(float angle, float radius, float height) => new(MathF.Cos(angle) * radius, height, MathF.Sin(angle) * radius);

    /// <summary>One triangle, wound clockwise as seen from outside, Godot's front face.</summary>
    private static void Triangle(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c)
    {
        tool.AddVertex(a);
        tool.AddVertex(b);
        tool.AddVertex(c);
    }
}
