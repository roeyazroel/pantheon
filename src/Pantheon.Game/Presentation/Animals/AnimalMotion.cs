using System;

namespace Pantheon.Game.Presentation.Animals;

/// <summary>A limb's angles in radians: positive swings its end forward (+Z). The last keeps a foot flat or turns a hand.</summary>
/// <param name="Upper">Thigh or upper arm, from the hip or shoulder.</param>
/// <param name="Lower">Shin or forearm, from the knee or elbow.</param>
/// <param name="End">Foot or hand, from the ankle or wrist.</param>
public readonly record struct LimbAngles(float Upper, float Lower, float End)
{
    /// <summary>Between two limbs' angles.</summary>
    /// <param name="a">From.</param>
    /// <param name="b">To.</param>
    /// <param name="t">How far (0..1).</param>
    /// <returns>The blend.</returns>
    public static LimbAngles Lerp(LimbAngles a, LimbAngles b, float t) =>
        new(a.Upper + ((b.Upper - a.Upper) * t), a.Lower + ((b.Lower - a.Lower) * t), a.End + ((b.End - a.End) * t));
}

/// <summary>
/// A Creature's whole pose (ADR-061): its hips' height, the torso's lean, sway and twist, its four
/// limbs, how far its arms are raised to the sides, its head and its tail.
/// </summary>
public sealed record BodyPose
{
    /// <summary>The hips' height above the ground, in body units.</summary>
    public required float HipHeight { get; init; }

    /// <summary>The torso's forward lean, in radians.</summary>
    public float Lean { get; init; }

    /// <summary>The torso's sideways sway.</summary>
    public float Roll { get; init; }

    /// <summary>The torso's twist about the spine.</summary>
    public float Twist { get; init; }

    /// <summary>The left leg.</summary>
    public LimbAngles LeftLeg { get; init; }

    /// <summary>The right leg.</summary>
    public LimbAngles RightLeg { get; init; }

    /// <summary>The left arm.</summary>
    public LimbAngles LeftArm { get; init; }

    /// <summary>The right arm.</summary>
    public LimbAngles RightArm { get; init; }

    /// <summary>How far the arms are raised to the sides, in radians (the left by its own, the right by its own).</summary>
    public (float Left, float Right) ArmsOut { get; init; }

    /// <summary>The head's nod: positive looks down.</summary>
    public float HeadPitch { get; init; }

    /// <summary>The head's turn.</summary>
    public float HeadYaw { get; init; }

    /// <summary>The tail's lift and swing.</summary>
    public (float Pitch, float Yaw) Tail { get; init; }

    /// <summary>Between two poses.</summary>
    /// <param name="a">From.</param>
    /// <param name="b">To.</param>
    /// <param name="t">How far (0..1).</param>
    /// <returns>The blend.</returns>
    public static BodyPose Lerp(BodyPose a, BodyPose b, float t)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        float L(float x, float y) => x + ((y - x) * t);
        return new BodyPose
        {
            HipHeight = L(a.HipHeight, b.HipHeight),
            Lean = L(a.Lean, b.Lean),
            Roll = L(a.Roll, b.Roll),
            Twist = L(a.Twist, b.Twist),
            LeftLeg = LimbAngles.Lerp(a.LeftLeg, b.LeftLeg, t),
            RightLeg = LimbAngles.Lerp(a.RightLeg, b.RightLeg, t),
            LeftArm = LimbAngles.Lerp(a.LeftArm, b.LeftArm, t),
            RightArm = LimbAngles.Lerp(a.RightArm, b.RightArm, t),
            ArmsOut = (L(a.ArmsOut.Left, b.ArmsOut.Left), L(a.ArmsOut.Right, b.ArmsOut.Right)),
            HeadPitch = L(a.HeadPitch, b.HeadPitch),
            HeadYaw = L(a.HeadYaw, b.HeadYaw),
            Tail = (L(a.Tail.Pitch, b.Tail.Pitch), L(a.Tail.Yaw, b.Tail.Yaw)),
        };
    }
}

