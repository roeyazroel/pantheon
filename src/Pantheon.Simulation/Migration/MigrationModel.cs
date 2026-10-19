using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Migration;

/// <summary>How hard life pushes a settlement's people to leave, and its main cause (ADR-016).</summary>
/// <param name="Value">The pressure (0..1).</param>
/// <param name="Cause">The cause that weighs most.</param>
public readonly record struct MigrationPressure(float Value, MigrationCause Cause);

/// <summary>
/// The rules of migration as pure functions (spec §31–32, ADR-016): what pushes people out of a
/// settlement, what draws them to another, what they believe on the road, and what becomes of
/// them and their beliefs when they arrive.
/// </summary>
public static class MigrationModel
{
    /// <summary>Share of a god's followers among the migrants that must give it up for the group to count as abandoning it.</summary>
    public const float NoticeableAbandonment = 0.1f;

    /// <summary>The pressure on a settlement's people to leave.</summary>
    /// <param name="rules">Migration rules.</param>
    /// <param name="world">World state, for the gods at war.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="conditions">Its conditions (hunger and danger).</param>
    /// <returns>The pressure and its main cause.</returns>
    public static MigrationPressure Push(MigrationRules rules, WorldState world, SettlementState settlement, SettlementConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        var feelings = FollowersFeelings(settlement.Beliefs);
        Span<float> terms =
        [
            rules.FamineWeight * conditions.FoodNeed,
            rules.HomelessWeight * SettlementEconomy.ComputeHomelessShare(settlement),
            rules.DangerWeight * conditions.DangerNeed,
            rules.WarWeight * War(rules, world, settlement),
            rules.FearWeight * feelings.Fear,
            rules.DoubtWeight * feelings.Doubt,
            rules.UnhappinessWeight * Squared(1f - settlement.Society.Happiness),
            rules.InstabilityWeight * Squared(1f - settlement.Society.Stability),
        ];
        var total = 0f;
        var strongest = 0;
        for (var i = 0; i < terms.Length; i++)
        {
            total += terms[i];
            if (terms[i] > terms[strongest])
            {
                strongest = i;
            }
        }

        var pressure = SimMath.Clamp01(total) * (1f - (rules.LoveLoyalty * feelings.Love));
        return new MigrationPressure(SimMath.Clamp01(pressure), (MigrationCause)strongest);
    }

    /// <summary>The square of a share, so only a large one weighs much.</summary>
    private static float Squared(float share) => SimMath.Clamp01(share) * SimMath.Clamp01(share);

    /// <summary>How far gods at war over the settlement drive its people out: 1 when two gods at war each hold half of them.</summary>
    /// <param name="rules">Migration rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The war pressure (0..1).</returns>
    public static float War(MigrationRules rules, WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var war = 0f;
        var beliefs = settlement.Beliefs;
        for (var i = 0; i < beliefs.Count; i++)
        {
            for (var j = i + 1; j < beliefs.Count; j++)
            {
                var weaker = Math.Min(beliefs[i].Adherence, beliefs[j].Adherence);
                if (weaker >= rules.WarPresence && DiplomacyModel.AreAtWar(world, beliefs[i].GodId, beliefs[j].GodId))
                {
                    war = Math.Max(war, SimMath.Clamp01(weaker / 0.5f));
                }
            }
        }

        return war;
    }

    /// <summary>The followers' love, fear and doubt of their gods, weighted by how many follow each.</summary>
    /// <param name="beliefs">A settlement's or a group's beliefs.</param>
    /// <returns>The weighted feelings; zero when nobody follows a god.</returns>
    public static (float Love, float Fear, float Doubt) FollowersFeelings(IReadOnlyList<GodBelief> beliefs)
    {
        ArgumentNullException.ThrowIfNull(beliefs);
        var (adherence, love, fear, doubt) = (0f, 0f, 0f, 0f);
        foreach (var belief in beliefs)
        {
            adherence += belief.Adherence;
            love += belief.Adherence * belief.Love;
            fear += belief.Adherence * belief.Fear;
            doubt += belief.Adherence * belief.Doubt;
        }

        return adherence > 0f ? (love / adherence, fear / adherence, doubt / adherence) : (0f, 0f, 0f);
    }

    /// <summary>The god most of a population follows, if any follow one.</summary>
    /// <param name="beliefs">The beliefs.</param>
    /// <returns>The god with the highest adherence (the lowest id on a tie), or <c>null</c>.</returns>
    public static int? MainGod(IReadOnlyList<GodBelief> beliefs)
    {
        ArgumentNullException.ThrowIfNull(beliefs);
        GodBelief? main = null;
        foreach (var belief in beliefs)
        {
            if (belief.Adherence > 0f && (main is null || belief.Adherence > main.Adherence || (belief.Adherence == main.Adherence && belief.GodId < main.GodId)))
            {
                main = belief;
            }
        }

        return main?.GodId;
    }

