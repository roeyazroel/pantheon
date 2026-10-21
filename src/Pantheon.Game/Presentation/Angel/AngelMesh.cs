using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation.Angel;

/// <summary>Which part of the angel a vertex belongs to, for the angel's shader (ADR-074).</summary>
public enum AngelPart
{
    /// <summary>Its robe and sleeves.</summary>
    Robe,

    /// <summary>Its face and hands.</summary>
    Skin,

    /// <summary>Its hair.</summary>
    Hair,

    /// <summary>A feather of its wings, or the wing behind them.</summary>
    Wing,

    /// <summary>Its halo.</summary>
    Halo,

    /// <summary>The thorns that break from a fallen angel's halo.</summary>
    Spike,

    /// <summary>Its eyes.</summary>
    Eye,

    /// <summary>The sash about its waist.</summary>
    Sash,

    /// <summary>A vengeful god's angel's burning blade.</summary>
    Blade,

    /// <summary>A deceptive god's angel's veil.</summary>
    Veil,

    /// <summary>A cruel god's angel's chains.</summary>
    Chain,

    /// <summary>An ambitious god's angel's crown of thorns.</summary>
    Crown,
}

/// <summary>
/// The angel's figure (ADR-074): a robed body with a head, hair and open hands, two great wings of
/// layered feathers, a halo with thorns hidden in it, and the marks of its god's traits. Positions are
/// in metres, the figure facing +Z with its hem at the origin. <see cref="Uv"/> is a feather's length
/// and breadth, or the halo's angle; <see cref="Detail"/>.X is the part and .Y the side: −1 on the
/// viewer's left (the figure's right), 0 in the middle, 1 on the viewer's right.
/// </summary>
/// <param name="Positions">Vertex positions.</param>
/// <param name="Normals">Vertex normals.</param>
/// <param name="Uv">Per-part coordinates.</param>
/// <param name="Detail">The part (X) and the side (Y).</param>
/// <param name="Indices">Triangles, three indices each.</param>
public sealed record AngelMesh(Vector3[] Positions, Vector3[] Normals, Vector2[] Uv, Vector2[] Detail, int[] Indices)
{
    /// <summary>Where a wing turns at the shoulder, for the shader's wingbeat: the right wing's (+X); the left's is its mirror.</summary>
    public static Vector3 Shoulder { get; } = new(0.14f, 1.5f, -0.1f);

    /// <summary>How many triangles it has.</summary>
    public int Triangles => Indices.Length / 3;

    /// <summary>Builds the figure. Pure.</summary>
    /// <returns>The figure.</returns>
    public static AngelMesh Build()
    {
        var b = new Builder();
        Body(b);
        foreach (var side in new[] { -1f, 1f })
        {
            Wing(b, side);
        }

        Halo(b);
        Marks(b);
        return b.Finish();
    }

    /// <summary>The robe, shoulders, arms, hands, head, hair, eyes and sash.</summary>
    private static void Body(Builder b)
    {
        (float Y, float R)[] robe = [(0f, 0.46f), (0.15f, 0.42f), (0.5f, 0.34f), (0.9f, 0.27f), (1.15f, 0.22f), (1.35f, 0.24f), (1.5f, 0.26f), (1.58f, 0.19f), (1.63f, 0.07f)];
        b.Lathe(robe, 22, 0.75f, AngelPart.Robe);
        b.Ellipsoid(new Vector3(0f, 1.49f, -0.01f), new Vector3(0.3f, 0.1f, 0.19f), AngelPart.Robe, 0f);
        b.Tube([new Vector3(0f, 1.56f, 0f), new Vector3(0f, 1.74f, 0.012f)], [0.055f, 0.05f], 8, AngelPart.Skin, 0f);
        b.Ellipsoid(new Vector3(0f, 1.84f, 0.02f), new Vector3(0.115f, 0.14f, 0.125f), AngelPart.Skin, 0f);
        b.Ellipsoid(new Vector3(0f, 1.885f, -0.016f), new Vector3(0.126f, 0.136f, 0.13f), AngelPart.Hair, 0f, above: 1.79f);
        foreach (var side in new[] { -1f, 1f })
        {
            b.Ellipsoid(new Vector3(side * 0.041f, 1.85f, 0.136f), new Vector3(0.021f, 0.011f, 0.008f), AngelPart.Eye, side);
            b.Tube([new Vector3(side * 0.23f, 1.49f, 0f), new Vector3(side * 0.31f, 1.22f, 0.05f), new Vector3(side * 0.35f, 1.04f, 0.13f), new Vector3(side * 0.37f, 0.97f, 0.17f)], [0.085f, 0.1f, 0.13f, 0.145f], 12, AngelPart.Robe, side);
            b.Ellipsoid(new Vector3(side * 0.37f, 0.95f, 0.19f), new Vector3(0.045f, 0.062f, 0.032f), AngelPart.Skin, side);
        }

        b.Torus(new Vector3(0f, 1.15f, 0f), Vector3.UnitY, new Vector2(0.222f, 0.168f), 0.024f, 28, 6, AngelPart.Sash);
    }