/// <summary>
/// How an upright animal moves (ADR-061). Its walk is driven by where its feet must be: a planted foot
/// stays where it touched down while the body passes over it, a swinging foot arcs forward to the
/// next step, and two-bone inverse kinematics bends the hip and knee to reach it. So feet never slide,
/// whatever the Creature's size or speed. The other poses (resting, working, fighting, playing, being
/// held) are set by the clock.
/// </summary>
public static class AnimalMotion
{
    /// <summary>How far the body moves in one full stride (two steps), in leg lengths, walking and running.</summary>
    public const float WalkStride = 1.6f;

    /// <summary>A running stride, in leg lengths.</summary>
    public const float RunStride = 2.6f;

    /// <summary>The body's travel in one full stride at a pace.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="pace">0 for a walk, 1 for a run.</param>
    /// <returns>The stride, in body units.</returns>
    public static float Stride(AnimalProportions b, float pace)
    {
        ArgumentNullException.ThrowIfNull(b);
        return (b.Thigh + b.Shin) * (WalkStride + ((RunStride - WalkStride) * Math.Clamp(pace, 0f, 1f)));
    }

    /// <summary>The share of a stride a foot stays planted: three fifths walking, a third running.</summary>
    /// <param name="pace">0 for a walk, 1 for a run.</param>
    /// <returns>The share.</returns>
    public static float Duty(float pace) => 0.62f - (0.27f * Math.Clamp(pace, 0f, 1f));

    /// <summary>
    /// Where a foot is, relative to the point under its hip, at a point of its stride: planted and moving
    /// back as the body passes, then lifted and swung forward.
    /// </summary>
    /// <param name="b">The proportions.</param>
    /// <param name="phase">The foot's own phase (0..1); it touches down at 0.</param>
    /// <param name="pace">0 for a walk, 1 for a run.</param>
    /// <returns>The foot's forward offset and its height off the ground, in body units.</returns>
    public static (float Forward, float Lift) Foot(AnimalProportions b, float phase, float pace)
    {
        ArgumentNullException.ThrowIfNull(b);
        var u = phase - MathF.Floor(phase);
        var duty = Duty(pace);
        var reach = Stride(b, pace) * duty * 0.5f;
        if (u < duty)
        {
            return (reach * (1f - (2f * u / duty)), 0f);
        }

        var s = (u - duty) / (1f - duty);
        var eased = s * s * (3f - (2f * s));
        var lift = (b.Thigh + b.Shin) * (0.12f + (0.13f * pace)) * MathF.Sin(MathF.PI * s);
        return (-reach + (2f * reach * eased), lift);
    }

    /// <summary>
    /// Bends a two-bone limb to reach a point (law of cosines). The knee bends forward and the elbow
    /// back; an out-of-reach point is reached as far as the limb goes.
    /// </summary>
    /// <param name="upper">The upper bone's length.</param>
    /// <param name="lower">The lower bone's length.</param>
    /// <param name="forward">The point's forward offset from the joint.</param>
    /// <param name="down">How far below the joint it lies.</param>
    /// <param name="kneeForward">Whether the middle joint bends forward (a knee) rather than back (an elbow).</param>
    /// <returns>The upper bone's forward swing and the lower bone's, relative to the upper.</returns>
    public static (float Upper, float Lower) Reach(float upper, float lower, float forward, float down, bool kneeForward)
    {
        var d = MathF.Sqrt((forward * forward) + (down * down));
        d = Math.Clamp(d, MathF.Abs(upper - lower) + 1e-3f, upper + lower - 1e-3f);
        var toward = MathF.Atan2(forward, down);
        var atHip = MathF.Acos(Math.Clamp(((upper * upper) + (d * d) - (lower * lower)) / (2f * upper * d), -1f, 1f));
        var atKnee = MathF.Acos(Math.Clamp(((upper * upper) + (lower * lower) - (d * d)) / (2f * upper * lower), -1f, 1f));
        var bend = MathF.PI - atKnee;
        return kneeForward ? (toward + atHip, -bend) : (toward - atHip, bend);
    }

