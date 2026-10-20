using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation.Animals;

/// <summary>
/// The body parts every upright animal shares (ADR-061), shaped by its proportions: a torso of pelvis,
/// belly, chest, shoulders and neck; limbs with their muscles; and paws, hooves, hands or stumps.
/// </summary>
internal static class AnimalParts
{
    /// <summary>An ellipsoid.</summary>
    public static SdfEllipsoid E(float x, float y, float z, float rx, float ry, float rz, PaintTag tag = PaintTag.Fur, float blend = 0.25f, float pitch = 0f, float roll = 0f) =>
        new() { Centre = new Vector3(x, y, z), Radii = new Vector3(rx, ry, rz), Tag = tag, Blend = blend, Turn = Quaternion.CreateFromYawPitchRoll(0f, pitch, roll) };

    /// <summary>A round cone between two points.</summary>
    public static SdfRoundCone C(Vector3 a, Vector3 b, float ra, float rb, PaintTag tag = PaintTag.Fur, float blend = 0.2f) =>
        new() { A = a, B = b, RadiusA = ra, RadiusB = rb, Tag = tag, Blend = blend };

    /// <summary>A point.</summary>
    public static Vector3 P(float x, float y, float z) => new(x, y, z);

    /// <summary>A pair of mirrored parts, right first.</summary>
    public static IEnumerable<SdfPart> Pair(Func<float, SdfPart> side)
    {
        yield return side(1f);
        yield return side(-1f);
    }

    /// <summary>The torso: pelvis, belly, chest, its lighter front, the shoulders and the neck.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="chest">The chest's half-width.</param>
    /// <param name="belly">The belly's half-width.</param>
    /// <param name="depth">How deep the body is, front to back.</param>
    /// <param name="front">The paint of the chest's and belly's front.</param>
    /// <returns>The parts.</returns>
    public static List<SdfPart> Torso(AnimalProportions b, float chest, float belly, float depth, PaintTag front = PaintTag.Belly)
    {
        var t = b.Torso;
        var neckRadius = b.ArmGirth * 1.15f;
        var parts = new List<SdfPart>
        {
            E(0f, 0.15f, -0.05f, b.HipWidth + (b.LegGirth * 0.9f), 0.62f, depth * 0.78f),
            E(0f, t * 0.42f, 0.08f, belly, t * 0.33f, depth * 0.95f, PaintTag.Fur, 0.55f),
            E(0f, t * 0.76f, 0f, chest, t * 0.3f, depth * 0.88f, PaintTag.Fur, 0.55f),
            E(0f, t * 0.6f, depth * 0.48f, Math.Min(chest, belly) * 0.68f, t * 0.36f, depth * 0.5f, front, 0.35f),
            C(P(0f, t * 0.86f, -0.05f), P(0f, t + (b.Neck * 0.55f), b.NeckForward * 0.6f), neckRadius, neckRadius * 0.85f, PaintTag.Fur, 0.4f),
        };
        parts.AddRange(Pair(s => E(s * b.ShoulderWidth * 0.92f, t * 0.95f, -0.02f, b.ArmGirth * 1.25f, b.ArmGirth * 1.15f, b.ArmGirth * 1.2f, PaintTag.Fur, 0.4f)));
        return parts;
    }

    /// <summary>A thigh with its muscle.</summary>
    public static List<SdfPart> Thigh(AnimalProportions b) =>
    [
        C(P(0f, 0f, 0f), P(0f, -b.Thigh, 0f), b.LegGirth, b.LegGirth * 0.72f),
        E(0f, -b.Thigh * 0.38f, 0.06f, b.LegGirth * 1.08f, b.Thigh * 0.42f, b.LegGirth * 1.12f, PaintTag.Fur, 0.3f),
    ];

    /// <summary>A shin with its calf.</summary>
    public static List<SdfPart> Shin(AnimalProportions b) =>
    [
        C(P(0f, 0f, 0f), P(0f, -b.Shin, 0f), b.LegGirth * 0.7f, b.LegGirth * 0.46f),
        E(0f, -b.Shin * 0.3f, -0.08f, b.LegGirth * 0.72f, b.Shin * 0.3f, b.LegGirth * 0.78f, PaintTag.Fur, 0.25f),
    ];

    /// <summary>An upper arm with its biceps.</summary>
    public static List<SdfPart> UpperArm(AnimalProportions b) =>
    [
        C(P(0f, 0f, 0f), P(0f, -b.UpperArm, 0f), b.ArmGirth, b.ArmGirth * 0.76f),
        E(0f, -b.UpperArm * 0.4f, 0.04f, b.ArmGirth * 1.05f, b.UpperArm * 0.36f, b.ArmGirth * 1.1f, PaintTag.Fur, 0.25f),
    ];

    /// <summary>A forearm.</summary>
    public static List<SdfPart> Forearm(AnimalProportions b) =>
    [
        C(P(0f, 0f, 0f), P(0f, -b.Forearm, 0f), b.ArmGirth * 0.8f, b.ArmGirth * 0.58f),
        E(0f, -b.Forearm * 0.28f, 0f, b.ArmGirth * 0.85f, b.Forearm * 0.3f, b.ArmGirth * 0.85f, PaintTag.Fur, 0.2f),
    ];

