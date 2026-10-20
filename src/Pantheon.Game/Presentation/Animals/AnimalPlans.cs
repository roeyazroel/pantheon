using System;
using System.Collections.Generic;
using System.Linq;
using static Pantheon.Game.Presentation.Animals.AnimalParts;

namespace Pantheon.Game.Presentation.Animals;

/// <summary>
/// The Creatures' bodies (ADR-061): eight upright animals in the manner of Black &amp; White's, and the
/// Beast. Each is a plan of proportions and shapes; the shared torso, limbs, hands and feet come from
/// <see cref="AnimalParts"/>, and each species has its own head, tail and markings.
/// </summary>
public static class AnimalPlans
{
    /// <summary>Every body, the animals in the order the new-world page offers them, then the Beast.</summary>
    public static IReadOnlyList<string> Names { get; } = ["Cow", "Ape", "Tiger", "Lion", "Wolf", "Bear", "Horse", "Tortoise", "Beast"];

    private static readonly Dictionary<string, AnimalPlan> Plans = new(StringComparer.Ordinal);

    /// <summary>A body's plan; an unknown name gets the Cow's.</summary>
    /// <param name="name">The body's name.</param>
    /// <returns>The plan.</returns>
    public static AnimalPlan For(string name)
    {
        lock (Plans)
        {
            if (!Plans.TryGetValue(name, out var plan))
            {
                plan = name switch
                {
                    "Ape" => Ape(),
                    "Tiger" => Cat(name, FurPattern.Stripes, mane: false),
                    "Lion" => Cat(name, FurPattern.None, mane: true),
                    "Wolf" => Wolf(),
                    "Bear" => Bear(name, horns: false),
                    "Horse" => Horse(),
                    "Tortoise" => Tortoise(),
                    "Beast" => Bear(name, horns: true),
                    _ => Cow(),
                };
                Plans[name] = plan;
            }

            return plan;
        }
    }

    /// <summary>A plan from its proportions, head, extremities, tail and pattern.</summary>
    private static AnimalPlan Build(string name, AnimalProportions b, List<SdfPart> torso, List<SdfPart> head, FacePlan face, (Extremity Feet, Extremity Hands) ends, FurPattern pattern, List<SdfPart>? tailTip = null, float tailThinning = 0.82f) => new()
    {
        Name = name,
        Body = b,
        Torso = torso,
        Head = head,
        Thigh = Thigh(b),
        Shin = Shin(b),
        Foot = Foot(b, ends.Feet),
        UpperArm = UpperArm(b),
        Forearm = Forearm(b),
        Hand = AnimalParts.Hand(b, ends.Hands),
        TailSegment = TailSegment(b, tailThinning),
        TailTip = tailTip ?? [],
        Face = face,
        Pattern = pattern,
    };