    /// <summary>A wing: a backing between its bone and its feathers' tips, then three layers of feathers from the longest.</summary>
    private static void Wing(Builder b, float side)
    {
        const int Feathers = 22;
        var shoulder = Shoulder with { X = side * Shoulder.X };
        var wrist = new Vector3(side * 0.58f, 1.92f, -0.2f);
        var tip = new Vector3(side * 0.95f, 2.3f, -0.28f);
        Vector3 Bone(float u) => u < 0.6f ? Vector3.Lerp(shoulder, wrist, u / 0.6f) : Vector3.Lerp(wrist, tip, (u - 0.6f) / 0.4f);
        Vector3 Along(float u)
        {
            var angle = (-92f + (136f * u)) * MathF.PI / 180f;
            return Vector3.Normalize(new Vector3(side * MathF.Cos(angle), MathF.Sin(angle), -0.15f));
        }

        float Length(float u) => 0.8f + (0.25f * MathF.Sin(MathF.PI * u));
        var facing = new Vector3(0f, 0f, 1f);
        var bones = new List<Vector3>();
        var tips = new List<Vector3>();
        for (var i = 0; i < Feathers; i++)
        {
            var u = i / (float)(Feathers - 1);
            bones.Add(Bone(u));
            tips.Add(Bone(u) + (Along(u) * Length(u) * 0.9f));
        }

        b.Sheet(bones, tips, facing, AngelPart.Wing, side);
        foreach (var (scale, width, lift) in new[] { (1f, 0.21f, 0f), (0.58f, 0.18f, 0.012f), (0.32f, 0.15f, 0.024f) })
        {
            for (var i = Feathers - 1; i >= 0; i--)
            {
                var u = i / (float)(Feathers - 1);
                var stagger = 1f + (0.07f * (((i * 5) % 3) - 1));
                b.Feather(Bone(u) + (facing * lift), Along(u), Length(u) * scale * stagger, width, facing, side);
            }
        }
    }

    /// <summary>The halo, tilted a little back, and the thorns hidden in it.</summary>
    private static void Halo(Builder b)
    {
        var centre = new Vector3(0f, 2.13f, -0.03f);
        var axis = Vector3.Normalize(new Vector3(0f, 1f, -0.27f));
        b.Torus(centre, axis, new Vector2(0.19f, 0.19f), 0.016f, 40, 6, AngelPart.Halo);
        var across = Vector3.UnitX;
        var depth = Vector3.Normalize(Vector3.Cross(across, axis));
        for (var k = 0; k < 12; k++)
        {
            var angle = k * MathF.Tau / 12f;
            var outward = (across * MathF.Cos(angle)) + (depth * MathF.Sin(angle));
            var root = centre + (outward * 0.19f);
            var height = 0.1f + (0.08f * (((k * 7) % 5) / 4f));
            b.Spike(root, Vector3.Normalize((axis * 0.75f) + (outward * 0.35f)), height, 0.018f, AngelPart.Spike, angle / MathF.Tau);
        }
    }

