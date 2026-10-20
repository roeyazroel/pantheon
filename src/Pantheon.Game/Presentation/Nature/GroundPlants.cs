using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation.Nature;

/// <summary>
/// Grows the ground cover (spec §61, ADR-053, ADR-073): tufts of curved blades darker at the foot,
/// flowers on stems, bushes of leaf cards, ferns of arching fronds, stones of broken shapes mossy on
/// top, and reeds with their seed heads. Each kind has a few variants. Pure.
/// </summary>
public static class GroundPlants
{
    private static readonly Vector3 Up = Vector3.UnitY;

    /// <summary>How many variants a kind has.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Its variants.</returns>
    public static int Variants(GroundCoverKind kind) => kind switch
    {
        GroundCoverKind.Rock => 3,
        GroundCoverKind.Grass or GroundCoverKind.Flowers or GroundCoverKind.Bush or GroundCoverKind.Reeds or GroundCoverKind.Fern => 2,
    };

    /// <summary>Grows a thing on the ground, standing at the origin.</summary>
    /// <param name="kind">What.</param>
    /// <param name="variant">Which of its variants, from 0.</param>
    /// <returns>Its mesh.</returns>
    public static PlantMesh Grow(GroundCoverKind kind, int variant)
    {
        var random = PlantRandom.For(0x6C0 + (ulong)kind, (ulong)Math.Clamp(variant, 0, Variants(kind) - 1) + 1);
        var b = new PlantBuilder();
        switch (kind)
        {
            case GroundCoverKind.Grass:
                Tuft(b, ref random, blades: 34, spread: 0.4f, (0.32f, 0.72f), 0.05f);
                break;
            case GroundCoverKind.Flowers:
                Tuft(b, ref random, blades: 8, spread: 0.12f, (0.2f, 0.4f), 0.035f);
                for (var f = 0; f < 3; f++)
                {
                    var angle = random.Range(0f, MathF.Tau);
                    var foot = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * random.Range(0.02f, 0.12f);
                    var head = foot + new Vector3(random.Range(-0.08f, 0.08f), random.Range(0.32f, 0.5f), random.Range(-0.08f, 0.08f));
                    b.Limb([foot - (Up * 0.05f), head], [0.008f, 0.006f], 3, 0.7f);
                    Blossom(b, ref random, head, random.Range(0.045f, 0.07f));
                }

                break;
            case GroundCoverKind.Bush:
                for (var s = 0; s < 3; s++)
                {
                    var lean = new Vector3(random.Range(-0.25f, 0.25f), 0.5f, random.Range(-0.25f, 0.25f));
                    b.Limb([Vector3.Zero - (Up * 0.05f), lean], [0.03f, 0.015f], 3, 0.5f);
                }

                var centre = new Vector3(0f, 0.42f, 0f);
                for (var c = 0; c < 9; c++)
                {
                    var angle = random.Range(0f, MathF.Tau);
                    var rise = random.Range(-0.1f, 0.9f);
                    var at = centre + (new Vector3(MathF.Cos(angle) * 0.42f, rise * 0.32f, MathF.Sin(angle) * 0.42f) * random.Range(0.5f, 1f));
                    var right = new Vector3(MathF.Cos(angle + 1.57f), 0f, MathF.Sin(angle + 1.57f));
                    var tilt = random.Range(0.2f, 1.2f);
                    var up = Vector3.Normalize((Up * MathF.Cos(tilt)) + (new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * MathF.Sin(tilt)));
                    b.Card(at, right, up, random.Range(0.55f, 0.75f), random.Range(0.55f, 0.75f), LeafCell.Shrub, centre - (Up * 0.2f), Math.Clamp(0.55f + (0.45f * rise), 0.4f, 1f));
                }

                break;
            case GroundCoverKind.Fern:
                var fronds = 8;
                for (var f = 0; f < fronds; f++)
                {
                    var angle = (MathF.Tau * f / fronds) + random.Range(-0.25f, 0.25f);
                    var outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    var length = random.Range(0.75f, 1.1f);
                    var rise = random.Range(0.9f, 1.4f);
                    var spine = new List<Vector3>();
                    var widths = new List<float>();
                    for (var s = 0; s <= 4; s++)
                    {
                        var t = s / 4f;
                        spine.Add((outward * (length * t)) + (Up * ((length * rise * t) - (length * 1.25f * t * t))));
                        widths.Add(0.34f * MathF.Max(0.15f, MathF.Sin(MathF.PI * MathF.Min(1f, (t * 0.9f) + 0.1f))));
                    }

                    b.Strip(spine, widths, new Vector3(-outward.Z, 0f, outward.X), LeafCell.Frond, -Up * 0.3f, 0.75f);
                }

                break;
            case GroundCoverKind.Rock:
                b.Stone(random.Range(0.32f, 0.5f), random.Range(0.5f, 0.8f), ref random);
                break;
            case GroundCoverKind.Reeds:
                Tuft(b, ref random, blades: 12, spread: 0.16f, (0.9f, 1.45f), 0.03f);
                for (var h = 0; h < 3; h++)
                {
                    var foot = new Vector3(random.Range(-0.1f, 0.1f), -0.05f, random.Range(-0.1f, 0.1f));
                    var top = foot + new Vector3(random.Range(-0.1f, 0.1f), random.Range(1.2f, 1.6f), random.Range(-0.1f, 0.1f));
                    b.Limb([foot, top], [0.01f, 0.008f], 3, 0.7f);
                    b.Limb([top - (Up * 0.22f), top - (Up * 0.02f)], [0.03f, 0.028f], 5, 0.8f);
                }

                break;
        }

        return b.Finish();
    }

