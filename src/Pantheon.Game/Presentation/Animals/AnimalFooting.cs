using System;

namespace Pantheon.Game.Presentation.Animals;

/// <summary>The ground under a foot (ADR-069): how far it lies above the ground under the body, in body units, and how steeply it rises ahead of the foot, in radians.</summary>
/// <param name="Height">Its height above the ground under the body; negative below.</param>
/// <param name="Slope">Its rise ahead of the foot: positive uphill.</param>
public readonly record struct FootGround(float Height, float Slope);

/// <summary>
/// Plants a Creature's feet on uneven ground, whatever it is doing (ADR-069). A pose is made for flat
/// ground; this last pass moves each ankle by the ground under it, keeping its lift (a swinging foot
/// stays as high above its ground as above flat ground), bends the hip and knee to reach it (two-bone
/// inverse kinematics) and lays the foot along the slope. When a foot cannot reach low ground the hips
/// drop, and when it cannot fold enough for high ground they rise: no foot ever sinks into the ground.
/// </summary>
public static class AnimalFooting
{
    /// <summary>The farthest the hips drop for low ground, in leg lengths.</summary>
    public const float MaxDrop = 0.45f;

    /// <summary>The farthest the hips rise for high ground, in leg lengths.</summary>
    public const float MaxRise = 0.5f;

    /// <summary>How close below the hip a foot may fold, in leg lengths.</summary>
    public const float Fold = 0.3f;

    /// <summary>The steepest slope a foot is laid along, in radians.</summary>
    public const float SteepestSlope = 0.6f;

    /// <summary>Where a leg's ankle is from its hip (forward kinematics), in body units.</summary>
    /// <param name="b">The proportions.</param>
    /// <param name="leg">The leg's angles.</param>
    /// <returns>How far forward and how far below the hip the ankle is.</returns>
    public static (float Forward, float Down) Ankle(AnimalProportions b, LimbAngles leg)
    {
        ArgumentNullException.ThrowIfNull(b);
        var lower = leg.Upper + leg.Lower;
        return ((b.Thigh * MathF.Sin(leg.Upper)) + (b.Shin * MathF.Sin(lower)), (b.Thigh * MathF.Cos(leg.Upper)) + (b.Shin * MathF.Cos(lower)));
    }

    /// <summary>How far the hips must move, up positive, so each foot keeps its height above its own ground.</summary>
    /// <param name="pose">The pose, made for flat ground.</param>
    /// <param name="b">The proportions.</param>
    /// <param name="left">The ground under the left foot.</param>
    /// <param name="right">The ground under the right foot.</param>
    /// <returns>The shift, in body units: a rise wins over a drop, so no foot sinks.</returns>
    public static float HipShift(BodyPose pose, AnimalProportions b, FootGround left, FootGround right)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(b);
        var legLength = b.Thigh + b.Shin;
        var (riseL, dropL) = Room(b, pose.LeftLeg, left.Height);
        var (riseR, dropR) = Room(b, pose.RightLeg, right.Height);
        var rise = MathF.Max(riseL, riseR);
        var drop = MathF.Min(dropL, dropR);
        var shift = rise > 0f ? rise : drop < 0f ? MathF.Max(drop, rise) : 0f;
        return Math.Clamp(shift, -MaxDrop * legLength, MaxRise * legLength);
    }

    /// <summary>The pose with its hips moved and each foot planted on its ground, laid along its slope.</summary>
    /// <param name="pose">The pose, made for flat ground.</param>
    /// <param name="b">The proportions.</param>
    /// <param name="left">The ground under the left foot.</param>
    /// <param name="right">The ground under the right foot.</param>
    /// <param name="shift">How far the hips move, up positive (see <see cref="HipShift"/>).</param>
    /// <returns>The planted pose.</returns>
    public static BodyPose Plant(BodyPose pose, AnimalProportions b, FootGround left, FootGround right, float shift)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(b);
        return pose with
        {
            HipHeight = pose.HipHeight + shift,
            LeftLeg = Reach(b, pose.LeftLeg, left, shift),
            RightLeg = Reach(b, pose.RightLeg, right, shift),
        };
    }

    /// <summary>How far the hips may rise and must drop for one foot: the rise it needs to fold, and the drop it needs to reach.</summary>
    private static (float Rise, float Drop) Room(AnimalProportions b, LimbAngles leg, float ground)
    {
        var (forward, down) = Ankle(b, leg);
        var wanted = down - ground;
        var reach = b.Thigh + b.Shin - 1e-3f;
        var farthest = MathF.Sqrt(MathF.Max(0f, (reach * reach) - (forward * forward)));
        var nearest = MathF.Max(Fold * (b.Thigh + b.Shin), MathF.Abs(b.Thigh - b.Shin) + 1e-3f);
        return (nearest - wanted, MathF.Min(0f, farthest - wanted));
    }

    /// <summary>One leg reaching its ankle's place over its ground, the foot keeping its turn and laid along the slope.</summary>
    private static LimbAngles Reach(AnimalProportions b, LimbAngles leg, FootGround ground, float shift)
    {
        var (forward, down) = Ankle(b, leg);
        var (upper, lower) = AnimalMotion.Reach(b.Thigh, b.Shin, forward, down + shift - ground.Height, kneeForward: true);
        var footTurn = leg.Upper + leg.Lower + leg.End;
        return new LimbAngles(upper, lower, footTurn - (upper + lower) + Math.Clamp(ground.Slope, -SteepestSlope, SteepestSlope));
    }
}
