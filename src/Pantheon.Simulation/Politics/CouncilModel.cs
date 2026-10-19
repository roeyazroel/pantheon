using System;
using System.Collections.Generic;
using Pantheon.Domain.History;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Politics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Culture;

namespace Pantheon.Simulation.Politics;

/// <summary>What the Divine Council's decrees in force mean (spec §42, ADR-025), as pure queries.</summary>
public static class CouncilModel
{
    /// <summary>Whether a decree condemns a god.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns><c>true</c> while condemned.</returns>
    public static bool IsCondemned(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Council.Decrees.Exists(d => d.Motion.Kind == CouncilMotionKind.Condemn && d.Motion.TargetGodId == godId);
    }

    /// <summary>Whether a decree makes a settlement neutral territory: no god converts or strikes there.</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <returns><c>true</c> while neutral.</returns>
    public static bool IsNeutral(WorldState world, int settlementId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Council.Decrees.Exists(d => d.Motion.Kind == CouncilMotionKind.NeutralTerritory && d.Motion.SettlementId == settlementId);
    }

    /// <summary>Whether a miracle is of a practice.</summary>
    /// <param name="miracle">The miracle.</param>
    /// <param name="practice">The practice.</param>
    /// <returns><c>true</c> when any of its effects is.</returns>
    public static bool Practises(MiracleDefinition miracle, Practice practice)
    {
        ArgumentNullException.ThrowIfNull(miracle);
        foreach (var effect in miracle.Effects)
        {
            var practised = practice switch
            {
                Practice.Smiting => effect.Kind is MiracleEffectKind.Smite or MiracleEffectKind.Judge,
                Practice.Cataclysms => effect.Kind is MiracleEffectKind.Ignite or MiracleEffectKind.Quake or MiracleEffectKind.Flood,
                Practice.Terror => effect.Kind is MiracleEffectKind.Terrify,
            };
            if (practised)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The decree a cast defies, if any: a prohibited practice anywhere, or a harmful miracle on
    /// protected sacred land or in neutral territory.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="miracle">The miracle.</param>
    /// <param name="target">Where it was cast.</param>
    /// <param name="reach">Its reach in metres.</param>
    /// <param name="affected">The settlements it reached.</param>
    /// <returns>The decree's motion, or <c>null</c>.</returns>
    public static CouncilMotion? Defied(WorldState world, MiracleDefinition miracle, GroundPosition target, float reach, IReadOnlyList<int> affected)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(miracle);
        ArgumentNullException.ThrowIfNull(affected);
        var harms = CultureModel.Harms(miracle);
        foreach (var decree in world.Diplomacy.Council.Decrees)
        {
            var motion = decree.Motion;
            var defied = motion.Kind switch
            {
                CouncilMotionKind.ProhibitPractice => motion.Practice is { } practice && Practises(miracle, practice),
                CouncilMotionKind.ProtectSacredLand => harms && world.HolySites.Find(s => s.Id == motion.SiteId) is { } site && Within(site.Position, target, reach + 40f),
                CouncilMotionKind.NeutralTerritory => harms && motion.SettlementId is { } id && Contains(affected, id),
                CouncilMotionKind.Condemn or CouncilMotionKind.CoordinateDisasterResponse or CouncilMotionKind.PantheonWar => false,
            };
            if (defied)
            {
                return motion;
            }
        }

        return null;
    }

    /// <summary>
    /// The factor on a miracle's cost under a coordinated disaster response: help that reaches a
    /// settlement struck lately costs less.
    /// </summary>
    /// <param name="rules">Politics rules; <c>null</c> for none, and the factor is 1.</param>
    /// <param name="world">World state.</param>
    /// <param name="miracle">The miracle.</param>
    /// <param name="target">Where it is cast.</param>
    /// <param name="reach">Its reach in metres.</param>
    /// <returns>The factor.</returns>
    public static float CostFactor(PoliticsRules? rules, WorldState world, MiracleDefinition miracle, GroundPosition target, float reach)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(miracle);
        if (rules is null || CultureModel.Harms(miracle) || !world.Diplomacy.Council.Decrees.Exists(d => d.Motion.Kind == CouncilMotionKind.CoordinateDisasterResponse))
        {
            return 1f;
        }

        var since = world.Tick - (long)(rules.StruckSeconds * SimulationTime.TicksPerSecond);
        foreach (var settlement in world.Settlements)
        {
            if (Within(settlement.Position, target, reach) && Struck(world, settlement, since))
            {
                return rules.CoordinatedCostFactor;
            }
        }

        return 1f;
    }

    /// <summary>Whether disaster struck a settlement since a tick: it has ruins, or the history records a quake, fire, flood, famine or thirst there.</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="sinceTick">The tick.</param>
    /// <returns><c>true</c> when struck.</returns>
    public static bool Struck(WorldState world, SettlementState settlement, long sinceTick)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.Ruins > 0 || settlement.Plague >= 0.05f || world.History.Exists(r => r.SettlementId == settlement.Id && r.Tick >= sinceTick
            && r.Kind is HistoryKind.Earthquake or HistoryKind.Fire or HistoryKind.Flood or HistoryKind.Famine or HistoryKind.Thirst);
    }

    /// <summary>Whether two points are within a distance.</summary>
    private static bool Within(GroundPosition a, GroundPosition b, float distance)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return (dx * dx) + (dz * dz) <= distance * distance;
    }

    /// <summary>Whether a list holds an id.</summary>
    private static bool Contains(IReadOnlyList<int> ids, int id)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                return true;
            }
        }

        return false;
    }
}
