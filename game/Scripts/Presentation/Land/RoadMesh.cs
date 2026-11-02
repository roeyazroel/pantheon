using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Land;

/// <summary>The worn tracks of roads and lanes as meshes (ADR-073, ADR-077), all drawn by the road shader.</summary>
internal static class RoadMesh
{
    private const string ShaderPath = "res://Shaders/road.gdshader";

    private static ShaderMaterial? _material;

    /// <summary>The road shader's material, shared by every track.</summary>
    public static ShaderMaterial Material => _material ??= Make();

    /// <summary>Adds a track: two strips, its left edge to its middle and its middle to its right edge.</summary>
    /// <param name="tool">The surface being built.</param>
    /// <param name="ribbon">The track's cross-sections.</param>
    /// <param name="vertices">The vertices in the surface so far; updated.</param>
    /// <param name="skip">Which stretches between two cross-sections to leave out, if any.</param>
    public static void Add(SurfaceTool tool, IReadOnlyList<RoadSection> ribbon, ref int vertices, Func<RoadSection, RoadSection, bool>? skip = null)
    {
        foreach (var section in ribbon)
        {
            Vertex(tool, section.Left, 0f, section.Along);
            Vertex(tool, section.Centre, 0.5f, section.Along);
            Vertex(tool, section.Right, 1f, section.Along);
        }

        for (var i = 1; i < ribbon.Count; i++)
        {
            if (skip?.Invoke(ribbon[i - 1], ribbon[i]) == true)
            {
                continue;
            }

            var (a, b) = (vertices + ((i - 1) * 3), vertices + (i * 3));
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

        vertices += ribbon.Count * 3;
    }

    /// <summary>Adds a worn disc, its rim ragged as a track's edge is.</summary>
    /// <param name="tool">The surface being built.</param>
    /// <param name="centre">Its middle, on the ground.</param>
    /// <param name="radius">Its radius, in metres.</param>
    /// <param name="height">The ground's height under a point.</param>
    /// <param name="vertices">The vertices in the surface so far; updated.</param>
    public static void Disc(SurfaceTool tool, System.Numerics.Vector2 centre, float radius, Func<float, float, float> height, ref int vertices)
    {
        const int around = 28;
        const int rings = 5;
        for (var ring = 0; ring <= rings; ring++)
        {
            var share = (float)ring / rings;
            for (var i = 0; i <= around; i++)
            {
                var angle = Mathf.Tau * i / around;
                var (x, z) = (centre.X + (Mathf.Cos(angle) * radius * share), centre.Y + (Mathf.Sin(angle) * radius * share));
                tool.SetUV(new Vector2(0.5f * (1f - share), radius * share));
                tool.AddVertex(new Vector3(x, height(x, z) + RoadLayout.Lift, z));
            }
        }

        for (var ring = 0; ring < rings; ring++)
        {
            for (var i = 0; i < around; i++)
            {
                var (a, b) = (vertices + (ring * (around + 1)) + i, vertices + ((ring + 1) * (around + 1)) + i);
                foreach (var index in new[] { a, b, a + 1, a + 1, b, b + 1 })
                {
                    tool.AddIndex(index);
                }
            }
        }

        vertices += (rings + 1) * (around + 1);
    }

    /// <summary>A vertex of a track: across it (0 left, 0.5 middle, 1 right) and metres along it.</summary>
    private static void Vertex(SurfaceTool tool, System.Numerics.Vector3 point, float across, float along)
    {
        tool.SetUV(new Vector2(across, along));
        tool.AddVertex(new Vector3(point.X, point.Y, point.Z));
    }

    private static ShaderMaterial Make()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        material.SetShaderParameter("noise", PlantTextures.Noise);
        return material;
    }
}
