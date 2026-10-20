using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation.Nature;

/// <summary>Which part of a plant a vertex belongs to, for the plant shader (ADR-073).</summary>
public enum PlantPart
{
    /// <summary>Bark: a trunk, a branch or a stem, textured with the bark.</summary>
    Bark,

    /// <summary>A card of leaves, needles or fronds, cut out of the leaf atlas.</summary>
    Leaves,

    /// <summary>A blade of grass or a reed, shaded from its foot to its tip.</summary>
    Blade,

    /// <summary>A stone, mossy where it faces up.</summary>
    Stone,

    /// <summary>A petal, in the flower's own colour.</summary>
    Petal,
}

/// <summary>
/// A plant built in code (ADR-073): its triangles, their normals, the texture coordinates (bark or the
/// leaf atlas), and for each vertex its part (<see cref="Detail"/>.X) and how much light reaches it
/// (<see cref="Detail"/>.Y: 0 deep in the crown, 1 in the open).
/// </summary>
/// <param name="Positions">Vertex positions, in metres, the plant standing at the origin.</param>
/// <param name="Normals">Vertex normals.</param>
/// <param name="Uv">Texture coordinates: the bark's, wrapping around and along, or a place in the leaf atlas.</param>
/// <param name="Detail">The part (X) and the light (Y).</param>
/// <param name="Indices">Triangles, three indices each, wound for Godot.</param>
public sealed record PlantMesh(Vector3[] Positions, Vector3[] Normals, Vector2[] Uv, Vector2[] Detail, int[] Indices)
{
    /// <summary>How many triangles it has.</summary>
    public int Triangles => Indices.Length / 3;

    /// <summary>Its height above the ground, in metres.</summary>
    public float Height
    {
        get
        {
            var top = 0f;
            foreach (var position in Positions)
            {
                top = MathF.Max(top, position.Y);
            }

            return top;
        }
    }
}

/// <summary>
/// Builds plants (ADR-073): bark along a curved spine, tapering ring by ring with its texture wrapped
/// round it; cards of leaves cut from a cell of the leaf atlas, lit as if they were the surface of the
/// crown they belong to; blades; and stones. Pure.
/// </summary>
public sealed class PlantBuilder
{
    private readonly List<Vector3> _positions = [];
    private readonly List<Vector3> _normals = [];
    private readonly List<Vector2> _uv = [];
    private readonly List<Vector2> _detail = [];
    private readonly List<int> _indices = [];

