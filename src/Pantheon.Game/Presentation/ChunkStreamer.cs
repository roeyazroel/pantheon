using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>What one update of a <see cref="ChunkStreamer"/> asks for.</summary>
/// <param name="Load">Chunks to load now, nearest first.</param>
/// <param name="Unload">Chunks to let go.</param>
public sealed record StreamingStep(IReadOnlyList<int> Load, IReadOnlyList<int> Unload);

/// <summary>
/// World streaming (spec §84, ADR-050): which square chunks of the island are near the camera
/// and so carry their detail, and which stay simulation data. A chunk loads when the camera comes
/// within <c>loadRadius</c> of its nearest ground point (in three dimensions, so a camera high up
/// loads only what it could see), nearest first and a few an update; it goes beyond the larger
/// <c>unloadRadius</c>, so a camera at the edge does not make chunks flicker. Pure.
/// </summary>
public sealed class ChunkStreamer
{
    private readonly int _perSide;
    private readonly float _chunkMetres;
    private readonly float _loadRadius;
    private readonly float _unloadRadius;
    private readonly HashSet<int> _loaded = [];

    /// <summary>Creates a streamer over a square of chunks; nothing is loaded yet.</summary>
    /// <param name="chunksPerSide">Chunks along each side of the island.</param>
    /// <param name="chunkMetres">A chunk's side, in metres.</param>
    /// <param name="loadRadius">How near the camera a chunk loads, in metres.</param>
    /// <param name="unloadRadius">How far from the camera a loaded chunk goes, at least the load radius.</param>
    public ChunkStreamer(int chunksPerSide, float chunkMetres, float loadRadius, float unloadRadius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chunksPerSide);
        if (!(chunkMetres > 0f) || !(loadRadius >= 0f) || !(unloadRadius >= loadRadius))
        {
            throw new ArgumentException("A streamer needs chunks of positive size and 0 <= load radius <= unload radius.");
        }

        (_perSide, _chunkMetres, _loadRadius, _unloadRadius) = (chunksPerSide, chunkMetres, loadRadius, unloadRadius);
    }

    /// <summary>All the chunks.</summary>
    public int Count => _perSide * _perSide;

    /// <summary>The chunks loaded now.</summary>
    public IReadOnlyCollection<int> Loaded => _loaded;

    /// <summary>Whether a chunk is loaded.</summary>
    /// <param name="chunk">The chunk.</param>
    /// <returns><c>true</c> when loaded.</returns>
    public bool IsLoaded(int chunk) => _loaded.Contains(chunk);

    /// <summary>Plans an update for the camera's place, and takes the plan as done.</summary>
    /// <param name="camera">The camera's place: X and Z on the ground, Y its height.</param>
    /// <param name="budget">The most chunks to load in this update.</param>
    /// <returns>What to load and unload.</returns>
    public StreamingStep Update(Vector3 camera, int budget)
    {
        var unload = new List<int>();
        foreach (var chunk in _loaded)
        {
            if (Distance(chunk, camera) > _unloadRadius)
            {
                unload.Add(chunk);
            }
        }

        foreach (var chunk in unload)
        {
            _loaded.Remove(chunk);
        }

        // Only the chunks within the load radius on the ground can load, so only those are looked at.
        var wanted = new List<(float Distance, int Chunk)>();
        var (first, last) = (Cell(camera.X - _loadRadius), Cell(camera.X + _loadRadius));
        var (top, bottom) = (Cell(camera.Z - _loadRadius), Cell(camera.Z + _loadRadius));
        for (var row = top; row <= bottom && Math.Abs(camera.Y) <= _loadRadius; row++)
        {
            for (var column = first; column <= last; column++)
            {
                var chunk = (row * _perSide) + column;
                if (!_loaded.Contains(chunk) && Distance(chunk, camera) is var distance && distance <= _loadRadius)
                {
                    wanted.Add((distance, chunk));
                }
            }
        }

        wanted.Sort();
        var load = new List<int>(Math.Min(Math.Max(0, budget), wanted.Count));
        for (var i = 0; i < wanted.Count && load.Count < budget; i++)
        {
            load.Add(wanted[i].Chunk);
            _loaded.Add(wanted[i].Chunk);
        }

        unload.Sort();
        return new StreamingStep(load, unload);
    }

    /// <summary>The chunk column (or row) a ground coordinate falls in, kept on the island.</summary>
    private int Cell(float metres) => Math.Clamp((int)MathF.Floor(metres / _chunkMetres), 0, Math.Max(0, _perSide - 1));

    /// <summary>Lets every chunk go, for a new world.</summary>
    public void Clear() => _loaded.Clear();

    /// <summary>Metres from the camera to a chunk's nearest point at sea level.</summary>
    private float Distance(int chunk, Vector3 camera)
    {
        var (minX, minZ) = (chunk % _perSide * _chunkMetres, chunk / _perSide * _chunkMetres);
        var dx = Math.Max(0f, Math.Max(minX - camera.X, camera.X - (minX + _chunkMetres)));
        var dz = Math.Max(0f, Math.Max(minZ - camera.Z, camera.Z - (minZ + _chunkMetres)));
        return MathF.Sqrt((dx * dx) + (dz * dz) + (camera.Y * camera.Y));
    }
}