    /// <summary>A tuft of curved blades, leaning out from its middle, each darker at its foot.</summary>
    private static void Tuft(PlantBuilder b, ref PlantRandom random, int blades, float spread, (float From, float To) height, float width)
    {
        for (var i = 0; i < blades; i++)
        {
            var angle = random.Range(0f, MathF.Tau);
            var outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            var foot = (outward * (spread * MathF.Sqrt(random.Next()))) - (Up * 0.04f);
            var tall = random.Range(height.From, height.To);
            var lean = random.Range(0.15f, 0.65f);
            var side = new Vector3(-outward.Z, 0f, outward.X) * random.Range(0.8f, 1.2f);
            var points = new Vector3[4];
            for (var s = 0; s < 4; s++)
            {
                // The blade bends over more the higher it grows.
                var t = s / 3f;
                points[s] = foot + (Up * (tall * t)) + (outward * (tall * lean * t * t));
            }

            var wide = width * random.Range(0.8f, 1.2f);
            var normal = Vector3.Normalize(Up + (outward * 0.35f));
            for (var s = 0; s < 3; s++)
            {
                var (t0, t1) = (s / 3f, (s + 1) / 3f);
                var (w0, w1) = (wide * (1f - t0), wide * (1f - t1));
                var (a0, a1) = (points[s] - (side * w0), points[s] + (side * w0));
                var (b0, b1) = (points[s + 1] - (side * w1), points[s + 1] + (side * w1));
                b.Triangle(a0, a1, b1, normal, PlantPart.Blade, (t0, t0, t1));
                if (s < 2)
                {
                    b.Triangle(a0, b1, b0, normal, PlantPart.Blade, (t0, t1, t1));
                }
            }
        }
    }

    /// <summary>A flower's head: five petals about its middle.</summary>
    private static void Blossom(PlantBuilder b, ref PlantRandom random, Vector3 head, float size)
    {
        var turn = random.Range(0f, MathF.Tau);
        var face = Vector3.Normalize(Up + new Vector3(random.Range(-0.3f, 0.3f), 0f, random.Range(-0.3f, 0.3f)));
        var across = Vector3.Normalize(Vector3.Cross(face, Vector3.UnitX));
        var other = Vector3.Cross(face, across);
        for (var p = 0; p < 5; p++)
        {
            var a = turn + (MathF.Tau * p / 5f);
            Vector3 At(float angle, float reach) => head + (((across * MathF.Cos(angle)) + (other * MathF.Sin(angle))) * reach);
            b.Triangle(head, At(a - 0.45f, size), At(a + 0.45f, size), face, PlantPart.Petal, (1f, 1f, 1f));
            b.Triangle(At(a - 0.45f, size), At(a, size * 1.2f), At(a + 0.45f, size), face, PlantPart.Petal, (1f, 1f, 1f));
        }
    }
}