    /// <summary>
    /// What the people leaving a settlement believe (spec §31: populations abandon gods): the
    /// settlement's beliefs, except that those who leave give up following each god in proportion
    /// to how much they fear or doubt it, <c>AbandonShare × max(fear, doubt)</c> of its followers.
    /// </summary>
    /// <param name="rules">Migration rules.</param>
    /// <param name="beliefs">The settlement's beliefs.</param>
    /// <returns>A copy of the beliefs for the group, and the god that lost most of them, if it lost at least <see cref="NoticeableAbandonment"/> of its followers among them.</returns>
    public static (List<GodBelief> Beliefs, int? Abandoned) MigrantBeliefs(MigrationRules rules, IReadOnlyList<GodBelief> beliefs)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(beliefs);
        var copy = new List<GodBelief>(beliefs.Count);
        int? abandoned = null;
        var most = 0f;
        foreach (var belief in beliefs)
        {
            var share = rules.AbandonShare * Math.Max(belief.Fear, belief.Doubt);
            var lost = belief.Adherence * share;
            copy.Add(new GodBelief { GodId = belief.GodId, Adherence = belief.Adherence - lost, Faith = belief.Faith, Love = belief.Love, Fear = belief.Fear, Doubt = belief.Doubt, Dependency = belief.Dependency, Resentment = belief.Resentment });
            if (lost > most && share >= NoticeableAbandonment)
            {
                most = lost;
                abandoned = belief.GodId;
            }
        }

