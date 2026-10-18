using System;
using System.Numerics;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Builds the fallback terrain as square chunks of a regular grid. Each chunk is culled on its
/// own, and a sample stride above 1 trades detail for speed.
/// </summary>
public static class TerrainMeshBuilder
{
    /// <summary>Number of chunks along each side for a given chunk size.</summary>
    /// <param name="field">Terrain.</param>
    /// <param name="cellsPerChunk">Grid cells per chunk side.</param>
    /// <param name="stride">Samples skipped per cell (1 = full detail).</param>
    /// <returns>Chunk count per side.</returns>
    public static int ChunksPerSide(TerrainField field, int cellsPerChunk, int stride)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellsPerChunk);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stride);
        var cells = (field.Resolution - 1 + stride - 1) / stride;
        return (cells + cellsPerChunk - 1) / cellsPerChunk;
    }

    /// <summary>Builds one chunk.</summary>
    /// <param name="field">Terrain.</param>
    /// <param name="rgba">Per-sample RGBA8 colours from <see cref="TerrainColorizer.ComputeRgba"/>.</param>
    /// <param name="chunkX">Chunk column.</param>
    /// <param name="chunkZ">Chunk row.</param>
    /// <param name="cellsPerChunk">Grid cells per chunk side.</param>
    /// <param name="stride">Samples per cell.</param>
    /// <returns>The chunk mesh.</returns>
    public static TerrainMeshData BuildChunk(TerrainField field, ReadOnlySpan<byte> rgba, int chunkX, int chunkZ, int cellsPerChunk, int stride)
    {
        var chunks = ChunksPerSide(field, cellsPerChunk, stride);
        if ((uint)chunkX >= (uint)chunks || (uint)chunkZ >= (uint)chunks)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkX), "Chunk coordinates are outside the terrain.");
        }

        if (rgba.Length != field.Resolution * field.Resolution * TerrainColorizer.BytesPerPixel)
        {
            throw new ArgumentException("Colour data must hold one RGBA8 pixel per terrain sample.", nameof(rgba));
        }

        var last = field.Resolution - 1;
        var startX = chunkX * cellsPerChunk * stride;
        var startZ = chunkZ * cellsPerChunk * stride;
        var columns = CellsInChunk(startX, last, cellsPerChunk, stride);
        var rows = CellsInChunk(startZ, last, cellsPerChunk, stride);
        var vertexColumns = columns + 1;
        var vertexCount = vertexColumns * (rows + 1);

        var positions = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var colors = new Vector4[vertexCount];
        for (var row = 0; row <= rows; row++)
        {
            var sampleZ = Math.Min(startZ + (row * stride), last);
            for (var column = 0; column <= columns; column++)
            {
                var sampleX = Math.Min(startX + (column * stride), last);
                var vertex = (row * vertexColumns) + column;
                positions[vertex] = new Vector3(sampleX * field.Spacing, field.GetHeight(sampleX, sampleZ), sampleZ * field.Spacing);
                normals[vertex] = ComputeNormal(field, sampleX, sampleZ, stride);
                colors[vertex] = ReadColor(rgba, (sampleZ * field.Resolution) + sampleX);
            }
        }

        return new TerrainMeshData(positions, normals, colors, BuildIndices(columns, rows));
    }

    /// <summary>Number of cells in a chunk; the last chunk can be smaller.</summary>
    private static int CellsInChunk(int start, int last, int cellsPerChunk, int stride) =>
        Math.Min(cellsPerChunk, (last - start + stride - 1) / stride);

    /// <summary>Normal from central differences over one stride, so it matches the chunk's detail level.</summary>
    private static Vector3 ComputeNormal(TerrainField field, int x, int z, int stride)
    {
        var run = 2f * stride * field.Spacing;
        var dx = (field.GetHeight(x + stride, z) - field.GetHeight(x - stride, z)) / run;
        var dz = (field.GetHeight(x, z + stride) - field.GetHeight(x, z - stride)) / run;
        return Vector3.Normalize(new Vector3(-dx, 1f, -dz));
    }

    /// <summary>Reads one RGBA8 pixel as normalised floats.</summary>
    private static Vector4 ReadColor(ReadOnlySpan<byte> rgba, int sampleIndex)
    {
        var offset = sampleIndex * TerrainColorizer.BytesPerPixel;
        return new Vector4(rgba[offset] / 255f, rgba[offset + 1] / 255f, rgba[offset + 2] / 255f, 1f);
    }

    /// <summary>Two triangles per cell, wound clockwise when seen from above.</summary>
    private static int[] BuildIndices(int columns, int rows)
    {
        var indices = new int[columns * rows * 6];
        var vertexColumns = columns + 1;
        var next = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var topLeft = (row * vertexColumns) + column;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + vertexColumns;
                var bottomRight = bottomLeft + 1;

                indices[next++] = topLeft;
                indices[next++] = topRight;
                indices[next++] = bottomLeft;
                indices[next++] = topRight;
                indices[next++] = bottomRight;
                indices[next++] = bottomLeft;
            }
        }

        return indices;
    }
}
