using System;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Game.Presentation;

/// <summary>Finds the settlement nearest to a point, for selection and targeting.</summary>
public static class SettlementPicker
{
    /// <summary>The settlement whose centre is nearest to a point, within a distance.</summary>
    /// <param name="world">World state.</param>
    /// <param name="point">Ground point.</param>
    /// <param name="maxDistance">Furthest distance in metres.</param>
    /// <returns>The settlement, or <c>null</c> when none is close enough.</returns>
    public static SettlementState? Nearest(WorldState world, GroundPosition point, float maxDistance)
    {
        ArgumentNullException.ThrowIfNull(world);
        SettlementState? best = null;
        var bestDistance = maxDistance;
        foreach (var settlement in world.Settlements)
        {
            var dx = settlement.Position.X - point.X;
            var dz = settlement.Position.Z - point.Z;
            var distance = MathF.Sqrt((dx * dx) + (dz * dz));
            if (distance <= bestDistance)
            {
                best = settlement;
                bestDistance = distance;
            }
        }

        return best;
    }
}
