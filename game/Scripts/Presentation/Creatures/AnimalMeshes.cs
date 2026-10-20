using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Pantheon.Game.Presentation;
using Pantheon.Game.Presentation.Animals;
using NVector3 = System.Numerics.Vector3;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>
/// The meshes of a species' body (ADR-061), made once a run from its plan and look: each part's shape
/// meshed by surface nets on worker threads and painted in its species' colours, then kept for every
/// Creature of that species.
/// </summary>
internal sealed class AnimalMeshes
{
    // Grid cells per body unit: finer for the head, where the face is.
    private const float BodyCell = 0.07f;
    private const float HeadCell = 0.045f;
    private const float LimbCell = 0.055f;

    private static readonly ConcurrentDictionary<string, Task<AnimalMeshes>> Made = new(StringComparer.Ordinal);

    private AnimalMeshes(AnimalPlan plan, IReadOnlyDictionary<string, ArrayMesh> parts)
    {
        Plan = plan;
        Parts = parts;
    }

    /// <summary>The plan the meshes were made from.</summary>
    public AnimalPlan Plan { get; }

    /// <summary>Each part's mesh, by name: torso, head, thigh, shin, foot, upper arm, forearm, hand, tail and tail tip.</summary>
    public IReadOnlyDictionary<string, ArrayMesh> Parts { get; }

    /// <summary>A species' meshes, made on worker threads the first time they are asked for.</summary>
    /// <param name="look">The species' look: its body and colours.</param>
    /// <returns>The meshes, when ready.</returns>
    public static Task<AnimalMeshes> For(CreatureLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        return Made.GetOrAdd(look.Species, _ => Task.Run(() => Make(look)));
    }

    /// <summary>Meshes every part of a species and builds the Godot meshes.</summary>
    private static AnimalMeshes Make(CreatureLook look)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var plan = AnimalPlans.For(look.BodyName);
        var palette = Palette(look);
        var shapes = new List<(string Name, IReadOnlyList<SdfPart> Parts, float Cell)>
        {
            ("Torso", plan.Torso, BodyCell),
            ("Head", plan.Head, HeadCell),
            ("Thigh", plan.Thigh, LimbCell),
            ("Shin", plan.Shin, LimbCell),
            ("Foot", plan.Foot, LimbCell * 0.8f),
            ("UpperArm", plan.UpperArm, LimbCell),
            ("Forearm", plan.Forearm, LimbCell),
            ("Hand", plan.Hand, LimbCell * 0.7f),
        };
        if (plan.TailSegment.Count > 0)
        {
            shapes.Add(("Tail", plan.TailSegment, LimbCell * 0.8f));
        }

        if (plan.TailTip.Count > 0)
        {
            shapes.Add(("TailTip", plan.TailTip, LimbCell * 0.8f));
        }

        var data = new MeshData[shapes.Count];
        Parallel.For(0, shapes.Count, i =>
        {
            var (name, parts, cell) = shapes[i];
            var salt = name.Length + i;
            data[i] = SurfaceNets.Mesh(new SdfShape(parts), cell, (tag, p) => AnimalPaint.Color(tag, p, palette, plan.Pattern, salt));
        });

        var meshes = new Dictionary<string, ArrayMesh>(StringComparer.Ordinal);
        for (var i = 0; i < shapes.Count; i++)
        {
            meshes[shapes[i].Name] = ToMesh(data[i]);
        }

        var vertices = 0;
        foreach (var part in data)
        {
            vertices += part.Positions.Length;
        }

        GD.Print($"Made the {look.Species}'s body ({plan.Name}): {vertices} vertices in {clock.ElapsedMilliseconds} ms");
        return new AnimalMeshes(plan, meshes);
    }

    /// <summary>A Godot mesh from mesh data: vertex colours in sRGB, triangles wound clockwise as Godot's front faces are.</summary>
    private static ArrayMesh ToMesh(MeshData data)
    {
        var count = data.Positions.Length;
        var positions = new Vector3[count];
        var normals = new Vector3[count];
        var colors = new Color[count];
        for (var i = 0; i < count; i++)
        {
            positions[i] = data.Positions[i].ToGodot();
            normals[i] = data.Normals[i].ToGodot();
            var c = data.Colors[i];
            colors[i] = new Color(c.X, c.Y, c.Z);
        }

        var indices = new int[data.Indices.Length];
        for (var i = 0; i < indices.Length; i += 3)
        {
            (indices[i], indices[i + 1], indices[i + 2]) = (data.Indices[i], data.Indices[i + 2], data.Indices[i + 1]);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        if (count > 0)
        {
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }

        return mesh;
    }

    /// <summary>A species' colours from its look.</summary>
    private static AnimalPalette Palette(CreatureLook look)
    {
        static NVector3 V(RgbColor c) => new(c.R, c.G, c.B);
        return new AnimalPalette(V(look.Hide), V(look.Belly), V(look.Muzzle ?? look.Belly), V(look.Nose ?? look.Hide), V(look.InnerEar ?? look.Belly), V(look.Horn ?? look.Belly), V(look.Hoof ?? look.Hide), V(look.Tufts), V(look.Shell ?? look.Hide), V(look.Marking ?? look.Tufts));
    }
}