        return (copy, abandoned);
    }

    /// <summary>
    /// How much a settlement draws migrants: room, food, safety and their own gods followed there,
    /// less the distance.
    /// </summary>
    /// <param name="rules">Migration rules.</param>
    /// <param name="settlement">The candidate destination.</param>
    /// <param name="conditions">Its conditions.</param>
    /// <param name="migrants">The migrants' beliefs.</param>
    /// <param name="distance">Metres to it.</param>
    /// <returns>The appeal (higher is better).</returns>
    public static float Appeal(MigrationRules rules, SettlementState settlement, SettlementConditions conditions, IReadOnlyList<GodBelief> migrants, float distance)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(migrants);
        var room = SimMath.Clamp01((settlement.Housing - settlement.Population) / (float)Math.Max(1, settlement.Population));
        return (rules.RoomWeight * room)
            + (rules.FoodWeight * (1f - conditions.FoodNeed))
            + (rules.SafetyWeight * (1f - conditions.DangerNeed))
            + (rules.FaithWeight * FaithMatch(migrants, settlement.Beliefs))
            - (rules.DistanceWeight * distance / rules.MaxDistance);
    }

    /// <summary>How far the migrants find their own gods followed in a settlement (0..1).</summary>
    /// <param name="migrants">The migrants' beliefs.</param>
    /// <param name="host">The settlement's beliefs.</param>
    /// <returns>The share-weighted overlap, per migrant who follows a god.</returns>
    public static float FaithMatch(IReadOnlyList<GodBelief> migrants, IReadOnlyList<GodBelief> host)
    {
        ArgumentNullException.ThrowIfNull(migrants);
        ArgumentNullException.ThrowIfNull(host);
        var following = 0f;
        var match = 0f;
        foreach (var belief in migrants)
        {
            following += belief.Adherence;
            if (BeliefModel.Find(host, belief.GodId) is { } there)
            {
                match += belief.Adherence * there.Adherence;
            }
        }

        return following > 0f ? match / following : 0f;
    }

    /// <summary>
    /// A god that demands conversion takes <c>ConversionDemand</c> of the migrants who follow other
    /// gods or none (spec §31). The converts share its followers' faith there, and fear and doubt it.
    /// </summary>
    /// <param name="rules">Migration rules.</param>
    /// <param name="migrants">The migrants' beliefs, changed in place.</param>
    /// <param name="hostGodId">The god demanding conversion.</param>
    /// <param name="hostFaith">Its followers' faith in the settlement they reach.</param>
    /// <returns>The share of the migrants converted.</returns>
    public static float Convert(MigrationRules rules, List<GodBelief> migrants, int hostGodId, float hostFaith)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(migrants);
        var host = BeliefModel.Find(migrants, hostGodId);
        if (host is null)
        {
            host = new GodBelief { GodId = hostGodId, Adherence = 0f, Faith = 0f, Love = 0f, Fear = 0f, Doubt = 0f };
            migrants.Add(host);
        }

        var moved = Math.Max(0f, 1f - BeliefModel.TotalAdherence(migrants)) * rules.ConversionDemand;
        foreach (var belief in migrants)
        {
            if (belief != host)
            {
                var taken = belief.Adherence * rules.ConversionDemand;
                belief.Adherence -= taken;
                moved += taken;
            }
        }

        if (moved > 0f)
        {
            var total = host.Adherence + moved;
            host.Faith = ((host.Faith * host.Adherence) + (hostFaith * moved)) / total;
            host.Love = host.Love * host.Adherence / total;
            host.Fear = ((host.Fear * host.Adherence) + (rules.ConversionFear * moved)) / total;
            host.Doubt = ((host.Doubt * host.Adherence) + (rules.ConversionDoubt * moved)) / total;
            host.Adherence = total;
        }

        return moved;
    }

    /// <summary>
    /// Migrants join a settlement: its people, stores, health and beliefs become the
    /// population-weighted mix of both (spec §32: conversion through migration).
    /// </summary>
    /// <param name="settlement">The settlement, changed in place.</param>
    /// <param name="group">The arriving group.</param>
    public static void Merge(SettlementState settlement, MigrationGroup group)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(group);
        var (stay, come) = (Math.Max(0, settlement.Population), Math.Max(0, group.Population));
        var total = stay + come;
        if (total == 0)
        {
            return;
        }

        foreach (var arriving in group.Beliefs)
        {
            if (BeliefModel.Find(settlement.Beliefs, arriving.GodId) is null)
            {
                settlement.Beliefs.Add(new GodBelief { GodId = arriving.GodId, Adherence = 0f, Faith = 0f, Love = 0f, Fear = 0f, Doubt = 0f });
            }
        }

        foreach (var belief in settlement.Beliefs)
        {
            var arriving = BeliefModel.Find(group.Beliefs, belief.GodId);
            var here = stay * belief.Adherence;
            var there = come * (arriving?.Adherence ?? 0f);
            var (wHere, wThere) = here + there > 0f ? (here, there) : (stay, arriving is null ? 0f : come);
            var weight = wHere + wThere;
            if (weight > 0f && arriving is not null)
            {
                belief.Faith = ((belief.Faith * wHere) + (arriving.Faith * wThere)) / weight;
                belief.Love = ((belief.Love * wHere) + (arriving.Love * wThere)) / weight;
                belief.Fear = ((belief.Fear * wHere) + (arriving.Fear * wThere)) / weight;
                belief.Doubt = ((belief.Doubt * wHere) + (arriving.Doubt * wThere)) / weight;
            }

            belief.Adherence = (here + there) / total;
        }

        var sum = BeliefModel.TotalAdherence(settlement.Beliefs);
        if (sum > 1f)
        {
            foreach (var belief in settlement.Beliefs)
            {
                belief.Adherence /= sum;
            }
        }

        settlement.Health = SimMath.Clamp01(((settlement.Health * stay) + (group.Health * come)) / total);
        settlement.Food += group.Food;
        settlement.Population = total;
    }

    /// <summary>The settlement's people are grateful to the god that took the newcomers in: its love rises by their share.</summary>
    /// <param name="rules">Migration rules.</param>
    /// <param name="settlement">The settlement after the merge.</param>
    /// <param name="hostGodId">The welcoming god.</param>
    /// <param name="arrived">How many arrived.</param>
    public static void Welcome(MigrationRules rules, SettlementState settlement, int hostGodId, int arrived)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        if (BeliefModel.Find(settlement.Beliefs, hostGodId) is { } host && settlement.Population > 0)
        {
            host.Love = SimMath.Clamp01(host.Love + (rules.WelcomeLove * arrived / settlement.Population));
        }
    }

    /// <summary>Migrants turned away lose love for the god that did it.</summary>
    /// <param name="rules">Migration rules.</param>
    /// <param name="migrants">The migrants' beliefs.</param>
    /// <param name="godId">The god that turned them away.</param>
    public static void Resent(MigrationRules rules, IReadOnlyList<GodBelief> migrants, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (BeliefModel.Find(migrants, godId) is { } belief)
        {
            belief.Love = SimMath.Clamp01(belief.Love - rules.TurnedAwayLoss);
        }
    }

    /// <summary>A god's followers' share of people without a roof, across the settlements they live in.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The follower-weighted homeless share (0..1).</returns>
    public static float FollowersHomeless(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var (followers, homeless) = (0f, 0f);
        foreach (var settlement in world.Settlements)
        {
            if (BeliefModel.Find(settlement.Beliefs, godId) is { } belief)
            {
                var count = settlement.Population * belief.Adherence;
                followers += count;
                homeless += count * SettlementEconomy.ComputeHomelessShare(settlement);
            }
        }

        return followers > 0f ? homeless / followers : 0f;
    }
}