    /// <summary>The Cow: broad and gentle, patched, with curved horns, a pink muzzle and hooves.</summary>
    private static AnimalPlan Cow()
    {
        var b = new AnimalProportions { HipWidth = 0.62f, Thigh = 1.35f, Shin = 1.25f, Ankle = 0.42f, Torso = 2.7f, Lean = 0.12f, ShoulderWidth = 1.12f, UpperArm = 1.3f, Forearm = 1.25f, Neck = 0.75f, NeckForward = 0.25f, LegGirth = 0.46f, ArmGirth = 0.36f, TailLength = 0.75f, TailGirth = 0.1f };
        List<SdfPart> head =
        [
            E(0f, 0.05f, -0.05f, 0.78f, 0.74f, 0.82f),
            E(0f, -0.38f, 0.7f, 0.58f, 0.46f, 0.55f, PaintTag.Muzzle, 0.3f),
            E(0f, -0.3f, 1.08f, 0.42f, 0.3f, 0.12f, PaintTag.Muzzle, 0.08f),
            .. Pair(s => E(s * 0.17f, -0.22f, 1.18f, 0.07f, 0.06f, 0.05f, PaintTag.Nose, 0.02f)),
            .. Pair(s => E(s * 0.95f, 0.12f, -0.12f, 0.42f, 0.14f, 0.24f, PaintTag.Fur, 0.12f, roll: s * -0.35f)),
            .. Pair(s => E(s * 0.98f, 0.14f, -0.06f, 0.32f, 0.08f, 0.16f, PaintTag.InnerEar, 0.02f, roll: s * -0.35f)),
            .. Pair(s => C(P(s * 0.5f, 0.6f, -0.12f), P(s * 0.95f, 0.75f, -0.05f), 0.14f, 0.1f, PaintTag.Horn, 0.08f)),
            .. Pair(s => C(P(s * 0.95f, 0.75f, -0.05f), P(s * 1.1f, 1.15f, 0.12f), 0.1f, 0.03f, PaintTag.Horn, 0.05f)),
            E(0f, 0.68f, 0.05f, 0.32f, 0.16f, 0.3f, PaintTag.Mane, 0.15f),
        ];
        var face = new FacePlan { Eye = P(0.42f, 0.18f, 0.66f), EyeRadius = 0.17f, Mouth = P(0f, -0.66f, 1.08f), MouthWidth = 0.42f };
        var tip = new List<SdfPart> { E(0f, 0f, -0.2f, 0.17f, 0.17f, 0.3f, PaintTag.Mane, 0.08f) };
        return Build("Cow", b, Torso(b, 1.35f, 1.45f, 1.05f), head, face, (Extremity.Hoof, Extremity.Hoof), FurPattern.Patches, tip, 0.9f);
    }

    /// <summary>The Ape: hunched and long-armed, dark-furred, with a bare face under a heavy brow.</summary>
    private static AnimalPlan Ape()
    {
        var b = new AnimalProportions { HipWidth = 0.55f, Thigh = 1.05f, Shin = 1.0f, Ankle = 0.32f, Torso = 2.85f, Lean = 0.42f, ShoulderWidth = 1.22f, UpperArm = 1.75f, Forearm = 1.7f, Neck = 0.55f, NeckForward = 0.45f, LegGirth = 0.44f, ArmGirth = 0.42f };
        List<SdfPart> head =
        [
            E(0f, 0.12f, -0.12f, 0.78f, 0.82f, 0.8f),
            E(0f, 0.92f, -0.25f, 0.42f, 0.25f, 0.45f, PaintTag.Fur, 0.4f),
            E(0f, -0.2f, 0.42f, 0.62f, 0.62f, 0.52f, PaintTag.Muzzle, 0.3f),
            E(0f, 0.32f, 0.55f, 0.68f, 0.16f, 0.26f, PaintTag.Muzzle, 0.15f),
            E(0f, -0.42f, 0.78f, 0.45f, 0.36f, 0.32f, PaintTag.Muzzle, 0.2f),
            .. Pair(s => E(s * 0.12f, 0.02f, 0.92f, 0.1f, 0.07f, 0.06f, PaintTag.Nose, 0.04f)),
            .. Pair(s => E(s * 0.82f, 0.1f, -0.08f, 0.16f, 0.24f, 0.1f, PaintTag.InnerEar, 0.08f)),
        ];
        var face = new FacePlan { Eye = P(0.27f, 0.17f, 0.7f), EyeRadius = 0.14f, Mouth = P(0f, -0.66f, 0.98f), MouthWidth = 0.46f, MouthTilt = 0.4f };
        return Build("Ape", b, Torso(b, 1.55f, 1.3f, 1.0f, PaintTag.Muzzle), head, face, (Extremity.Hand, Extremity.Hand), FurPattern.None);
    }

