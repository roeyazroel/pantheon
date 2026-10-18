using System.Globalization;
using Godot;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Terrain;

/// <summary>
/// Fallback terrain renderer, used when Terrain3D is unavailable or <c>--terrain=fallback</c> is
/// given: vertex-coloured mesh chunks built from the same <see cref="TerrainField"/> and colours.
/// </summary>
public sealed class FallbackMeshTerrainPresenter : ITerrainPresenter
{
    private const string MaterialPath = "res://Materials/terrain_fallback.tres";
    private const int CellsPerChunk = 64;

    // Every second sample (4 m cells at the default spacing) keeps the build fast.
    private const int Stride = 2;

    private readonly Node3D _parent;
    private readonly Material _material;
    private readonly string _reason;

    /// <summary>Creates the presenter; nothing is built until <see cref="Build"/>.</summary>
    /// <param name="parent">Node that will hold the terrain chunks.</param>
    /// <param name="reason">Why the fallback is used, shown with the backend name.</param>
    public FallbackMeshTerrainPresenter(Node3D parent, string reason)
    {
        _parent = parent;
        _material = GD.Load<Material>(MaterialPath);
        _reason = reason;
    }

    /// <inheritdoc />
    public string BackendName => $"Fallback mesh ({_reason})";

    /// <inheritdoc />
    public bool IsFallback => true;

    /// <inheritdoc />
    public void Build(TerrainField field, BiomePalette palette)
    {
        Clear();
        var rgba = TerrainColorizer.ComputeRgba(field, palette);
        var chunks = TerrainMeshBuilder.ChunksPerSide(field, CellsPerChunk, Stride);
        for (var z = 0; z < chunks; z++)
        {
            for (var x = 0; x < chunks; x++)
            {
                var data = TerrainMeshBuilder.BuildChunk(field, rgba, x, z, CellsPerChunk, Stride);
                _parent.AddChild(new MeshInstance3D
                {
                    Name = string.Create(CultureInfo.InvariantCulture, $"Chunk_{x}_{z}"),
                    Mesh = ToArrayMesh(data),
                    MaterialOverride = _material,
                });
            }
        }
    }

    /// <inheritdoc />
    public void Clear() => _parent.FreeChildren();

    /// <summary>Uploads one chunk as an indexed triangle mesh.</summary>
    private static ArrayMesh ToArrayMesh(TerrainMeshData data)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Positions.ToGodot();
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals.ToGodot();
        arrays[(int)Mesh.ArrayType.Color] = data.Colors.ToGodotColors();
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
