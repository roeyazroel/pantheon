using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation.Animals;

/// <summary>
/// One shape of a body part as a signed distance (ADR-061): negative inside, positive outside.
/// Parts blend smoothly into each other by <see cref="Blend"/>, so a head, its snout and its ears
/// become one organic surface. <see cref="Tag"/> names the part's paint (fur, belly, horn...).
/// </summary>
public abstract record SdfPart
{
    /// <summary>How far, in units, this part melts into the shape before it (0 is a hard union).</summary>
    public float Blend { get; init; } = 0.15f;

    /// <summary>The paint of the surface nearest this part.</summary>
    public required PaintTag Tag { get; init; }

    /// <summary>Whether the part is carved out of the shape rather than added to it.</summary>
    public bool Carve { get; init; }

    /// <summary>The signed distance from a point.</summary>
    /// <param name="p">The point.</param>
    /// <returns>Negative inside.</returns>
    public abstract float Distance(Vector3 p);

    /// <summary>A box that holds the part.</summary>
    /// <returns>Its corners.</returns>
    public abstract (Vector3 Min, Vector3 Max) Bounds();
}

/// <summary>An ellipsoid, optionally turned.</summary>
public sealed record SdfEllipsoid : SdfPart
{
    /// <summary>Its centre.</summary>
    public required Vector3 Centre { get; init; }

    /// <summary>Its half-axes.</summary>
    public required Vector3 Radii { get; init; }

    /// <summary>Its turn, applied to its axes.</summary>
    public Quaternion Turn { get; init; } = Quaternion.Identity;

    /// <inheritdoc />
    public override float Distance(Vector3 p)
    {
        // Inigo Quilez's bound for an ellipsoid: exact on the surface, close nearby.
        var q = Vector3.Transform(p - Centre, Quaternion.Inverse(Turn));
        var k0 = (q / Radii).Length();
        var k1 = (q / (Radii * Radii)).Length();
        return k1 < 1e-6f ? -MathF.Min(Radii.X, MathF.Min(Radii.Y, Radii.Z)) : k0 * (k0 - 1f) / k1;
    }

    /// <inheritdoc />
    public override (Vector3 Min, Vector3 Max) Bounds()
    {
        var r = MathF.Max(Radii.X, MathF.Max(Radii.Y, Radii.Z));
        return (Centre - new Vector3(r), Centre + new Vector3(r));
    }
}

/// <summary>A cone with round ends from one point to another: a limb, a tail, a horn, a snout.</summary>
public sealed record SdfRoundCone : SdfPart
{
    /// <summary>Its first end.</summary>
    public required Vector3 A { get; init; }

    /// <summary>Its second end.</summary>
    public required Vector3 B { get; init; }

    /// <summary>Its radius at <see cref="A"/>.</summary>
    public required float RadiusA { get; init; }

    /// <summary>Its radius at <see cref="B"/>.</summary>
    public required float RadiusB { get; init; }

    /// <inheritdoc />
    public override float Distance(Vector3 p)
    {
        // Inigo Quilez's exact round cone.
        var ba = B - A;
        var l2 = Vector3.Dot(ba, ba);
        if (l2 < 1e-8f)
        {
            return (p - A).Length() - MathF.Max(RadiusA, RadiusB);
        }

        var rr = RadiusA - RadiusB;
        var a2 = l2 - (rr * rr);
        var il2 = 1f / l2;
        var pa = p - A;
        var y = Vector3.Dot(pa, ba);
        var z = y - l2;
        var xv = (pa * l2) - (ba * y);
        var x2 = Vector3.Dot(xv, xv);
        var y2 = y * y * l2;
        var z2 = z * z * l2;
        var k = MathF.Sign(rr) * rr * rr * x2;
        if (MathF.Sign(z) * a2 * z2 > k)
        {
            return (MathF.Sqrt(x2 + z2) * il2) - RadiusB;
        }

        if (MathF.Sign(y) * a2 * y2 < k)
        {
            return (MathF.Sqrt(x2 + y2) * il2) - RadiusA;
        }

        return (((MathF.Sqrt(x2 * a2 * il2) + (y * rr)) * il2) - RadiusA);
    }

    /// <inheritdoc />
    public override (Vector3 Min, Vector3 Max) Bounds()
    {
        var r = MathF.Max(RadiusA, RadiusB);
        return (Vector3.Min(A, B) - new Vector3(r), Vector3.Max(A, B) + new Vector3(r));
    }
}