    /// <summary>A walk or a run at a point of its stride, the feet on uneven ground.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="phase">The stride's phase: it grows by the distance moved over <see cref="Stride"/>.</param>
    /// <param name="pace">0 for a walk, 1 for a run.</param>
    /// <param name="ground">How far the ground under each foot lies above (positive) or below the ground under the body.</param>
    /// <returns>The pose.</returns>
    public static BodyPose Walk(AnimalProportions b, float phase, float pace, (float Left, float Right) ground = default)
    {
        ArgumentNullException.ThrowIfNull(b);
        pace = Math.Clamp(pace, 0f, 1f);
        var legLength = b.Thigh + b.Shin;
        var duty = Duty(pace);
        var bob = legLength * (0.035f + (0.03f * pace)) * (1f - MathF.Cos(4f * MathF.PI * (phase - (duty * 0.5f)))) * 0.5f;
        var hip = b.HipHeight - bob - (legLength * 0.04f * pace);
        var left = Leg(b, phase, pace, hip, ground.Left);
        var right = Leg(b, phase + 0.5f, pace, hip, ground.Right);
        var (leftForward, _) = Foot(b, phase, pace);
        var swing = leftForward / (Stride(b, pace) * duty * 0.5f);
        var armSwing = (0.35f + (0.35f * pace)) * swing;
        var elbow = 0.35f + (0.55f * pace);
        return new BodyPose
        {
            HipHeight = hip,
            Lean = b.Lean + (0.2f * pace),
            Roll = 0.035f * MathF.Sin(MathF.Tau * phase),
            Twist = 0.07f * swing,
            LeftLeg = left,
            RightLeg = right,
            LeftArm = new LimbAngles(-armSwing, elbow, 0.1f),
            RightArm = new LimbAngles(armSwing, elbow, 0.1f),
            HeadPitch = -(b.Lean * 0.6f) - (0.1f * pace),
            Tail = (0.25f + (0.25f * pace) + (0.08f * MathF.Sin(4f * MathF.PI * phase)), 0.18f * MathF.Sin(MathF.Tau * phase)),
        };
    }

    /// <summary>Standing still, breathing.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="clock">Seconds of the Creature's clock.</param>
    /// <returns>The pose.</returns>
    public static BodyPose Stand(AnimalProportions b, float clock)
    {
        ArgumentNullException.ThrowIfNull(b);
        var breathe = MathF.Sin(clock * 1.7f);
        var hip = b.HipHeight - (0.015f * (b.Thigh + b.Shin) * (1f + breathe));
        var leg = Leg(b, 0.31f, 0f, hip, 0f, planted: true);
        return new BodyPose
        {
            HipHeight = hip,
            Lean = b.Lean + (0.02f * breathe),
            LeftLeg = leg,
            RightLeg = leg,
            LeftArm = new LimbAngles(0.08f, 0.3f + (0.04f * breathe), 0.1f),
            RightArm = new LimbAngles(0.08f, 0.3f + (0.04f * breathe), 0.1f),
            ArmsOut = (0.08f, 0.08f),
            HeadPitch = -(b.Lean * 0.6f),
            Tail = (0.15f, 0.12f * MathF.Sin(clock * 0.9f)),
        };
    }