    /// <summary>Bark along a spine: rings of <paramref name="sides"/> about each point, of its radius.</summary>
    /// <param name="spine">The spine's points, from the base.</param>
    /// <param name="radii">Each point's radius.</param>
    /// <param name="sides">Sides about.</param>
    /// <param name="light">How much light reaches it (0..1).</param>
    /// <param name="part">The part: bark, or a blade or reed.</param>
    public void Limb(IReadOnlyList<Vector3> spine, IReadOnlyList<float> radii, int sides, float light, PlantPart part = PlantPart.Bark)
    {
        ArgumentNullException.ThrowIfNull(spine);
        ArgumentNullException.ThrowIfNull(radii);
        if (spine.Count < 2)
        {
            return;
        }

        var start = _positions.Count;
        var along = 0f;
        var reference = MathF.Abs(Vector3.Normalize(spine[1] - spine[0]).Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
        for (var i = 0; i < spine.Count; i++)
        {
            var axis = Vector3.Normalize(spine[Math.Min(spine.Count - 1, i + 1)] - spine[Math.Max(0, i - 1)]);
            var across = Vector3.Normalize(Vector3.Cross(reference, axis));
            if (float.IsNaN(across.X))
            {
                across = Vector3.UnitZ;
            }

            var deep = Vector3.Cross(axis, across);
            along += i == 0 ? 0f : Vector3.Distance(spine[i], spine[i - 1]);
            var girth = MathF.Max(0.02f, MathF.Tau * radii[i]);
            for (var s = 0; s <= sides; s++)
            {
                var angle = MathF.Tau * s / sides;
                var radial = (across * MathF.Cos(angle)) + (deep * MathF.Sin(angle));
                // The bark's texture is one tile about a limb at its foot and keeps its grain's aspect along it.
                Add(spine[i] + (radial * radii[i]), radial, new Vector2(s / (float)sides, along / girth * 0.5f), part, light);
            }
        }

        for (var i = 0; i < spine.Count - 1; i++)
        {
            for (var s = 0; s < sides; s++)
            {
                var k = start + (i * (sides + 1)) + s;
                Quad(k, k + 1, k + sides + 2, k + sides + 1);
            }
        }
    }

    /// <summary>
    /// A card of leaves: a quad cut from a cell of the leaf atlas, its normals bent out from the
    /// crown's middle so the crown is lit as one rounded mass rather than as flat cards.
    /// </summary>
    /// <param name="centre">The card's middle.</param>
    /// <param name="right">Its width's direction, of unit length.</param>
    /// <param name="up">Its height's direction, of unit length.</param>
    /// <param name="width">Its width, in metres.</param>
    /// <param name="height">Its height, in metres.</param>
    /// <param name="cell">The atlas cell it shows.</param>
    /// <param name="crown">The crown's middle, its normals point away from.</param>
    /// <param name="light">How much light reaches it (0..1).</param>
    /// <param name="bend">How far its normals bend towards the crown's (0 flat, 1 wholly the crown's).</param>
    public void Card(Vector3 centre, Vector3 right, Vector3 up, float width, float height, LeafCell cell, Vector3 crown, float light, float bend = 0.8f)
    {
        var start = _positions.Count;
        var face = Vector3.Normalize(Vector3.Cross(up, right));
        var (u0, v0, u1, v1) = LeafAtlas.Bounds(cell);
        var corners = new[] { (-0.5f, -0.5f, u0, v1), (0.5f, -0.5f, u1, v1), (0.5f, 0.5f, u1, v0), (-0.5f, 0.5f, u0, v0) };
        foreach (var (x, y, u, v) in corners)
        {
            var position = centre + (right * (x * width)) + (up * (y * height));
            var outward = position - crown;
            var round = outward.LengthSquared() > 1e-6f ? Vector3.Normalize(outward) : Vector3.UnitY;
            // A card facing away from the crown's side still faces out with it.
            var flat = Vector3.Dot(face, round) < 0f ? -face : face;
            Add(position, Vector3.Normalize(Vector3.Lerp(flat, round, bend)), new Vector2(u, v), PlantPart.Leaves, light);
        }

        Quad(start, start + 1, start + 2, start + 3);
    }

    /// <summary>A strip of cards along a curve, one cell stretched along it: a palm's frond or a willow's switch.</summary>
    /// <param name="spine">The curve's points.</param>
    /// <param name="widths">Its width at each point.</param>
    /// <param name="side">The strip's width direction at its root, of unit length.</param>
    /// <param name="cell">The atlas cell.</param>
    /// <param name="crown">The crown's middle.</param>
    /// <param name="light">How much light reaches it (0..1).</param>
    public void Strip(IReadOnlyList<Vector3> spine, IReadOnlyList<float> widths, Vector3 side, LeafCell cell, Vector3 crown, float light)
    {
        ArgumentNullException.ThrowIfNull(spine);
        ArgumentNullException.ThrowIfNull(widths);
        var (u0, v0, u1, v1) = LeafAtlas.Bounds(cell);
        var start = _positions.Count;
        for (var i = 0; i < spine.Count; i++)
        {
            var v = v1 + ((v0 - v1) * i / (spine.Count - 1f));
            var round = Vector3.Normalize(spine[i] - crown + new Vector3(0f, 0.3f, 0f));
            Add(spine[i] - (side * (widths[i] * 0.5f)), round, new Vector2(u0, v), PlantPart.Leaves, light);
            Add(spine[i] + (side * (widths[i] * 0.5f)), round, new Vector2(u1, v), PlantPart.Leaves, light);
        }

        for (var i = 0; i < spine.Count - 1; i++)
        {
            var k = start + (i * 2);
            Quad(k, k + 1, k + 3, k + 2);
        }
    }

    /// <summary>A flat triangle, for blades and petals: each corner with its own normal and light.</summary>
    /// <param name="a">One corner.</param>
    /// <param name="b">The next.</param>
    /// <param name="c">The last.</param>
    /// <param name="normal">The normal at every corner.</param>
    /// <param name="part">The part.</param>
    /// <param name="lights">The light at each corner: for a blade, its height up the blade.</param>
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, PlantPart part, (float A, float B, float C) lights)
    {
        var start = _positions.Count;
        Add(a, normal, Vector2.Zero, part, lights.A);
        Add(b, normal, Vector2.Zero, part, lights.B);
        Add(c, normal, Vector2.Zero, part, lights.C);
        _indices.Add(start);
        _indices.Add(start + 1);
        _indices.Add(start + 2);
    }

