using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation.Animals;

/// <summary>What a stretch of a Creature's surface is painted as (ADR-061).</summary>
public enum PaintTag
{
    /// <summary>Its hide or fur, with its species' pattern.</summary>
    Fur,

    /// <summary>Its belly, chest and the light of its face.</summary>
    Belly,

    /// <summary>Its muzzle or face skin.</summary>
    Muzzle,

    /// <summary>Its nose.</summary>
    Nose,

    /// <summary>The inside of its ears.</summary>
    InnerEar,

    /// <summary>Horn, beak or claw.</summary>
    Horn,

    /// <summary>Hoof or sole.</summary>
    Hoof,

    /// <summary>Mane, tuft or a horse's hair.</summary>
    Mane,

    /// <summary>A tortoise's shell.</summary>
    Shell,
}

/// <summary>A fur's pattern, painted over <see cref="PaintTag.Fur"/>.</summary>
public enum FurPattern
{
    /// <summary>Plain.</summary>
    None,

    /// <summary>A tiger's dark stripes.</summary>
    Stripes,

    /// <summary>A cow's patches.</summary>
    Patches,

    /// <summary>A tortoise's plates and a soft mottle.</summary>
    Plates,
}

/// <summary>What a Creature has at the end of its legs and arms.</summary>
public enum Extremity
{
    /// <summary>Padded paws with blunt toes.</summary>
    Paw,

    /// <summary>Hooves.</summary>
    Hoof,

    /// <summary>A hand with a thumb.</summary>
    Hand,

    /// <summary>A stumpy round foot.</summary>
    Stump,
}

/// <summary>
/// The skeleton's proportions of an upright animal Creature (ADR-061), in body units before its
/// level's scale: hips, legs, torso, arms, neck and head. It walks upright as Black &amp; White's
/// Creatures did, with arms and hands to carry, build and fight.
/// </summary>
public sealed record AnimalProportions
{
    /// <summary>How much of the leg's full reach it stands with: the knees bend a little.</summary>
    public const float StandingReach = 0.93f;

    /// <summary>The hip joints' height above the ground when standing.</summary>
    public float HipHeight => Ankle + (StandingReach * (Thigh + Shin));

    /// <summary>Half the distance between the hip joints.</summary>
    public required float HipWidth { get; init; }

    /// <summary>Thigh, from hip to knee.</summary>
    public required float Thigh { get; init; }

    /// <summary>Shin, from knee to ankle.</summary>
    public required float Shin { get; init; }

    /// <summary>The ankle's height above the sole.</summary>
    public required float Ankle { get; init; }

    /// <summary>The torso, from the hips to the shoulders.</summary>
    public required float Torso { get; init; }

    /// <summary>How far the torso leans forward when standing, in radians.</summary>
    public required float Lean { get; init; }

    /// <summary>Half the distance between the shoulder joints.</summary>
    public required float ShoulderWidth { get; init; }

    /// <summary>Upper arm, from shoulder to elbow.</summary>
    public required float UpperArm { get; init; }

    /// <summary>Forearm, from elbow to wrist.</summary>
    public required float Forearm { get; init; }

    /// <summary>The neck, from between the shoulders to the head's centre.</summary>
    public required float Neck { get; init; }

    /// <summary>How far forward of the shoulders the neck reaches.</summary>
    public float NeckForward { get; init; }

    /// <summary>The leg's thickness at the hip.</summary>
    public required float LegGirth { get; init; }

    /// <summary>The arm's thickness at the shoulder.</summary>
    public required float ArmGirth { get; init; }

    /// <summary>The tail: its segments' length and girth at the root; 0 for none.</summary>
    public float TailLength { get; init; }

    /// <summary>The tail's girth at its root.</summary>
    public float TailGirth { get; init; }
}

/// <summary>Where the face's moving parts sit on a head (ADR-055, ADR-061), in head units.</summary>
public sealed record FacePlan
{
    /// <summary>The right eye's centre; the left mirrors it.</summary>
    public required Vector3 Eye { get; init; }

    /// <summary>The eyes' radius.</summary>
    public required float EyeRadius { get; init; }

    /// <summary>The mouth's centre, on the front of the muzzle.</summary>
    public required Vector3 Mouth { get; init; }

    /// <summary>The mouth's width.</summary>
    public required float MouthWidth { get; init; }

    /// <summary>How far the mouth tilts down to face the god's camera, in radians.</summary>
    public float MouthTilt { get; init; } = 0.6f;
}

/// <summary>
/// A species' whole body plan (ADR-061): its proportions, the shapes of each part in its joint's
/// space, its face, and how its surfaces are painted. The client builds a skeleton of joints from the
/// proportions, meshes each shape once, and hangs the meshes on the joints.
/// </summary>
public sealed record AnimalPlan
{
    /// <summary>The body's name: an animal (Cow, Ape...) or the Beast.</summary>
    public required string Name { get; init; }

    /// <summary>The skeleton.</summary>
    public required AnimalProportions Body { get; init; }

    /// <summary>The torso, pelvis to neck, in the torso's space (origin at the hips, up the spine).</summary>
    public required IReadOnlyList<SdfPart> Torso { get; init; }

    /// <summary>The head, in the head's space (origin at its centre, facing +Z).</summary>
    public required IReadOnlyList<SdfPart> Head { get; init; }

    /// <summary>A thigh, hanging down from the hip.</summary>
    public required IReadOnlyList<SdfPart> Thigh { get; init; }

    /// <summary>A shin, hanging down from the knee.</summary>
    public required IReadOnlyList<SdfPart> Shin { get; init; }

    /// <summary>A foot, below the ankle, toes towards +Z.</summary>
    public required IReadOnlyList<SdfPart> Foot { get; init; }

    /// <summary>An upper arm, hanging from the shoulder.</summary>
    public required IReadOnlyList<SdfPart> UpperArm { get; init; }

    /// <summary>A forearm, hanging from the elbow.</summary>
    public required IReadOnlyList<SdfPart> Forearm { get; init; }

    /// <summary>A hand, below the wrist.</summary>
    public required IReadOnlyList<SdfPart> Hand { get; init; }

    /// <summary>One tail segment, reaching back along -Z from its root; empty for no tail.</summary>
    public IReadOnlyList<SdfPart> TailSegment { get; init; } = [];

    /// <summary>The tail's tip, if it differs: a tuft or a bush.</summary>
    public IReadOnlyList<SdfPart> TailTip { get; init; } = [];

    /// <summary>The face.</summary>
    public required FacePlan Face { get; init; }

    /// <summary>The fur's pattern.</summary>
    public FurPattern Pattern { get; init; }
}