    /// <summary>Sitting on the ground to rest, legs before it and arms on its knees, nodding in sleep.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="clock">Seconds of the Creature's clock.</param>
    /// <returns>The pose.</returns>
    public static BodyPose Sit(AnimalProportions b, float clock)
    {
        ArgumentNullException.ThrowIfNull(b);
        var breathe = MathF.Sin(clock * 1.1f);
        var leg = new LimbAngles(1.35f, -1.25f, -0.1f);
        return new BodyPose
        {
            HipHeight = b.LegGirth * 1.05f,
            Lean = -0.12f + (0.03f * breathe),
            LeftLeg = leg with { Upper = 1.25f },
            RightLeg = leg,
            LeftArm = new LimbAngles(0.75f, 0.55f, 0.2f),
            RightArm = new LimbAngles(0.7f, 0.6f, 0.2f),
            ArmsOut = (0.15f, 0.15f),
            HeadPitch = 0.35f + (0.05f * breathe),
            Tail = (-0.5f, 0.4f),
        };
    }

    /// <summary>Working with both hands before it: feeding, building, foraging, eating.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="clock">Seconds of the Creature's clock.</param>
    /// <returns>The pose.</returns>
    public static BodyPose Work(AnimalProportions b, float clock)
    {
        ArgumentNullException.ThrowIfNull(b);
        var beat = MathF.Sin(clock * 5f);
        var leg = Leg(b, 0.31f, 0f, b.HipHeight * 0.9f, 0f, planted: true);
        return new BodyPose
        {
            HipHeight = b.HipHeight * 0.9f,
            Lean = b.Lean + 0.4f,
            LeftLeg = leg,
            RightLeg = leg,
            LeftArm = new LimbAngles(1.05f + (0.45f * beat), 0.7f, 0.3f),
            RightArm = new LimbAngles(1.05f - (0.45f * beat), 0.7f, 0.3f),
            HeadPitch = 0.35f,
            Tail = (0.2f, 0.25f * MathF.Sin(clock * 2f)),
        };
    }

    /// <summary>Boxing an enemy: a staggered stance, the guard up, blows thrown in turn.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="clock">Seconds of the Creature's clock.</param>
    /// <param name="offset">Sets one fighter's blows apart from the other's.</param>
    /// <returns>The pose.</returns>
    public static BodyPose Fight(AnimalProportions b, float clock, float offset)
    {
        ArgumentNullException.ThrowIfNull(b);
        var t = (clock * 1.6f) + offset;
        var leftBlow = Punch(t);
        var rightBlow = Punch(t + 0.5f);
        var hip = (b.HipHeight * 0.88f) - (0.08f * MathF.Abs(MathF.Sin(t * MathF.Tau)));
        return new BodyPose
        {
            HipHeight = hip,
            Lean = b.Lean + 0.25f + (0.12f * MathF.Max(leftBlow, rightBlow)),
            Twist = 0.35f * (leftBlow - rightBlow),
            LeftLeg = Leg(b, 0.12f, 0f, hip, 0f, planted: true),
            RightLeg = Leg(b, 0.55f, 0f, hip, 0f, planted: true),
            LeftArm = new LimbAngles(0.9f + (0.7f * leftBlow), 1.7f - (1.5f * leftBlow), 0f),
            RightArm = new LimbAngles(0.9f + (0.7f * rightBlow), 1.7f - (1.5f * rightBlow), 0f),
            ArmsOut = (0.25f, 0.25f),
            HeadPitch = -0.15f,
            Tail = (0.6f, 0.4f * MathF.Sin(clock * 6f)),
        };
    }

    /// <summary>A lunge with a raking blow from above, at someone's village.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="clock">Seconds of the Creature's clock.</param>
    /// <returns>The pose.</returns>
    public static BodyPose Attack(AnimalProportions b, float clock)
    {
        ArgumentNullException.ThrowIfNull(b);
        var rake = MathF.Max(0f, MathF.Sin(clock * 4.5f));
        var hip = b.HipHeight * 0.92f;
        return new BodyPose
        {
            HipHeight = hip,
            Lean = b.Lean + 0.25f + (0.4f * rake),
            LeftLeg = Leg(b, 0.1f, 0f, hip, 0f, planted: true),
            RightLeg = Leg(b, 0.6f, 0f, hip, 0f, planted: true),
            LeftArm = new LimbAngles(2.7f - (2f * rake), 0.4f, 0.2f),
            RightArm = new LimbAngles(0.6f, 1.2f, 0f),
            HeadPitch = -0.25f,
            Tail = (0.7f, 0f),
        };
    }