    /// <summary>A great cat: the Tiger striped, the Lion with its mane; round ears, a white muzzle and a long tail.</summary>
    private static AnimalPlan Cat(string name, FurPattern pattern, bool mane)
    {
        var b = new AnimalProportions { HipWidth = 0.56f, Thigh = 1.4f, Shin = 1.35f, Ankle = 0.45f, Torso = 2.6f, Lean = 0.22f, ShoulderWidth = 1.05f, UpperArm = 1.35f, Forearm = 1.3f, Neck = 0.65f, NeckForward = 0.3f, LegGirth = 0.44f, ArmGirth = 0.38f, TailLength = 0.95f, TailGirth = mane ? 0.11f : 0.17f };
        List<SdfPart> head =
        [
            E(0f, 0.08f, -0.02f, 0.8f, 0.72f, 0.8f),
            .. Pair(s => E(s * 0.5f, -0.28f, 0.28f, 0.46f, 0.4f, 0.42f, PaintTag.Belly, 0.3f)),
            E(0f, -0.25f, 0.68f, 0.42f, 0.3f, 0.36f, PaintTag.Belly, 0.25f),
            E(0f, -0.04f, 0.98f, 0.13f, 0.09f, 0.08f, PaintTag.Nose, 0.05f),
            E(0f, -0.52f, 0.52f, 0.28f, 0.16f, 0.24f, PaintTag.Belly, 0.15f),
            .. Pair(s => E(s * 0.55f, 0.72f, -0.08f, 0.24f, 0.26f, 0.11f, PaintTag.Fur, 0.15f)),
            .. Pair(s => E(s * 0.55f, 0.72f, -0.02f, 0.15f, 0.17f, 0.06f, PaintTag.InnerEar, 0.02f)),
        ];
        if (mane)
        {
            head.Add(E(0f, -0.05f, -0.28f, 1.3f, 1.25f, 0.95f, PaintTag.Mane, 0.35f));
            head.Add(E(0f, -0.75f, -0.15f, 1.05f, 0.75f, 0.8f, PaintTag.Mane, 0.3f));
        }

        var torso = Torso(b, mane ? 1.35f : 1.22f, 1.05f, 0.95f);
        if (mane)
        {
            torso.Add(E(0f, b.Torso * 0.92f, 0.05f, 1.1f, 0.65f, 0.9f, PaintTag.Mane, 0.4f));
        }

        var face = new FacePlan { Eye = P(0.36f, 0.24f, 0.66f), EyeRadius = 0.15f, Mouth = P(0f, -0.43f, 0.82f), MouthWidth = 0.34f };
        var tip = mane ? new List<SdfPart> { E(0f, 0f, -0.25f, 0.2f, 0.2f, 0.32f, PaintTag.Mane, 0.08f) } : new List<SdfPart> { E(0f, 0f, -0.2f, 0.16f, 0.16f, 0.26f, PaintTag.Mane, 0.1f) };
        return Build(name, b, torso, head, face, (Extremity.Paw, Extremity.Paw), pattern, tip, 0.88f);
    }

    /// <summary>The Wolf: lean and grey, with a long muzzle, tall pointed ears and a bushy tail.</summary>
    private static AnimalPlan Wolf()
    {
        var b = new AnimalProportions { HipWidth = 0.5f, Thigh = 1.42f, Shin = 1.38f, Ankle = 0.44f, Torso = 2.5f, Lean = 0.26f, ShoulderWidth = 0.98f, UpperArm = 1.3f, Forearm = 1.28f, Neck = 0.7f, NeckForward = 0.35f, LegGirth = 0.4f, ArmGirth = 0.34f, TailLength = 0.8f, TailGirth = 0.3f };
        List<SdfPart> head =
        [
            E(0f, 0.12f, -0.12f, 0.7f, 0.66f, 0.74f),
            C(P(0f, -0.1f, 0.3f), P(0f, -0.28f, 1.3f), 0.42f, 0.2f, PaintTag.Belly, 0.3f),
            E(0f, -0.18f, 1.42f, 0.14f, 0.11f, 0.11f, PaintTag.Nose, 0.05f),
            .. Pair(s => E(s * 0.5f, -0.25f, 0.2f, 0.42f, 0.38f, 0.38f, PaintTag.Belly, 0.3f)),
            .. Pair(s => C(P(s * 0.38f, 0.55f, -0.18f), P(s * 0.5f, 1.25f, -0.22f), 0.26f, 0.04f, PaintTag.Fur, 0.12f)),
            .. Pair(s => C(P(s * 0.38f, 0.6f, -0.1f), P(s * 0.48f, 1.12f, -0.14f), 0.15f, 0.02f, PaintTag.InnerEar, 0.02f)),
        ];
        var face = new FacePlan { Eye = P(0.3f, 0.28f, 0.6f), EyeRadius = 0.14f, Mouth = P(0f, -0.48f, 1.05f), MouthWidth = 0.3f, MouthTilt = 0.35f };
        var tip = new List<SdfPart> { E(0f, 0f, -0.45f, 0.42f, 0.4f, 0.65f, PaintTag.Fur, 0.2f), E(0f, 0f, -1.0f, 0.2f, 0.2f, 0.25f, PaintTag.Belly, 0.25f) };
        return Build("Wolf", b, Torso(b, 1.15f, 0.92f, 0.9f), head, face, (Extremity.Paw, Extremity.Paw), FurPattern.None, tip, 1.25f);
    }

