using System;
using System.Numerics;

namespace Pantheon.Game.Population;

/// <summary>Small helpers for ground positions, which are world (X, Z) coordinates.</summary>
internal static class GroundMath
{
    /// <summary>A ground offset at an angle and distance.</summary>
    /// <param name="angle">Angle from +X towards +Z, in radians.</param>
    /// <param name="radius">Distance.</param>
    /// <returns>The offset.</returns>
    public static Vector2 Polar(float angle, float radius) => new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

    /// <summary>Yaw that faces along a ground direction (yaw 0 faces +Z, as for houses and fields).</summary>
    /// <param name="direction">Direction; need not be normalised.</param>
    /// <returns>Yaw in radians.</returns>
    public static float YawAlong(Vector2 direction) => MathF.Atan2(direction.X, direction.Y);

    /// <summary>Yaw that faces from one ground point towards another.</summary>
    /// <param name="from">Where the figure stands.</param>
    /// <param name="to">What it faces.</param>
    /// <returns>Yaw in radians.</returns>
    public static float YawTowards(Vector2 from, Vector2 to) => YawAlong(to - from);
}