    /// <summary>A stone: an icosahedron split once and pushed in and out at each corner, flattened onto the ground.</summary>
    /// <param name="size">Its radius, in metres.</param>
    /// <param name="flatness">Its height as a share of its width.</param>
    /// <param name="random">Its own randomness, for its shape.</param>
    public void Stone(float size, float flatness, ref PlantRandom random)
    {
        var (corners, faces) = Icosphere();
        var start = _positions.Count;
        var bumps = new float[corners.Count];
        for (var i = 0; i < corners.Count; i++)
        {
            bumps[i] = 0.75f + (0.45f * random.Next());
        }

        for (var i = 0; i < corners.Count; i++)
        {
            var c = corners[i] * bumps[i];
            var position = new Vector3(c.X * size, MathF.Max(-0.15f, c.Y) * size * flatness, c.Z * size);
            Add(position, Vector3.Normalize(new Vector3(corners[i].X, corners[i].Y / MathF.Max(0.3f, flatness), corners[i].Z)), new Vector2(position.X, position.Z), PlantPart.Stone, 0.5f + (0.5f * MathF.Max(0f, corners[i].Y)));
        }

        foreach (var (a, b, c) in faces)
        {
            _indices.Add(start + a);
            _indices.Add(start + c);
            _indices.Add(start + b);
        }
    }

    /// <summary>The plant made so far.</summary>
    /// <returns>Its mesh.</returns>
    public PlantMesh Finish() => new([.. _positions], [.. _normals], [.. _uv], [.. _detail], [.. _indices]);

    /// <summary>Adds a vertex.</summary>
    private void Add(Vector3 position, Vector3 normal, Vector2 uv, PlantPart part, float light)
    {
        _positions.Add(position);
        _normals.Add(normal);
        _uv.Add(uv);
        _detail.Add(new Vector2((int)part, Math.Clamp(light, 0f, 1f)));
    }

    /// <summary>A quad as two triangles, wound for Godot's clockwise front faces.</summary>
    private void Quad(int a, int b, int c, int d)
    {
        _indices.Add(a);
        _indices.Add(c);
        _indices.Add(b);
        _indices.Add(a);
        _indices.Add(d);
        _indices.Add(c);
    }

    /// <summary>An icosahedron's corners split once (42 points on the unit sphere) and its 80 faces.</summary>
    private static (List<Vector3> Corners, List<(int, int, int)> Faces) Icosphere()
    {
        var t = (1f + MathF.Sqrt(5f)) / 2f;
        var corners = new List<Vector3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
            new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        for (var i = 0; i < corners.Count; i++)
        {
            corners[i] = Vector3.Normalize(corners[i]);
        }

        (int, int, int)[] faces =
        [
            (0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
            (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1),
        ];
        var middles = new Dictionary<(int, int), int>();
        int Middle(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (!middles.TryGetValue(key, out var index))
            {
                index = corners.Count;
                corners.Add(Vector3.Normalize((corners[a] + corners[b]) / 2f));
                middles[key] = index;
            }

            return index;
        }

        var split = new List<(int, int, int)>();
        foreach (var (a, b, c) in faces)
        {
            var (ab, bc, ca) = (Middle(a, b), Middle(b, c), Middle(c, a));
            split.AddRange([(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]);
        }

        return (corners, split);
    }
}

/// <summary>A plant's own randomness: a small xorshift stream from a seed, so the same plant is always grown the same. Pure.</summary>
/// <param name="state">The stream's state; never 0.</param>
public struct PlantRandom(ulong state)
{
    private ulong _state = state == 0 ? 0x9E3779B97F4A7C15UL : state;

    /// <summary>A stream for a plant.</summary>
    /// <param name="a">One part of its key, such as its kind.</param>
    /// <param name="b">Another, such as its variant.</param>
    /// <returns>The stream.</returns>
    public static PlantRandom For(ulong a, ulong b) => new(PresentationHash.Mix(0x7A11, a, b));

    /// <summary>The next value in 0..1.</summary>
    /// <returns>The value.</returns>
    public float Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 7;
        _state ^= _state << 17;
        return (_state >> 40) / (float)(1UL << 24);
    }

    /// <summary>A value in a range.</summary>
    /// <param name="from">The least.</param>
    /// <param name="to">The most.</param>
    /// <returns>The value.</returns>
    public float Range(float from, float to) => from + ((to - from) * Next());
}