    /// <summary>The Bear, round and massive with a short snout; the Beast is a great horned one.</summary>
    private static AnimalPlan Bear(string name, bool horns)
    {
        var b = new AnimalProportions { HipWidth = 0.7f, Thigh = 1.2f, Shin = 1.1f, Ankle = 0.38f, Torso = 2.75f, Lean = 0.2f, ShoulderWidth = 1.3f, UpperArm = 1.4f, Forearm = 1.35f, Neck = 0.55f, NeckForward = 0.35f, LegGirth = 0.55f, ArmGirth = 0.47f, TailLength = 0.25f, TailGirth = 0.18f };
        List<SdfPart> head =
        [
            E(0f, 0.05f, -0.05f, 0.88f, 0.8f, 0.86f),
            E(0f, -0.28f, 0.72f, 0.46f, 0.38f, 0.48f, PaintTag.Muzzle, 0.3f),
            E(0f, -0.12f, 1.12f, 0.17f, 0.12f, 0.1f, PaintTag.Nose, 0.05f),
            .. Pair(s => E(s * 0.62f, 0.72f, -0.12f, 0.26f, 0.26f, 0.14f, PaintTag.Fur, 0.15f)),
            .. Pair(s => E(s * 0.62f, 0.72f, -0.04f, 0.15f, 0.15f, 0.06f, PaintTag.InnerEar, 0.02f)),
        ];
        if (horns)
        {
            head.AddRange(Pair(s => C(P(s * 0.55f, 0.55f, -0.2f), P(s * 1.2f, 1.0f, -0.35f), 0.2f, 0.12f, PaintTag.Horn, 0.08f)));
            head.AddRange(Pair(s => C(P(s * 1.2f, 1.0f, -0.35f), P(s * 1.35f, 1.65f, 0.1f), 0.12f, 0.03f, PaintTag.Horn, 0.05f)));
        }

        var face = new FacePlan { Eye = P(0.36f, 0.2f, 0.72f), EyeRadius = 0.14f, Mouth = P(0f, -0.56f, 0.98f), MouthWidth = 0.34f };
        var torso = Torso(b, 1.6f, 1.6f, 1.15f, PaintTag.Fur);
        torso.Add(E(0f, b.Torso * 0.9f, -0.35f, 1.2f, 0.55f, 0.6f, PaintTag.Fur, 0.5f));
        return Build(name, b, torso, head, face, (Extremity.Paw, Extremity.Paw), FurPattern.None);
    }