/// <summary>A box with rounded edges, optionally turned: a hoof, a plate of shell.</summary>
public sealed record SdfRoundBox : SdfPart
{
    /// <summary>Its centre.</summary>
    public required Vector3 Centre { get; init; }

    /// <summary>Its half-extents, before rounding.</summary>
    public required Vector3 Half { get; init; }

    /// <summary>The rounding of its edges.</summary>
    public float Rounding { get; init; }

    /// <summary>Its turn.</summary>
    public Quaternion Turn { get; init; } = Quaternion.Identity;

    /// <inheritdoc />
    public override float Distance(Vector3 p)
    {
        var q = Vector3.Abs(Vector3.Transform(p - Centre, Quaternion.Inverse(Turn))) - Half;
        return Vector3.Max(q, Vector3.Zero).Length() + MathF.Min(MathF.Max(q.X, MathF.Max(q.Y, q.Z)), 0f) - Rounding;
    }

    /// <inheritdoc />
    public override (Vector3 Min, Vector3 Max) Bounds()
    {
        var r = Half.Length() + Rounding;
        return (Centre - new Vector3(r), Centre + new Vector3(r));
    }
}

/// <summary>Blends parts into one shape and tells which part's paint lies nearest a point.</summary>
public sealed class SdfShape
{
    private readonly IReadOnlyList<SdfPart> _parts;

    /// <summary>Creates a shape from its parts, in order: each blends into those before it.</summary>
    /// <param name="parts">The parts; the first may not be carved.</param>
    public SdfShape(IReadOnlyList<SdfPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0 || parts[0].Carve)
        {
            throw new ArgumentException("A shape needs a first part that adds to it.", nameof(parts));
        }

        _parts = parts;
    }

    /// <summary>The parts.</summary>
    public IReadOnlyList<SdfPart> Parts => _parts;

    /// <summary>The shape's signed distance from a point.</summary>
    /// <param name="p">The point.</param>
    /// <returns>Negative inside.</returns>
    public float Distance(Vector3 p)
    {
        var d = _parts[0].Distance(p);
        for (var i = 1; i < _parts.Count; i++)
        {
            var part = _parts[i];
            d = part.Carve ? SmoothMax(d, -part.Distance(p), part.Blend) : SmoothMin(d, part.Distance(p), part.Blend);
        }

        return d;
    }

    /// <summary>The paint at a point on the surface: that of the nearest adding part.</summary>
    /// <param name="p">The point.</param>
    /// <returns>The paint.</returns>
    public PaintTag TagAt(Vector3 p)
    {
        var best = float.MaxValue;
        var tag = _parts[0].Tag;
        foreach (var part in _parts)
        {
            if (part.Carve)
            {
                continue;
            }

            var d = part.Distance(p);
            if (d < best)
            {
                (best, tag) = (d, part.Tag);
            }
        }

        return tag;
    }

    /// <summary>A box that holds the shape.</summary>
    /// <returns>Its corners.</returns>
    public (Vector3 Min, Vector3 Max) Bounds()
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var part in _parts)
        {
            if (part.Carve)
            {
                continue;
            }

            var (lo, hi) = part.Bounds();
            (min, max) = (Vector3.Min(min, lo), Vector3.Max(max, hi));
        }

        return (min, max);
    }

    /// <summary>A smooth union: within <paramref name="k"/> of each other the two surfaces melt together.</summary>
    /// <param name="a">One distance.</param>
    /// <param name="b">The other.</param>
    /// <param name="k">The blend's width.</param>
    /// <returns>The blended distance.</returns>
    public static float SmoothMin(float a, float b, float k)
    {
        if (k <= 0f)
        {
            return MathF.Min(a, b);
        }

        var h = MathF.Max(k - MathF.Abs(a - b), 0f) / k;
        return MathF.Min(a, b) - (h * h * k * 0.25f);
    }

    /// <summary>A smooth intersection, for carving.</summary>
    /// <param name="a">One distance.</param>
    /// <param name="b">The other.</param>
    /// <param name="k">The blend's width.</param>
    /// <returns>The blended distance.</returns>
    public static float SmoothMax(float a, float b, float k) => -SmoothMin(-a, -b, k);
}
