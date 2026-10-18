using System.Numerics;

namespace Pantheon.Game.Camera;

/// <summary>Turns a mouse position near the window border into camera pan axes.</summary>
public static class EdgeScroll
{
    /// <summary>
    /// Pan axes for a mouse position. Speed ramps up linearly across the border margin and is
    /// full at the outermost pixel.
    /// </summary>
    /// <param name="mouse">Mouse position in pixels, measured from the top-left corner.</param>
    /// <param name="viewportSize">Viewport size in pixels.</param>
    /// <param name="margin">Border width, in pixels, that triggers scrolling.</param>
    /// <returns>X = right and Y = forward (towards the top of the screen), each in -1..1. Zero when the
    /// mouse is away from the border or outside the viewport.</returns>
    public static Vector2 ComputeAxes(Vector2 mouse, Vector2 viewportSize, float margin)
    {
        var usable = margin > 0f && viewportSize.X > 2f * margin && viewportSize.Y > 2f * margin;
        var inside = mouse.X >= 0f && mouse.Y >= 0f && mouse.X <= viewportSize.X && mouse.Y <= viewportSize.Y;
        if (!usable || !inside)
        {
            return Vector2.Zero;
        }

        // Screen Y grows downwards, so the top border scrolls forward.
        return new Vector2(Axis(mouse.X, viewportSize.X, margin), -Axis(mouse.Y, viewportSize.Y, margin));
    }

    /// <summary>-1..0 inside the low border, 0..1 inside the high border, 0 elsewhere.</summary>
    private static float Axis(float position, float size, float margin)
    {
        if (position < margin)
        {
            return (position / margin) - 1f;
        }

        return position > size - margin ? 1f - ((size - position) / margin) : 0f;
    }
}
