using System;
using System.Globalization;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;

namespace Pantheon.Game.Disasters;

/// <summary>
/// Debug drivers for disasters (ADR-014): start an earthquake or a fire at a point, so their damage
/// and the settlements' response can be seen without waiting. Each returns a status line.
/// </summary>
public static class DisasterCommands
{
    /// <summary>Magnitude of a debug earthquake.</summary>
    public const float DebugMagnitude = 6.5f;

    /// <summary>Intensity of a debug fire.</summary>
    public const float DebugFire = 0.9f;

    /// <summary>Starts a natural earthquake at a point.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="target">The epicentre.</param>
    /// <returns>Status line.</returns>
    public static string StartQuake(GameSession session, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var quake = DisasterModel.StartQuake(session.Content.Disasters, session.World.Disasters, target, DebugMagnitude, session.World.Tick, godId: null);
        return string.Format(CultureInfo.InvariantCulture, "An earthquake of magnitude {0:0.0} strikes at ({1:0}, {2:0}); it shakes {3:0} m around", quake.Magnitude, target.X, target.Z, quake.Radius);
    }

    /// <summary>Sets the region at a point alight, burnable or not, so a fire can be studied anywhere on land.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="target">The point.</param>
    /// <returns>Status line.</returns>
    public static string StartFire(GameSession session, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        if (!session.World.Regions.TryGetRegionIdAt(target.X, target.Z, out var regionId))
        {
            return "Point at the land to start a fire";
        }

        var region = session.World.Regions.Cells[regionId];
        var flammability = DisasterModel.Flammability(session.Content.Disasters, region);
        region.Fire = Math.Max(region.Fire, DebugFire);
        return string.Format(CultureInfo.InvariantCulture, "Region #{0} is burning (flammability {1:0.00}: the fire {2})", regionId, flammability, flammability >= session.Content.Disasters.MinFlammability ? "can spread" : "will soon die out");
    }

    /// <summary>
    /// Floods the land at a point (ADR-022): the nearest river within <see cref="DebugFloodReach"/>
    /// bursts its banks, or, with none so near, a wave floods the low land around the point.
    /// </summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="target">The point.</param>
    /// <returns>Status line.</returns>
    public static string StartFlood(GameSession session, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        Domain.Water.River? nearest = null;
        var nearestDistance = DebugFloodReach;
        foreach (var river in world.Water.Rivers)
        {
            foreach (var regionId in river.Regions)
            {
                var centre = world.Regions.GetRegionCenter(regionId);
                var distance = MathF.Sqrt(((centre.X - target.X) * (centre.X - target.X)) + ((centre.Z - target.Z) * (centre.Z - target.Z)));
                if (distance < nearestDistance)
                {
                    (nearest, nearestDistance) = (river, distance);
                }
            }
        }

        if (nearest is not null)
        {
            nearest.Level = Math.Max(nearest.Level, session.Content.Water.MaxFloodLevel);
            return string.Format(CultureInfo.InvariantCulture, "The {0} bursts its banks; it floods the low land downstream", nearest.Name);
        }

        return Simulation.Water.WaterModel.Inundate(world, target, DebugWaveRadius, DebugWaveHeight) is { } deepest
            ? string.Format(CultureInfo.InvariantCulture, "A wave floods the low land around ({0:0}, {1:0}); deepest in region #{2}", target.X, target.Z, deepest)
            : "No river nearby, and the land here is too high to flood";
    }

    /// <summary>Distance within which a debug flood picks a river rather than a wave, in metres.</summary>
    public const float DebugFloodReach = 400f;

    /// <summary>Radius of a debug wave, in metres.</summary>
    public const float DebugWaveRadius = 180f;

    /// <summary>Height of a debug wave, in metres.</summary>
    public const float DebugWaveHeight = 12f;
}
