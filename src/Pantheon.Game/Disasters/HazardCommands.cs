using System;
using System.Globalization;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Disasters;

/// <summary>A hazard the debug tools can start (ADR-029).</summary>
public enum HazardKind
{
    /// <summary>A tornado at the point, running with the wind.</summary>
    Tornado,

    /// <summary>A landslide from the region at the point.</summary>
    Landslide,

    /// <summary>A meteor striking the point.</summary>
    Meteor,

    /// <summary>Blight in the fields around the point.</summary>
    Blight,

    /// <summary>A locust swarm at the point.</summary>
    Locusts,

    /// <summary>A wave on the coast nearest the point.</summary>
    Tsunami,

    /// <summary>The volcano wakes.</summary>
    Eruption,
}

/// <summary>Debug drivers for the hazards of ADR-029 (the I hotkey and <c>--disaster</c>). Each returns a status line; only the host changes its world.</summary>
public static class HazardCommands
{
    /// <summary>The hazard after another in the I cycle.</summary>
    /// <param name="kind">The hazard started last, or <c>null</c> for the first.</param>
    /// <returns>The next one, back to the first after the last.</returns>
    public static HazardKind Next(HazardKind? kind) => kind is not { } last ? HazardKind.Tornado : last switch
    {
        HazardKind.Tornado => HazardKind.Landslide,
        HazardKind.Landslide => HazardKind.Meteor,
        HazardKind.Meteor => HazardKind.Blight,
        HazardKind.Blight => HazardKind.Locusts,
        HazardKind.Locusts => HazardKind.Tsunami,
        HazardKind.Tsunami => HazardKind.Eruption,
        HazardKind.Eruption => HazardKind.Tornado,
    };

    /// <summary>Starts a hazard at a point.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="kind">The hazard.</param>
    /// <param name="target">The point.</param>
    /// <returns>Status line.</returns>
    public static string Start(GameSession session, HazardKind kind, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        var rules = session.Content.Disasters;
        var hazards = rules.Hazards;
        if (kind is not (HazardKind.Tsunami or HazardKind.Eruption) && !world.Regions.TryGetRegionIdAt(target.X, target.Z, out _))
        {
            return "Point at the land to start a hazard";
        }

        world.Regions.TryGetRegionIdAt(target.X, target.Z, out var regionId);
        return kind switch
        {
            HazardKind.Tornado => Tornado(world, hazards, target),
            HazardKind.Landslide => Landslide(world, hazards, regionId),
            HazardKind.Meteor => Format("A falling star strikes at ({0:0}, {1:0}): magnitude {2:0.0}", target.X, target.Z, session.Command((_, events) =>
                HazardModel.FallingStar(rules, session.Content.SettlementRules, session.Content.DivineRules, session.Content.Domains, session.Content.Creatures.Callings, world, target, events)).Magnitude),
            HazardKind.Blight => Blight(world, regionId),
            HazardKind.Locusts => Locusts(world, hazards, target),
            HazardKind.Tsunami => HazardModel.RaiseTsunami(hazards.Tsunami with { MinMagnitude = 0f, CoastDistance = float.MaxValue }, world, target, 7f) is { } wave
                ? Format("A great wave comes ashore at ({0:0}, {1:0}), {2:0} m inland", wave.Shore.X, wave.Shore.Z, wave.Radius)
                : "The wave found no low land to flood",
            HazardKind.Eruption => HazardModel.Wake(hazards.Volcano, world) is { } volcano
                ? Format("{0} wakes: it erupts in {1:0} seconds", DisasterText.VolcanoName(world, volcano.RegionId), hazards.Volcano.RumbleSeconds)
                : "This island has no volcano",
        };
    }

    /// <summary>A tornado at the point, running with the prevailing wind.</summary>
    private static string Tornado(WorldState world, Domain.Disasters.HazardRules hazards, GroundPosition target)
    {
        var (windX, windZ) = WeatherModel.WindVector(world.Weather);
        HazardModel.SpawnTornado(hazards.Tornado, world, target, MathF.Atan2(windZ, windX), hazards.Tornado.MaxSeconds);
        return Format("A tornado touches down at ({0:0}, {1:0})", target.X, target.Z);
    }

    /// <summary>The region at the point slides onto the land below it.</summary>
    private static string Landslide(WorldState world, Domain.Disasters.HazardRules hazards, int regionId)
    {
        var below = HazardModel.Downhill(world.Regions, regionId)?.RegionId ?? regionId;
        HazardModel.Slide(hazards.Landslide, world, regionId, below);
        return Format("The slope at region #{0} gives way onto region #{1}", regionId, below);
    }

    /// <summary>A locust swarm at the point.</summary>
    private static string Locusts(WorldState world, Domain.Disasters.HazardRules hazards, GroundPosition target)
    {
        HazardModel.SpawnSwarm(world, target, hazards.Locusts.MaxSeconds);
        return Format("Locusts rise at ({0:0}, {1:0})", target.X, target.Z);
    }

    /// <summary>Blight in the fields around the point.</summary>
    private static string Blight(WorldState world, int regionId)
    {
        var fields = 0;
        foreach (var region in Simulation.Ecology.EcologyModel.Around(world.Regions, regionId, 1))
        {
            if (SettlementEconomy.IsFarmland(region))
            {
                region.Blight = Math.Max(region.Blight, 0.6f);
                fields++;
            }
        }

        return Format("Blight takes {0} fields around region #{1}", fields, regionId);
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
