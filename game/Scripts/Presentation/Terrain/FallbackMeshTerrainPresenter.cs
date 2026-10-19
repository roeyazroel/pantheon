using System.Globalization;
using Godot;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Terrain;

/// <summary>
/// Fallback terrain renderer, used when Terrain3D is unavailable or <c>--terrain=fallback</c> is
/// given: vertex-coloured mesh chunks built from the same <see cref="TerrainField"/> and colours.
/// The chunks stream their detail (spec §84, ADR-050): each is built coarse, every second sample,
/// and those near the camera are rebuilt from every sample, their borders matched to the coarse.
/// </summary>
public sealed class FallbackMeshTerrainPresenter : ITerrainPresenter
{
    private const string MaterialPath = "res://Materials/terrain_fallback.tres";
    private const int CellsPerChunk = 64;

    // Every second sample (4 m cells at the default spacing) far off; every sample near the camera.
    private const int CoarseStride = 2;

    private readonly Node3D _parent;
    private readonly Material _material;
    private readonly string _reason;
    private readonly StreamingSettings _streaming;
    private MeshInstance3D[] _chunks = [];
    private ArrayMesh[] _coarse = [];
    private ChunkStreamer? _streamer;
    private TerrainField? _field;
    private byte[] _rgba = [];

    /// <summary>Creates the presenter; nothing is built until <see cref="Build"/>.</summary>
    /// <param name="parent">Node that will hold the terrain chunks.</param>
    /// <param name="reason">Why the fallback is used, shown with the backend name.</param>
    /// <param name="streaming">How near the camera the terrain is drawn in full detail.</param>
    public FallbackMeshTerrainPresenter(Node3D parent, string reason, StreamingSettings streaming)
    {
        _parent = parent;
        _material = GD.Load<Material>(MaterialPath);
        _reason = reason;
        _streaming = streaming;
    }

    /// <inheritdoc />
    public string BackendName => $"Fallback mesh ({_reason})";

    /// <inheritdoc />
    public bool IsFallback => true;

    /// <inheritdoc />
    public (int Detailed, int Chunks)? Detail => (_streamer?.Loaded.Count ?? 0, _chunks.Length);

    /// <inheritdoc />
    public void Build(TerrainField field, BiomePalette palette)
    {
        Clear();
        _field = field;
        _rgba = TerrainColorizer.ComputeRgba(field, palette);
        var chunks = TerrainMeshBuilder.ChunksPerSide(field, CellsPerChunk, CoarseStride);
        _chunks = new MeshInstance3D[chunks * chunks];
        _coarse = new ArrayMesh[chunks * chunks];
        _streamer = new ChunkStreamer(chunks, CellsPerChunk * CoarseStride * field.Spacing, _streaming.TerrainDetailRadius, _streaming.TerrainCoarseRadius);
        for (var z = 0; z < chunks; z++)
        {
            for (var x = 0; x < chunks; x++)
            {
                var index = (z * chunks) + x;
                _coarse[index] = ToArrayMesh(TerrainMeshBuilder.BuildChunk(field, _rgba, x, z, CellsPerChunk, CoarseStride));
                _chunks[index] = new MeshInstance3D
                {
                    Name = string.Create(CultureInfo.InvariantCulture, $"Chunk_{x}_{z}"),
                    Mesh = _coarse[index],
                    MaterialOverride = _material,
                };
                _parent.AddChild(_chunks[index]);
            }
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        _parent.FreeChildren();
        (_chunks, _coarse, _streamer, _field) = ([], [], null, null);
    }

    /// <inheritdoc />
    public void Stream(Vector3 camera)
    {
        if (_streamer is null || _field is null)
        {
            return;
        }

        var step = _streamer.Update(new System.Numerics.Vector3(camera.X, camera.Y, camera.Z), _streaming.TerrainChunksPerFrame);
        foreach (var chunk in step.Unload)
        {
            _chunks[chunk].Mesh = _coarse[chunk];
        }

        var perSide = (int)Mathf.Sqrt(_chunks.Length);
        foreach (var chunk in step.Load)
        {
            // The same footprint at every sample: twice the cells at a stride of one.
            var data = TerrainMeshBuilder.BuildChunk(_field, _rgba, chunk % perSide, chunk / perSide, CellsPerChunk * CoarseStride, 1, edgeStride: CoarseStride);
            _chunks[chunk].Mesh = ToArrayMesh(data);
        }
    }

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
