using System;
using System.Numerics;

namespace Pantheon.Simulation.Terrain;

/// <summary>
/// Ray against heightfield intersection, used for cursor picking. It works on the
/// <see cref="TerrainField"/>, so results do not depend on the terrain renderer.
/// </summary>
public static class TerrainRaycaster
{
    private const int RefinementIterations = 12;

    /// <summary>
    /// Marches a ray across the terrain surface <c>max(height, floorHeight)</c> and returns the
    /// first intersection. Passing <c>floorHeight = 0</c> treats the sea surface as solid, which
    /// is what the player sees.
    /// </summary>
    /// <param name="field">Terrain to test against.</param>
    /// <param name="origin">Ray origin in world metres.</param>
    /// <param name="direction">Ray direction; it does not need to be normalised.</param>
    /// <param name="maxDistance">Maximum distance along the ray, in metres.</param>
    /// <param name="floorHeight">Minimum surface height (for example the sea level).</param>
    /// <param name="hit">The intersection point when the method returns <c>true</c>.</param>
    /// <returns><c>true</c> when the ray hits the terrain inside the field.</returns>
    public static bool TryRaycast(TerrainField field, Vector3 origin, Vector3 direction, float maxDistance, float floorHeight, out Vector3 hit)
    {
        ArgumentNullException.ThrowIfNull(field);
        hit = default;
        if (direction.LengthSquared() < 1e-12f || !(maxDistance > 0f))
        {
            return false;
        }

        var dir = Vector3.Normalize(direction);
        if (!TryClipToField(field, origin, dir, maxDistance, out var start, out var end))
        {
            return false;
        }

        if (HeightAboveSurface(field, origin + (dir * start), floorHeight) <= 0f)
        {
            hit = origin + (dir * start);
            return true;
        }

        // Half-sample steps cannot skip a whole bilinear cell; bisection then refines the crossing.
        var step = field.Spacing * 0.5f;
        var previous = start;
        for (var t = start + step; t <= end + step; t += step)
        {
            var current = MathF.Min(t, end);
            if (HeightAboveSurface(field, origin + (dir * current), floorHeight) <= 0f)
            {
                hit = origin + (dir * Refine(field, origin, dir, previous, current, floorHeight));
                return true;
            }

            previous = current;
        }

        return false;
    }

    /// <summary>Signed height of a point above the pickable surface.</summary>
    private static float HeightAboveSurface(TerrainField field, Vector3 point, float floorHeight) =>
        point.Y - MathF.Max(field.SampleHeight(point.X, point.Z), floorHeight);

    /// <summary>Bisects between a point above the surface (<paramref name="above"/>) and one below (<paramref name="below"/>).</summary>
    private static float Refine(TerrainField field, Vector3 origin, Vector3 dir, float above, float below, float floorHeight)
    {
        for (var i = 0; i < RefinementIterations; i++)
        {
            var middle = (above + below) * 0.5f;
            if (HeightAboveSurface(field, origin + (dir * middle), floorHeight) > 0f)
            {
                above = middle;
            }
            else
            {
                below = middle;
            }
        }

        return below;
    }

    /// <summary>Clips the ray's XZ projection to the field's square footprint (slab method).</summary>
    private static bool TryClipToField(TerrainField field, Vector3 origin, Vector3 dir, float maxDistance, out float start, out float end)
    {
        start = 0f;
        end = maxDistance;
        var max = field.MaxCoordinate;
        return ClipAxis(origin.X, dir.X, max, ref start, ref end) && ClipAxis(origin.Z, dir.Z, max, ref start, ref end);
    }

    /// <summary>Narrows <c>[start, end]</c> to the part of the ray where one coordinate lies in <c>[0, max]</c>.</summary>
    private static bool ClipAxis(float origin, float direction, float max, ref float start, ref float end)
    {
        if (MathF.Abs(direction) < 1e-8f)
        {
            return origin >= 0f && origin <= max;
        }

        var t0 = (0f - origin) / direction;
        var t1 = (max - origin) / direction;
        if (t0 > t1)
        {
            (t0, t1) = (t1, t0);
        }

        start = MathF.Max(start, t0);
        end = MathF.Min(end, t1);
        return start <= end;
    }
}
