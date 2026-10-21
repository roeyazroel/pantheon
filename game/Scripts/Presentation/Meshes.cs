using System.Linq;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation;

/// <summary>Shapes built in code (<see cref="TaggedMesh"/>) as Godot meshes: each vertex's part and slot in its UV.</summary>
internal static class Meshes
{
    /// <summary>A shape's triangles as a Godot mesh.</summary>
    /// <param name="data">The triangles.</param>
    /// <param name="material">The material, if any.</param>
    /// <returns>The mesh.</returns>
    public static ArrayMesh ToArrayMesh(TaggedMesh data, Material? material = null)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Positions.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals.Select(n => new Vector3(n.X, n.Y, n.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = data.Tags.Select(t => new Vector2(t.X, t.Y)).ToArray();
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        if (material is not null)
        {
            mesh.SurfaceSetMaterial(0, material);
        }

        return mesh;
    }

    /// <summary>A plant as an array mesh (ADR-073): its atlas or bark coordinates as UV, its part and light as UV2.</summary>
    /// <param name="data">The plant.</param>
    /// <param name="material">The material to give its surface, if any.</param>
    /// <returns>The mesh.</returns>
    public static ArrayMesh ToArrayMesh(Pantheon.Game.Presentation.Nature.PlantMesh data, Material? material = null)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Positions.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals.Select(n => new Vector3(n.X, n.Y, n.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = data.Uv.Select(t => new Vector2(t.X, t.Y)).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = data.Detail.Select(t => new Vector2(t.X, t.Y)).ToArray();
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        if (material is not null)
        {
            mesh.SurfaceSetMaterial(0, material);
        }

        return mesh;
    }

    /// <summary>The player's angel as an array mesh (ADR-074): its per-part coordinates as UV, its part and side as UV2.</summary>
    /// <param name="data">The angel's figure.</param>
    /// <param name="material">The material to give its surface, if any.</param>
    /// <returns>The mesh.</returns>
    public static ArrayMesh ToArrayMesh(Pantheon.Game.Presentation.Angel.AngelMesh data, Material? material = null)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Positions.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals.Select(n => new Vector3(n.X, n.Y, n.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = data.Uv.Select(t => new Vector2(t.X, t.Y)).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = data.Detail.Select(t => new Vector2(t.X, t.Y)).ToArray();
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        if (material is not null)
        {
            mesh.SurfaceSetMaterial(0, material);
        }

        return mesh;
    }
}