    /// <summary>Playing or greeting a friend: hopping with its arms thrown up, one waving.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="clock">Seconds of the Creature's clock.</param>
    /// <param name="offset">Sets one Creature's hops apart from another's.</param>
    /// <returns>The pose.</returns>
    public static BodyPose Play(AnimalProportions b, float clock, float offset)
    {
        ArgumentNullException.ThrowIfNull(b);
        var hop = MathF.Abs(MathF.Sin((clock * 3.5f) + offset));
        var legLength = b.Thigh + b.Shin;
        var leg = Leg(b, 0.31f, 0f, b.HipHeight, 0f, planted: true);
        return new BodyPose
        {
            HipHeight = b.HipHeight + (hop * legLength * 0.3f),
            Lean = b.Lean - 0.1f,
            LeftLeg = leg with { Lower = leg.Lower - (0.4f * hop) },
            RightLeg = leg with { Lower = leg.Lower - (0.4f * hop) },
            LeftArm = new LimbAngles(2.6f, 0.3f, 0f),
            RightArm = new LimbAngles(2.2f + (0.5f * MathF.Sin(clock * 7f)), 0.5f, 0f),
            ArmsOut = (0.5f, 0.7f),
            HeadPitch = -0.3f,
            Tail = (0.7f, 0.8f * MathF.Sin(clock * 9f)),
        };
    }

    /// <summary>Hanging from its god's hand: legs dangling and kicking, arms flailing, looking down.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="clock">Seconds of the Creature's clock.</param>
    /// <returns>The pose.</returns>
    public static BodyPose Dangle(AnimalProportions b, float clock)
    {
        ArgumentNullException.ThrowIfNull(b);
        var kick = MathF.Sin(clock * 3.1f);
        return new BodyPose
        {
            HipHeight = b.HipHeight,
            Lean = 0.05f,
            LeftLeg = new LimbAngles(0.25f * kick, -0.3f - (0.25f * MathF.Max(0f, kick)), 0.5f),
            RightLeg = new LimbAngles(-0.25f * kick, -0.3f - (0.25f * MathF.Max(0f, -kick)), 0.5f),
            LeftArm = new LimbAngles(0.6f + (0.5f * MathF.Sin(clock * 2.3f)), 0.6f, 0f),
            RightArm = new LimbAngles(0.6f - (0.5f * MathF.Sin(clock * 2.3f)), 0.6f, 0f),
            ArmsOut = (0.9f, 0.9f),
            HeadPitch = 0.45f,
            Tail = (-0.6f, 0.5f * kick),
        };
    }

    /// <summary>One leg reaching its foot: planted, or at a point of its stride.</summary>
    private static LimbAngles Leg(AnimalProportions b, float phase, float pace, float hip, float ground, bool planted = false)
    {
        var (forward, lift) = planted ? (Foot(b, phase, pace).Forward * 0.25f, 0f) : Foot(b, phase, pace);
        var down = hip - b.Ankle - lift - ground;
        var (upper, lower) = Reach(b.Thigh, b.Shin, forward, down, kneeForward: true);
        var toe = lift > 0f ? 0.25f * MathF.Sin(MathF.PI * Math.Clamp(lift / ((b.Thigh + b.Shin) * 0.25f), 0f, 1f)) : 0f;
        return new LimbAngles(upper, lower, -(upper + lower) + toe);
    }

    /// <summary>A blow's reach over time: a quick jab out and back, then a rest.</summary>
    private static float Punch(float t)
    {
        var u = t - MathF.Floor(t);
        return u < 0.25f ? MathF.Sin(u / 0.25f * MathF.PI) : 0f;
    }
}