    /// <summary>The traits' marks: a blade in its hand, a veil, chains at its wrists and a crown of thorns.</summary>
    private static void Marks(Builder b)
    {
        b.Blade(new Vector3(0.37f, 0.93f, 0.21f), Vector3.Normalize(new Vector3(0.03f, -1f, 0.12f)), 0.85f, 0.045f);
        b.Veil(new Vector3(0f, 1.82f, 0.146f), 0.14f, 0.3f);
        foreach (var side in new[] { -1f, 1f })
        {
            for (var link = 0; link < 5; link++)
            {
                var at = new Vector3(side * 0.37f, 0.9f - (link * 0.062f), 0.18f);
                b.Torus(at, link % 2 == 0 ? Vector3.UnitX : Vector3.UnitZ, new Vector2(0.03f, 0.02f), 0.007f, 10, 4, AngelPart.Chain, side);
            }
        }

        var crown = new Vector3(0f, 1.95f, 0.005f);
        b.Torus(crown, Vector3.UnitY, new Vector2(0.128f, 0.135f), 0.01f, 24, 4, AngelPart.Crown);
        for (var k = 0; k < 9; k++)
        {
            var angle = k * MathF.Tau / 9f;
            var outward = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
            b.Spike(crown + new Vector3(outward.X * 0.128f, 0f, outward.Z * 0.135f), Vector3.Normalize(Vector3.UnitY + (outward * 0.25f)), 0.07f, 0.014f, AngelPart.Crown, 0f);
        }
    }

    /// <summary>Gathers the figure's triangles.</summary>
    private sealed class Builder
    {
        private readonly List<Vector3> _positions = [];
        private readonly List<Vector3> _normals = [];
        private readonly List<Vector2> _uv = [];
        private readonly List<Vector2> _detail = [];
        private readonly List<int> _indices = [];

        /// <summary>A body of revolution about the Y axis, flattened front to back, its hem waving a little, closed below.</summary>
        public void Lathe((float Y, float R)[] profile, int sides, float depth, AngelPart part)
        {
            var start = _positions.Count;
            for (var r = 0; r < profile.Length; r++)
            {
                var (y, radius) = profile[r];
                var next = profile[Math.Min(profile.Length - 1, r + 1)];
                var last = profile[Math.Max(0, r - 1)];
                var slope = (last.R - next.R) / MathF.Max(0.01f, next.Y - last.Y);
                for (var s = 0; s <= sides; s++)
                {
                    var angle = s * MathF.Tau / sides;
                    var wave = r == 0 ? 1f + (0.07f * MathF.Sin(angle * 7f)) : 1f;
                    var (sin, cos) = MathF.SinCos(angle);
                    Add(new Vector3(sin * radius * wave, y, cos * radius * depth * wave), Vector3.Normalize(new Vector3(sin, slope * 0.6f, cos / depth)), new Vector2(s / (float)sides, y), part, MathF.Sign(sin) * MathF.Min(1f, MathF.Abs(sin) * 4f));
                }
            }

            for (var r = 0; r < profile.Length - 1; r++)
            {
                for (var s = 0; s < sides; s++)
                {
                    var a = start + (r * (sides + 1)) + s;
                    var c = a + sides + 1;
                    Quad(a, a + 1, c + 1, c);
                }
            }

            var centre = _positions.Count;
            Add(new Vector3(0f, 0.02f, 0f), -Vector3.UnitY, Vector2.Zero, part, 0f);
            for (var s = 0; s < sides; s++)
            {
                Triangle(centre, start + s + 1, start + s);
            }
        }

