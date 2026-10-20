using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation.Animals;

/// <summary>A mesh made from a shape (ADR-061): positions, smooth normals, painted colours and triangles.</summary>
/// <param name="Positions">The vertices.</param>
/// <param name="Normals">Their normals, from the shape's gradient.</param>
/// <param name="Colors">Their colours (sRGB, 0..1).</param>
/// <param name="Indices">Triangles, three indices each, wound counter-clockwise seen from outside.</param>
public sealed record MeshData(Vector3[] Positions, Vector3[] Normals, Vector3[] Colors, int[] Indices);

/// <summary>
/// Turns a signed-distance shape into a smooth mesh by naive surface nets (ADR-061): one vertex in
/// every grid cell the surface passes through, at the mean of where it crosses the cell's edges, and a
/// quad across every grid edge it crosses. Normals come from the shape's gradient, so the surface
/// shades smoothly at any grid size.
/// </summary>
public static class SurfaceNets
{
    private static readonly (int A, int B)[] CellEdges =
    [
        (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7),
    ];

    /// <summary>Meshes a shape.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="cell">The grid's cell size, in the shape's units.</param>
    /// <param name="paint">The colour at a point on the surface, by the paint there.</param>
    /// <returns>The mesh.</returns>
    public static MeshData Mesh(SdfShape shape, float cell, Func<PaintTag, Vector3, Vector3> paint)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(paint);
        if (!(cell > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, "The cell size must be positive.");
        }

        var (lo, hi) = shape.Bounds();
        var origin = lo - new Vector3(2f * cell);
        var size = hi - lo + new Vector3(4f * cell);
        var nx = (int)MathF.Ceiling(size.X / cell) + 1;
        var ny = (int)MathF.Ceiling(size.Y / cell) + 1;
        var nz = (int)MathF.Ceiling(size.Z / cell) + 1;
        if ((long)nx * ny * nz > 4_000_000)
        {
            throw new ArgumentException("The grid would be too fine for the shape.", nameof(cell));
        }

        var field = new float[nx * ny * nz];
        for (var z = 0; z < nz; z++)
        {
            for (var y = 0; y < ny; y++)
            {
                for (var x = 0; x < nx; x++)
                {
                    field[Index(x, y, z, nx, ny)] = shape.Distance(origin + (new Vector3(x, y, z) * cell));
                }
            }
        }

        var cellVertex = new int[(nx - 1) * (ny - 1) * (nz - 1)];
        Array.Fill(cellVertex, -1);
        var positions = new List<Vector3>();
        Span<float> corner = stackalloc float[8];
        for (var z = 0; z < nz - 1; z++)
        {
            for (var y = 0; y < ny - 1; y++)
            {
                for (var x = 0; x < nx - 1; x++)
                {
                    var inside = 0;
                    for (var c = 0; c < 8; c++)
                    {
                        corner[c] = field[Index(x + (c & 1), y + ((c >> 1) & 1), z + ((c >> 2) & 1), nx, ny)];
                        if (corner[c] < 0f)
                        {
                            inside++;
                        }
                    }

                    if (inside is 0 or 8)
                    {
                        continue;
                    }

                    var sum = Vector3.Zero;
                    var crossings = 0;
                    foreach (var (a, b) in CellEdges)
                    {
                        if ((corner[a] < 0f) == (corner[b] < 0f))
                        {
                            continue;
                        }

                        var t = corner[a] / (corner[a] - corner[b]);
                        sum += Vector3.Lerp(Corner(a), Corner(b), t);
                        crossings++;
                    }

                    cellVertex[Index(x, y, z, nx - 1, ny - 1)] = positions.Count;
                    positions.Add(origin + ((new Vector3(x, y, z) + (sum / crossings)) * cell));
                }
            }
        }

        var indices = new List<int>();
        for (var z = 1; z < nz - 1; z++)
        {
            for (var y = 1; y < ny - 1; y++)
            {
                for (var x = 1; x < nx - 1; x++)
                {
                    var here = field[Index(x, y, z, nx, ny)] < 0f;

                    // The edge from this point along +X is shared by the cells at (x, y-1..y, z-1..z).
                    if (x < nx - 1 && here != (field[Index(x + 1, y, z, nx, ny)] < 0f))
                    {
                        Quad(indices, cellVertex, nx, ny, (x, y - 1, z - 1), (x, y, z - 1), (x, y, z), (x, y - 1, z), here);
                    }

                    if (y < ny - 1 && here != (field[Index(x, y + 1, z, nx, ny)] < 0f))
                    {
                        Quad(indices, cellVertex, nx, ny, (x - 1, y, z - 1), (x - 1, y, z), (x, y, z), (x, y, z - 1), here);
                    }

                    if (z < nz - 1 && here != (field[Index(x, y, z + 1, nx, ny)] < 0f))
                    {
                        Quad(indices, cellVertex, nx, ny, (x - 1, y - 1, z), (x, y - 1, z), (x, y, z), (x - 1, y, z), here);
                    }
                }
            }
        }

        var normals = new Vector3[positions.Count];
        var colors = new Vector3[positions.Count];
        var step = cell * 0.5f;
        for (var i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            var gradient = new Vector3(
                shape.Distance(p + new Vector3(step, 0f, 0f)) - shape.Distance(p - new Vector3(step, 0f, 0f)),
                shape.Distance(p + new Vector3(0f, step, 0f)) - shape.Distance(p - new Vector3(0f, step, 0f)),
                shape.Distance(p + new Vector3(0f, 0f, step)) - shape.Distance(p - new Vector3(0f, 0f, step)));
            normals[i] = gradient.LengthSquared() > 1e-12f ? Vector3.Normalize(gradient) : Vector3.UnitY;
            colors[i] = paint(shape.TagAt(p), p);
        }

        return new MeshData([.. positions], normals, colors, [.. indices]);

        static Vector3 Corner(int c) => new(c & 1, (c >> 1) & 1, (c >> 2) & 1);
    }

    /// <summary>A quad of four cells' vertices, as two triangles facing out of the shape.</summary>
    private static void Quad(List<int> indices, int[] cellVertex, int nx, int ny, (int X, int Y, int Z) a, (int X, int Y, int Z) b, (int X, int Y, int Z) c, (int X, int Y, int Z) d, bool insideFirst)
    {
        var va = cellVertex[Index(a.X, a.Y, a.Z, nx - 1, ny - 1)];
        var vb = cellVertex[Index(b.X, b.Y, b.Z, nx - 1, ny - 1)];
        var vc = cellVertex[Index(c.X, c.Y, c.Z, nx - 1, ny - 1)];
        var vd = cellVertex[Index(d.X, d.Y, d.Z, nx - 1, ny - 1)];
        if (va < 0 || vb < 0 || vc < 0 || vd < 0)
        {
            return;
        }

        if (insideFirst)
        {
            indices.AddRange([va, vb, vc, va, vc, vd]);
        }
        else
        {
            indices.AddRange([va, vc, vb, va, vd, vc]);
        }
    }

    private static int Index(int x, int y, int z, int nx, int ny) => x + (nx * (y + (ny * z)));
}
