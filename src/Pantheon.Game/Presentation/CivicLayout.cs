using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Where a settlement's temple and walls stand (ADR-023), derived from its layout and never saved:
/// the temple beside the shrine, and the walls a ring of posts just beyond the houses with four
/// gates.
/// </summary>
public static class CivicLayout
{
    /// <summary>Distance of the temple from the shrine, in metres: within the ring the houses keep clear.</summary>
    public const float TempleDistance = 6f;

    /// <summary>Distance of the walls beyond the farthest house, in metres.</summary>
    public const float WallMargin = 6f;

    /// <summary>Metres between two posts of the walls.</summary>
    public const float PostSpacing = 2.5f;

    // Share of the ring left open at each of the four gates.
    private const float GateShare = 0.06f;

    /// <summary>The temple's place on the ground and its turn: beside the shrine, on a side chosen from the settlement's id, its front to the shrine.</summary>
    /// <param name="site">The settlement's layout.</param>
    /// <param name="terrain">Terrain.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <returns>The temple's centre on the ground, and its yaw (its local +Z points away from the shrine).</returns>
    public static (Vector3 Position, float Yaw) Temple(SettlementSite site, TerrainField terrain, int settlementId)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(terrain);
        var angle = PresentationHash.Unit(TempleSalt, (ulong)settlementId, 0) * MathF.Tau;
        var (dx, dz) = (MathF.Cos(angle), MathF.Sin(angle));
        var x = site.Center.X + (dx * TempleDistance);
        var z = site.Center.Z + (dz * TempleDistance);
        return (new Vector3(x, terrain.SampleHeight(x, z), z), MathF.Atan2(dx, dz));
    }

    /// <summary>The posts of the walls: a ring <see cref="WallMargin"/> beyond the farthest house, with four gates, on the ground and never in water.</summary>
    /// <param name="site">The settlement's layout.</param>
    /// <param name="terrain">Terrain.</param>
    /// <returns>The posts' feet.</returns>
    public static IReadOnlyList<Vector3> Walls(SettlementSite site, TerrainField terrain)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(terrain);
        var radius = site.HouseDiscRadius + WallMargin;
        var count = Math.Max(8, (int)MathF.Round(MathF.Tau * radius / PostSpacing));
        var posts = new List<Vector3>(count);
        for (var i = 0; i < count; i++)
        {
            var turn = (float)i / count;
            var fromGate = MathF.Abs((turn * 4f) - MathF.Round(turn * 4f)) / 4f;
            if (fromGate < GateShare)
            {
                continue;
            }

            var angle = turn * MathF.Tau;
            var x = site.Center.X + (MathF.Cos(angle) * radius);
            var z = site.Center.Z + (MathF.Sin(angle) * radius);
            var height = terrain.SampleHeight(x, z);
            if (height >= 0f && !terrain.IsUnderFreshWater(x, z))
            {
                posts.Add(new Vector3(x, height, z));
            }
        }

        return posts;
    }

    private const ulong TempleSalt = 31;
}