        /// <summary>An ellipsoid, or its part above a height.</summary>
        public void Ellipsoid(Vector3 centre, Vector3 radii, AngelPart part, float side, float above = float.NegativeInfinity)
        {
            const int Rings = 10;
            const int Segments = 14;
            var start = _positions.Count;
            for (var r = 0; r <= Rings; r++)
            {
                var polar = r * MathF.PI / Rings;
                for (var s = 0; s <= Segments; s++)
                {
                    var azimuth = s * MathF.Tau / Segments;
                    var unit = new Vector3(MathF.Sin(polar) * MathF.Sin(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Cos(azimuth));
                    var point = centre + (unit * radii);
                    Add(point with { Y = MathF.Max(point.Y, above) }, Vector3.Normalize(unit / radii), new Vector2(s / (float)Segments, r / (float)Rings), part, side);
                }
            }

            for (var r = 0; r < Rings; r++)
            {
                for (var s = 0; s < Segments; s++)
                {
                    var a = start + (r * (Segments + 1)) + s;
                    var c = a + Segments + 1;
                    if (_positions[a].Y > above || _positions[c].Y > above)
                    {
                        Quad(a, c, c + 1, a + 1);
                    }
                }
            }
        }

        /// <summary>A tube along a spine, ring by ring.</summary>
        public void Tube(IReadOnlyList<Vector3> spine, IReadOnlyList<float> radii, int sides, AngelPart part, float side)
        {
            var start = _positions.Count;
            for (var i = 0; i < spine.Count; i++)
            {
                var ahead = Vector3.Normalize(spine[Math.Min(spine.Count - 1, i + 1)] - spine[Math.Max(0, i - 1)]);
                var across = Vector3.Normalize(Vector3.Cross(ahead, MathF.Abs(ahead.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY));
                var other = Vector3.Cross(ahead, across);
                for (var s = 0; s <= sides; s++)
                {
                    var (sin, cos) = MathF.SinCos(s * MathF.Tau / sides);
                    var normal = (across * cos) + (other * sin);
                    Add(spine[i] + (normal * radii[i]), normal, new Vector2(s / (float)sides, i / (float)(spine.Count - 1)), part, side);
                }
            }

            for (var i = 0; i < spine.Count - 1; i++)
            {
                for (var s = 0; s < sides; s++)
                {
                    var a = start + (i * (sides + 1)) + s;
                    var c = a + sides + 1;
                    Quad(a, a + 1, c + 1, c);
                }
            }
        }

        /// <summary>A ring about an axis, elliptical, its angle in UV.X.</summary>
        public void Torus(Vector3 centre, Vector3 axis, Vector2 radius, float tube, int segments, int sides, AngelPart part, float side = 0f)
        {
            var across = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Z) > 0.9f ? Vector3.UnitX : Vector3.UnitZ));
            var depth = Vector3.Cross(across, axis);
            var start = _positions.Count;
            for (var s = 0; s <= segments; s++)
            {
                var (sin, cos) = MathF.SinCos(s * MathF.Tau / segments);
                var ring = centre + (across * (cos * radius.X)) + (depth * (sin * radius.Y));
                var outward = Vector3.Normalize((across * cos) + (depth * sin));
                for (var t = 0; t <= sides; t++)
                {
                    var (tsin, tcos) = MathF.SinCos(t * MathF.Tau / sides);
                    var normal = (outward * tcos) + (axis * tsin);
                    Add(ring + (normal * tube), normal, new Vector2(s / (float)segments, t / (float)sides), part, side);
                }
            }

            for (var s = 0; s < segments; s++)
            {
                for (var t = 0; t < sides; t++)
                {
                    var a = start + (s * (sides + 1)) + t;
                    var c = a + sides + 1;
                    Quad(a, c, c + 1, a + 1);
                }
            }
        }

        /// <summary>A sheet between two rows of points: a wing's backing.</summary>
        public void Sheet(IReadOnlyList<Vector3> inner, IReadOnlyList<Vector3> outer, Vector3 normal, AngelPart part, float side)
        {
            var start = _positions.Count;
            for (var i = 0; i < inner.Count; i++)
            {
                Add(inner[i], normal, new Vector2(0f, 0f), part, side);
                Add(outer[i], normal, new Vector2(0.8f, 0f), part, side);
            }

            for (var i = 0; i < inner.Count - 1; i++)
            {
                var a = start + (i * 2);
                Quad(a, a + 1, a + 3, a + 2);
            }
        }

        /// <summary>A feather: a pointed blade along a direction, folded a little along its rib, its length and breadth in UV.</summary>
        public void Feather(Vector3 root, Vector3 along, float length, float width, Vector3 facing, float side)
        {
            var across = Vector3.Normalize(Vector3.Cross(facing, along));
            float[] at = [0f, 0.25f, 0.6f, 0.86f, 1f];
            float[] breadth = [0.3f, 0.95f, 1f, 0.8f, 0.25f];
            var start = _positions.Count;
            for (var i = 0; i < at.Length; i++)
            {
                var spine = root + (along * (length * at[i]));
                var half = width * 0.5f * breadth[i];
                var fold = facing * (half * 0.25f);
                Add(spine + (across * half) - fold, Vector3.Normalize(facing + (across * 0.25f)), new Vector2(at[i], 1f), AngelPart.Wing, side);
                Add(spine + fold, facing, new Vector2(at[i], 0f), AngelPart.Wing, side);
                Add(spine - (across * half) - fold, Vector3.Normalize(facing - (across * 0.25f)), new Vector2(at[i], -1f), AngelPart.Wing, side);
            }

            for (var i = 0; i < at.Length - 1; i++)
            {
                var a = start + (i * 3);
                Quad(a, a + 1, a + 4, a + 3);
                Quad(a + 1, a + 2, a + 5, a + 4);
            }
        }

