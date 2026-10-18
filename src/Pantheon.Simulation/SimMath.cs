namespace Pantheon.Simulation;

/// <summary>Small deterministic float helpers shared by the generators and systems.</summary>
internal static class SimMath
{
    /// <summary>Clamps a value to the 0..1 range.</summary>
    public static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);

    /// <summary>Linear interpolation from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static float Lerp(float from, float to, float t) => from + ((to - from) * t);

    /// <summary>Hermite smoothstep of <paramref name="value"/> between two edges (0 below, 1 above).</summary>
    public static float SmoothStep(float edge0, float edge1, float value)
    {
        if (edge1 <= edge0)
        {
            return value < edge0 ? 0f : 1f;
        }

        var t = Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - (2f * t));
    }
}