    /// <summary>The Horse: tall and long-legged, with a long face, upright ears, a mane down its neck and hooves.</summary>
    private static AnimalPlan Horse()
    {
        var b = new AnimalProportions { HipWidth = 0.55f, Thigh = 1.55f, Shin = 1.5f, Ankle = 0.48f, Torso = 2.55f, Lean = 0.14f, ShoulderWidth = 1.02f, UpperArm = 1.35f, Forearm = 1.3f, Neck = 1.05f, NeckForward = 0.55f, LegGirth = 0.43f, ArmGirth = 0.35f, TailLength = 0.85f, TailGirth = 0.24f };
        List<SdfPart> head =
        [
            E(0f, 0.28f, -0.25f, 0.56f, 0.62f, 0.66f),
            C(P(0f, 0.12f, 0.1f), P(0f, -0.42f, 1.3f), 0.48f, 0.36f, PaintTag.Fur, 0.3f),
            E(0f, -0.5f, 1.38f, 0.4f, 0.36f, 0.36f, PaintTag.Muzzle, 0.2f),
            .. Pair(s => E(s * 0.18f, -0.38f, 1.68f, 0.08f, 0.08f, 0.05f, PaintTag.Nose, 0.03f)),
            .. Pair(s => C(P(s * 0.28f, 0.72f, -0.32f), P(s * 0.34f, 1.25f, -0.4f), 0.15f, 0.03f, PaintTag.Fur, 0.1f)),
            E(0f, 0.85f, -0.05f, 0.18f, 0.25f, 0.35f, PaintTag.Mane, 0.15f),
            E(0f, 0.05f, 0.62f, 0.12f, 0.35f, 0.22f, PaintTag.Belly, 0.05f, pitch: 0.6f),
        ];
        var torso = Torso(b, 1.2f, 1.15f, 1.0f);
        for (var i = 0; i < 4; i++)
        {
            var t = i / 3f;
            torso.Add(E(0f, b.Torso * (0.92f + (0.32f * t)), -0.42f + (b.NeckForward * 0.35f * t), 0.16f, 0.32f, 0.2f, PaintTag.Mane, 0.2f));
        }

        var face = new FacePlan { Eye = P(0.42f, 0.3f, 0.32f), EyeRadius = 0.15f, Mouth = P(0f, -0.78f, 1.48f), MouthWidth = 0.34f, MouthTilt = 0.5f };
        var tip = new List<SdfPart> { E(0f, -0.3f, -0.55f, 0.3f, 0.55f, 0.35f, PaintTag.Mane, 0.2f, pitch: 0.7f) };
        return Build("Horse", b, torso, head, face, (Extremity.Hoof, Extremity.Hoof), FurPattern.None, tip, 1.0f);
    }

    /// <summary>The Tortoise: squat and slow under a high plated shell, with a beaked head and stumpy feet.</summary>
    private static AnimalPlan Tortoise()
    {
        var b = new AnimalProportions { HipWidth = 0.66f, Thigh = 0.95f, Shin = 0.9f, Ankle = 0.38f, Torso = 2.45f, Lean = 0.1f, ShoulderWidth = 1.2f, UpperArm = 1.15f, Forearm = 1.1f, Neck = 0.8f, NeckForward = 0.45f, LegGirth = 0.55f, ArmGirth = 0.45f, TailLength = 0.3f, TailGirth = 0.16f };
        List<SdfPart> head =
        [
            E(0f, 0.05f, 0.05f, 0.66f, 0.58f, 0.82f),
            E(0f, -0.18f, 0.78f, 0.34f, 0.28f, 0.3f, PaintTag.Horn, 0.15f),
            .. Pair(s => E(s * 0.12f, 0.02f, 0.98f, 0.05f, 0.04f, 0.04f, PaintTag.Nose, 0.02f)),
        ];
        var torso = Torso(b, 1.3f, 1.35f, 1.0f);
        torso.Add(E(0f, b.Torso * 0.55f, -0.45f, 1.55f, b.Torso * 0.58f, 1.0f, PaintTag.Shell, 0.3f));
        torso.Add(E(0f, b.Torso * 0.5f, 0.42f, 1.1f, b.Torso * 0.46f, 0.55f, PaintTag.Belly, 0.25f));
        var face = new FacePlan { Eye = P(0.36f, 0.16f, 0.56f), EyeRadius = 0.13f, Mouth = P(0f, -0.4f, 0.86f), MouthWidth = 0.3f, MouthTilt = 0.4f };
        return Build("Tortoise", b, torso, head, face, (Extremity.Stump, Extremity.Stump), FurPattern.Plates);
    }

    /// <summary>Every adding part of a plan, for checks.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>Its parts.</returns>
    public static IEnumerable<SdfPart> AllParts(AnimalPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Torso.Concat(plan.Head).Concat(plan.Thigh).Concat(plan.Shin).Concat(plan.Foot).Concat(plan.UpperArm).Concat(plan.Forearm).Concat(plan.Hand).Concat(plan.TailSegment).Concat(plan.TailTip);
    }
}