        /// <summary>A thin three-sided thorn.</summary>
        public void Spike(Vector3 root, Vector3 up, float height, float width, AngelPart part, float place)
        {
            var across = Vector3.Normalize(Vector3.Cross(up, MathF.Abs(up.X) > 0.9f ? Vector3.UnitZ : Vector3.UnitX));
            var other = Vector3.Cross(up, across);
            var start = _positions.Count;
            for (var k = 0; k < 3; k++)
            {
                var (sin, cos) = MathF.SinCos(k * MathF.Tau / 3f);
                var outward = (across * cos) + (other * sin);
                Add(root + (outward * width), outward, new Vector2(place, 0f), part, 0f);
            }

            Add(root + (up * height), up, new Vector2(place, 1f), part, 0f);
            for (var k = 0; k < 3; k++)
            {
                Triangle(start + k, start + ((k + 1) % 3), start + 3);
            }
        }

        /// <summary>A blade: a flat, double-edged diamond along a direction, with a crossguard.</summary>
        public void Blade(Vector3 grip, Vector3 along, float length, float width)
        {
            var across = Vector3.Normalize(Vector3.Cross(along, Vector3.UnitZ));
            var flat = Vector3.Cross(across, along);
            var start = _positions.Count;
            Add(grip, -along, new Vector2(0f, 0f), AngelPart.Blade, 1f);
            foreach (var (edge, normal) in new[] { (across, across), (flat * 0.25f, flat), (-across, -across), (flat * -0.25f, -flat) })
            {
                Add(grip + (along * (length * 0.18f)) + (edge * (width * 0.5f)), normal, new Vector2(0.18f, 1f), AngelPart.Blade, 1f);
            }

            Add(grip + (along * length), along, new Vector2(1f, 0f), AngelPart.Blade, 1f);
            for (var k = 0; k < 4; k++)
            {
                var (a, c) = (start + 1 + k, start + 1 + ((k + 1) % 4));
                Triangle(start, a, c);
                Triangle(a, start + 5, c);
            }

            Tube([grip + (along * 0.12f) - (across * 0.09f), grip + (along * 0.12f) + (across * 0.09f)], [0.012f, 0.012f], 5, AngelPart.Blade, 1f);
        }

        /// <summary>A veil hung over the lower face, curving about it.</summary>
        public void Veil(Vector3 top, float half, float drop)
        {
            const int Across = 8;
            const int Down = 5;
            var start = _positions.Count;
            for (var d = 0; d <= Down; d++)
            {
                for (var a = 0; a <= Across; a++)
                {
                    var x = -half + (2f * half * a / Across);
                    var y = drop * d / Down;
                    var bend = (x * x) / (half * half);
                    Add(top + new Vector3(x, -y, (-0.07f * bend) + (0.02f * d / Down)), Vector3.Normalize(new Vector3(x * 2.5f, 0f, 1f)), new Vector2(a / (float)Across, d / (float)Down), AngelPart.Veil, 0f);
                }
            }

            for (var d = 0; d < Down; d++)
            {
                for (var a = 0; a < Across; a++)
                {
                    var i = start + (d * (Across + 1)) + a;
                    Quad(i, i + 1, i + Across + 2, i + Across + 1);
                }
            }
        }

        /// <summary>The finished figure.</summary>
        public AngelMesh Finish() => new([.. _positions], [.. _normals], [.. _uv], [.. _detail], [.. _indices]);

        private void Add(Vector3 position, Vector3 normal, Vector2 uv, AngelPart part, float side)
        {
            _positions.Add(position);
            _normals.Add(normal);
            _uv.Add(uv);
            _detail.Add(new Vector2((float)part, side));
        }

        private void Quad(int a, int b, int c, int d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        private void Triangle(int a, int b, int c)
        {
            _indices.Add(a);
            _indices.Add(b);
            _indices.Add(c);
        }
    }
}