    /// <summary>A foot below the ankle, toes forward, its sole at the ankle's height below.</summary>
    public static List<SdfPart> Foot(AnimalProportions b, Extremity kind)
    {
        var a = b.Ankle;
        var g = b.LegGirth;
        return kind switch
        {
            Extremity.Hoof =>
            [
                C(P(0f, 0.1f, 0f), P(0f, -a * 0.55f, 0.06f), g * 0.5f, g * 0.6f),
                new SdfRoundBox { Centre = P(0f, -a * 0.78f, 0.1f), Half = new Vector3(g * 0.5f, a * 0.2f, g * 0.55f), Rounding = 0.07f, Tag = PaintTag.Hoof, Blend = 0.06f },
            ],
            Extremity.Stump =>
            [
                new SdfRoundBox { Centre = P(0f, (0.15f - a) * 0.5f, 0.06f), Half = new Vector3(g * 0.55f, ((0.15f + a) * 0.5f) - 0.1f, g * 0.62f), Rounding = 0.1f, Tag = PaintTag.Fur },
                .. Toes(g * 0.7f, -a * 0.82f, g * 0.75f, 3, 0.11f, PaintTag.Horn),
            ],
            Extremity.Hand =>
            [
                E(0f, -a * 0.55f, g * 0.55f, g * 0.62f, a * 0.5f, g * 1.15f),
                .. Toes(g * 0.95f, -a * 0.8f, g * 1.55f, 4, g * 0.2f, PaintTag.Fur),
            ],
            Extremity.Paw =>
            [
                E(0f, -a * 0.55f, g * 0.45f, g * 0.66f, a * 0.52f, g * 1.0f),
                .. Toes(g * 0.85f, -a * 0.78f, g * 1.3f, 4, g * 0.22f, PaintTag.Fur),
                .. Toes(g * 0.85f, -a * 0.86f, g * 1.55f, 4, 0.05f, PaintTag.Horn),
            ],
        };
    }

    /// <summary>A hand below the wrist: fingers and a thumb, a paw with claws, or a hoof.</summary>
    public static List<SdfPart> Hand(AnimalProportions b, Extremity kind)
    {
        var g = b.ArmGirth;
        return kind switch
        {
            Extremity.Hoof =>
            [
                E(0f, -g * 0.7f, 0.02f, g * 0.72f, g * 0.85f, g * 0.6f),
                new SdfRoundBox { Centre = P(0f, -g * 1.45f, 0.03f), Half = new Vector3(g * 0.6f, g * 0.28f, g * 0.55f), Rounding = 0.06f, Tag = PaintTag.Hoof, Blend = 0.08f },
            ],
            Extremity.Stump =>
            [
                E(0f, -g * 0.65f, 0f, g * 0.75f, g * 0.8f, g * 0.7f),
                .. Toes(g * 0.75f, -g * 1.3f, g * 0.35f, 3, 0.09f, PaintTag.Horn),
            ],
            Extremity.Hand =>
            [
                E(0f, -g * 0.75f, 0.04f, g * 0.78f, g * 0.85f, g * 0.42f),
                .. Fingers(g, PaintTag.Fur),
                C(P(g * 0.65f, -g * 0.55f, 0.12f), P(g * 0.95f, -g * 1.15f, 0.35f), g * 0.24f, g * 0.18f, PaintTag.Fur, 0.12f),
            ],
            Extremity.Paw =>
            [
                E(0f, -g * 0.8f, 0.06f, g * 0.82f, g * 0.9f, g * 0.6f),
                .. Fingers(g * 0.85f, PaintTag.Fur),
                .. Toes(g * 0.62f, -g * 1.75f, g * 0.45f, 4, 0.05f, PaintTag.Horn),
            ],
        };
    }

    /// <summary>One tail segment, reaching back and a little down from its root.</summary>
    public static List<SdfPart> TailSegment(AnimalProportions b, float thinning = 0.82f) =>
        b.TailLength <= 0f ? [] : [C(P(0f, 0f, 0f), P(0f, 0f, -b.TailLength), b.TailGirth, b.TailGirth * thinning)];

    /// <summary>A row of toes or claws across a foot's front.</summary>
    private static IEnumerable<SdfPart> Toes(float width, float y, float z, int count, float radius, PaintTag tag)
    {
        for (var i = 0; i < count; i++)
        {
            var x = count == 1 ? 0f : (-width * 0.5f) + (width * i / (count - 1));
            yield return E(x, y, z, radius, radius * 0.85f, radius * 1.3f, tag, tag == PaintTag.Horn ? 0.02f : 0.1f);
        }
    }

    /// <summary>Four fingers hanging from a palm.</summary>
    private static IEnumerable<SdfPart> Fingers(float g, PaintTag tag)
    {
        for (var i = 0; i < 4; i++)
        {
            var x = (-g * 0.55f) + (g * 1.1f * i / 3f);
            yield return C(P(x, -g * 1.25f, 0.06f), P(x * 1.05f, -g * 1.95f, 0.16f), g * 0.21f, g * 0.17f, tag, 0.1f);
        }
    }
}
