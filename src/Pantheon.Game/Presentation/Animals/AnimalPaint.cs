using System;
using System.Numerics;

namespace Pantheon.Game.Presentation.Animals;

/// <summary>A species' colours (ADR-061), sRGB 0..1, from its look in <c>divine_view.json</c>.</summary>
/// <param name="Fur">Hide or fur.</param>
/// <param name="Belly">Belly, chest and the light of the face.</param>
/// <param name="Muzzle">Muzzle or face skin.</param>
/// <param name="Nose">Nose.</param>
/// <param name="InnerEar">The inside of the ears.</param>
/// <param name="Horn">Horn, beak and claws.</param>
/// <param name="Hoof">Hooves and soles.</param>
/// <param name="Mane">Mane, tufts and a horse's hair.</param>
/// <param name="Shell">A tortoise's shell.</param>
/// <param name="Marking">Stripes and patches.</param>
public sealed record AnimalPalette(Vector3 Fur, Vector3 Belly, Vector3 Muzzle, Vector3 Nose, Vector3 InnerEar, Vector3 Horn, Vector3 Hoof, Vector3 Mane, Vector3 Shell, Vector3 Marking);

/// <summary>
/// Paints a Creature's surface (ADR-061): each part's colour, the fur's pattern (a tiger's stripes, a
/// cow's patches, a tortoise's plates) and a soft mottle that keeps a hide from looking flat. The
/// pattern follows the point in its part's own space, so it is the same every time.
/// </summary>
public static class AnimalPaint
{
    /// <summary>The colour at a point of a part.</summary>
    /// <param name="tag">The part's paint.</param>
    /// <param name="p">The point, in the part's space.</param>
    /// <param name="palette">The colours.</param>
    /// <param name="pattern">The fur's pattern.</param>
    /// <param name="salt">A number that sets this part's patches apart from another's.</param>
    /// <returns>The colour.</returns>
    public static Vector3 Color(PaintTag tag, Vector3 p, AnimalPalette palette, FurPattern pattern, int salt)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var mottle = 0.9f + (0.2f * Noise((p * 3.1f) + new Vector3(salt * 7.3f)));
        var color = tag switch
        {
            PaintTag.Fur => Fur(p, palette, pattern, salt),
            PaintTag.Belly => palette.Belly,
            PaintTag.Muzzle => palette.Muzzle,
            PaintTag.Nose => palette.Nose,
            PaintTag.InnerEar => palette.InnerEar,
            PaintTag.Horn => Vector3.Lerp(palette.Horn, palette.Horn * 0.7f, Noise(p * 6f)),
            PaintTag.Hoof => palette.Hoof,
            PaintTag.Mane => palette.Mane * (0.85f + (0.3f * Noise(new Vector3(p.X * 9f, p.Y * 2f, p.Z * 9f)))),
            PaintTag.Shell => Shell(p, palette),
        };
        return Vector3.Clamp(color * mottle, Vector3.Zero, Vector3.One);
    }

    /// <summary>Fur, with its pattern.</summary>
    private static Vector3 Fur(Vector3 p, AnimalPalette palette, FurPattern pattern, int salt) => pattern switch
    {
        FurPattern.None => palette.Fur,
        FurPattern.Stripes => MathF.Abs(MathF.Sin(((p.Y * 3.3f) + (0.45f * MathF.Sin((p.X * 2.3f) + (p.Z * 1.9f))) + salt) * MathF.PI)) > 0.8f
            ? palette.Marking
            : palette.Fur,
        FurPattern.Patches => Noise((p * 0.95f) + new Vector3(salt * 3.7f, 1.3f, salt * 1.1f)) > 0.58f ? palette.Marking : palette.Fur,
        FurPattern.Plates => Vector3.Lerp(palette.Fur, palette.Marking, Noise(p * 4f) * 0.5f),
    };

    /// <summary>A shell of plates with dark seams between them and a lighter middle to each.</summary>
    private static Vector3 Shell(Vector3 p, AnimalPalette palette)
    {
        var u = (p.X * 1.6f) + (MathF.Floor(p.Y * 1.6f) * 0.5f);
        var v = p.Y * 1.6f;
        var edge = MathF.Min(MathF.Min(Frac(u), 1f - Frac(u)), MathF.Min(Frac(v), 1f - Frac(v)));
        var seam = edge < 0.07f;
        var middle = 1f - (MathF.Min(edge, 0.5f) * 0.6f);
        return seam ? palette.Shell * 0.45f : Vector3.Lerp(palette.Shell * 1.15f, palette.Shell, middle);
    }

    /// <summary>Smooth value noise in 0..1.</summary>
    /// <param name="p">The point.</param>
    /// <returns>The noise.</returns>
    public static float Noise(Vector3 p)
    {
        var i = new Vector3(MathF.Floor(p.X), MathF.Floor(p.Y), MathF.Floor(p.Z));
        var f = p - i;
        var u = f * f * (new Vector3(3f) - (2f * f));
        var (x, y, z) = ((int)i.X, (int)i.Y, (int)i.Z);
        float Lerp(float a, float b, float t) => a + ((b - a) * t);
        var x00 = Lerp(Hash(x, y, z), Hash(x + 1, y, z), u.X);
        var x10 = Lerp(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), u.X);
        var x01 = Lerp(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), u.X);
        var x11 = Lerp(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), u.X);
        return Lerp(Lerp(x00, x10, u.Y), Lerp(x01, x11, u.Y), u.Z);
    }

    /// <summary>A lattice point's value in 0..1.</summary>
    private static float Hash(int x, int y, int z)
    {
        unchecked
        {
            var h = (uint)((x * 73856093) ^ (y * 19349663) ^ (z * 83492791));
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    private static float Frac(float v) => v - MathF.Floor(v);
}
