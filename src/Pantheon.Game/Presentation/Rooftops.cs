using System;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>
/// The tops of a village's buildings, so the camera keeps above them (ADR-057): on a steep village's
/// edge the ground under the camera can lie below a roof beside it. Pure.
/// </summary>
public static class Rooftops
{
    /// <summary>The highest roof within a reach of a point, if any.</summary>
    /// <param name="site">The village's layout.</param>
    /// <param name="x">The point's X, in metres.</param>
    /// <param name="z">The point's Z, in metres.</param>
    /// <param name="reach">How far beyond a house's walls a point still counts as over it, in metres.</param>
    /// <returns>The roof's height, or <c>null</c> when no house stands so near.</returns>
    public static float? Highest(SettlementSite site, float x, float z, float reach)
    {
        ArgumentNullException.ThrowIfNull(site);
        var point = new Vector2(x, z);
        if (Vector2.Distance(point, new Vector2(site.Center.X, site.Center.Z)) > site.HouseDiscRadius + Population.LocalNavGrid.HutRadius + reach + 10f)
        {
            return null;
        }

        float? highest = null;
        foreach (var house in site.Houses)
        {
            var radius = (Population.LocalNavGrid.HutRadius * house.Scale) + reach;
            if (Vector2.DistanceSquared(point, new Vector2(house.Position.X, house.Position.Z)) <= radius * radius)
            {
                var top = house.Position.Y + (HouseMesh.Top * house.Scale);
                highest = highest is { } h ? MathF.Max(h, top) : top;
            }
        }

        return highest;
    }
}
