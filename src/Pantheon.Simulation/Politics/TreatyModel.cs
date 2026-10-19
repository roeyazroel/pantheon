using System;
using System.Collections.Generic;
using Pantheon.Domain.Politics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Politics;

/// <summary>
/// What the treaties in force allow and forbid (spec §38–39, ADR-025), as pure queries. Every
/// system that a treaty changes asks here, so a treaty's effect lives in one place.
/// </summary>
public static class TreatyModel
{
    /// <summary>The treaty of a kind binding two gods, if one is in force.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The treaty, or <c>null</c>.</returns>
    public static Treaty? Find(WorldState world, int a, int b, TreatyKind kind)
    {
        ArgumentNullException.ThrowIfNull(world);
        return a == b ? null : world.Diplomacy.Treaties.Find(t => t.Kind == kind && t.Binds(a) && t.Binds(b));
    }

    /// <summary>Whether a treaty of a kind binds two gods.</summary>
    public static bool Has(WorldState world, int a, int b, TreatyKind kind) => Find(world, a, b, kind) is not null;

    /// <summary>Whether a god may know of a treaty: it is public, or the god is a party (spec §39).</summary>
    /// <param name="treaty">The treaty.</param>
    /// <param name="godId">The god; <c>null</c> for the host, which knows everything.</param>
    /// <returns><c>true</c> when known.</returns>
    public static bool KnownTo(Treaty treaty, int? godId)
    {
        ArgumentNullException.ThrowIfNull(treaty);
        return !treaty.Secret || godId is null || treaty.Binds(godId.Value);
    }

    /// <summary>The treaties a god knows of, binding it or not.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The treaties.</returns>
    public static List<Treaty> KnownBy(WorldState world, int? godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Treaties.FindAll(t => KnownTo(t, godId));
    }

    /// <summary>Whether a treaty forbids one god to declare war on another: a non-aggression pact.</summary>
    public static bool ForbidsWar(WorldState world, int godId, int otherId) => Has(world, godId, otherId, TreatyKind.NonAggression);

    /// <summary>Whether a joint war forbids a god a separate peace with its enemy.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god that would make peace.</param>
    /// <param name="enemyId">The enemy.</param>
    /// <returns><c>true</c> when a joint war against the enemy binds the god.</returns>
    public static bool ForbidsPeace(WorldState world, int godId, int enemyId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Treaties.Exists(t => t.Kind == TreatyKind.JointWar && t.TargetGodId == enemyId && t.Binds(godId));
    }

    /// <summary>Whether a god casts in another's influence as an ally would: allies, or a shared-influence treaty.</summary>
    public static bool SharesInfluence(WorldState world, int godId, int otherId) =>
        DiplomacyModel.AreAllied(world, godId, otherId) || Has(world, godId, otherId, TreatyKind.SharedInfluence);

    /// <summary>Whether two gods' Creatures are bound never to fight.</summary>
    public static bool SparesCreatures(WorldState world, int godId, int otherId) => Has(world, godId, otherId, TreatyKind.CreatureNonAggression);

    /// <summary>Whether a host god must welcome another god's migrants, whatever its refugee policy.</summary>
    public static bool WelcomesMigrants(WorldState world, int hostGodId, int migrantGodId) => Has(world, hostGodId, migrantGodId, TreatyKind.MigrationAgreement);

    /// <summary>Whether a god may preach in a settlement as if it led there: religious access to the god that leads it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The preaching god.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns><c>true</c> with access.</returns>
    public static bool HasAccess(WorldState world, int godId, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return Leader(settlement) is { } leader && Has(world, godId, leader, TreatyKind.ReligiousAccess);
    }

    /// <summary>Whether a god answers disasters among another's people by treaty.</summary>
    public static bool Assists(WorldState world, int godId, int otherId) => Has(world, godId, otherId, TreatyKind.DisasterAssistance);

    /// <summary>
    /// The partner a territorial agreement keeps a god away from at a point: the god that leads a
    /// settlement within reach of the point, if the two are bound by such an agreement.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The casting god.</param>
    /// <param name="target">Where it casts.</param>
    /// <param name="reach">The miracle's reach in metres.</param>
    /// <returns>The partner whose land it is, or <c>null</c>.</returns>
    public static int? ForbiddenLand(WorldState world, int godId, GroundPosition target, float reach)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Diplomacy.Treaties.Exists(t => t.Kind == TreatyKind.TerritorialAgreement && t.Binds(godId)))
        {
            return null;
        }

        foreach (var settlement in world.Settlements)
        {
            var dx = settlement.Position.X - target.X;
            var dz = settlement.Position.Z - target.Z;
            if (settlement.Population > 0 && (dx * dx) + (dz * dz) <= reach * reach && Leader(settlement) is { } leader && leader != godId
                && Has(world, godId, leader, TreatyKind.TerritorialAgreement))
            {
                return leader;
            }
        }

        return null;
    }

    /// <summary>
    /// The treaties that call gods to a partner's defence when an aggressor declares war on it: a
    /// defensive pact whose defender is the other party and whose target is the aggressor or any,
    /// and mutual defence either way.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="victimId">The god attacked.</param>
    /// <param name="aggressorId">The god that declared war.</param>
    /// <returns>The treaties, with the god each calls.</returns>
    public static List<(Treaty Treaty, int DefenderId)> Defenders(WorldState world, int victimId, int aggressorId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var found = new List<(Treaty, int)>();
        foreach (var treaty in world.Diplomacy.Treaties)
        {
            int? defender = null;
            if (treaty.Kind == TreatyKind.DefensivePact && treaty.SecondGodId == victimId && (treaty.TargetGodId is null || treaty.TargetGodId == aggressorId))
            {
                defender = treaty.FirstGodId;
            }
            else if (treaty.Kind == TreatyKind.MutualDefense && treaty.Binds(victimId))
            {
                defender = treaty.PartnerOf(victimId);
            }

            if (defender is { } id && id != aggressorId)
            {
                found.Add((treaty, id));
            }
        }

        return found;
    }

    /// <summary>The god most of a settlement's people follow, if any holds the largest share.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The god, or <c>null</c>.</returns>
    public static int? Leader(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        int? leader = null;
        var best = 0.05f;
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence > best)
            {
                (leader, best) = (belief.GodId, belief.Adherence);
            }
        }

        return leader;
    }

    /// <summary>Whether a god may know a proposal's terms or a treaty exists between two gods; for readouts.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god asking.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other.</param>
    /// <returns>The kinds of treaty it knows bind the two.</returns>
    public static List<Treaty> KnownBetween(WorldState world, int? godId, int a, int b)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Treaties.FindAll(t => t.Binds(a) && t.Binds(b) && KnownTo(t, godId));
    }

    /// <summary>The strongest follower share a god holds in a settlement (0 when none).</summary>
    internal static float Share(SettlementState settlement, int godId) => BeliefModel.Find(settlement, godId)?.Adherence ?? 0f;
}
