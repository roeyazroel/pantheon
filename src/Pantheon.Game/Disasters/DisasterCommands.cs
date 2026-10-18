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
        if (!session.World.Regions.TryGetRegionIdAt(target.X, target.Z, out var regionId))
        {
            return "Point at the land to start a fire";
        }

        var region = session.World.Regions.Cells[regionId];
        var flammability = DisasterModel.Flammability(session.Content.Disasters, region);
        region.Fire = Math.Max(region.Fire, DebugFire);
        return string.Format(CultureInfo.InvariantCulture, "Region #{0} is burning (flammability {1:0.00}: the fire {2})", regionId, flammability, flammability >= session.Content.Disasters.MinFlammability ? "can spread" : "will soon die out");
    }
}
