using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Diplomacy;

/// <summary>Lookups and rules of relations between gods, as functions of the world (ADR-012).</summary>
public static class DiplomacyModel
{
    /// <summary>Weight of an act's memory when it is made: betrayal and war weigh most.</summary>
    /// <param name="act">The act.</param>
    /// <returns>Initial weight (0..1).</returns>
    public static float InitialWeight(DiplomaticAct act) => act switch
    {
        DiplomaticAct.Betrayed => 1f,
        DiplomaticAct.DeclaredWar => 0.8f,
        DiplomaticAct.Converted => 0.5f,
        DiplomaticAct.CreatureAttacked => 0.5f,
        DiplomaticAct.Harmed => 0.4f,
        DiplomaticAct.Allied => 0.6f,
        DiplomaticAct.MadePeace => 0.5f,
        DiplomaticAct.Helped => 0.3f,
        DiplomaticAct.Courted => 0.2f,
        DiplomaticAct.WrongedAlly => 0.25f,
        DiplomaticAct.Sheltered => 0.4f,
        DiplomaticAct.TurnedAway => 0.3f,
        DiplomaticAct.CreatureDefeated => 0.4f,
        DiplomaticAct.TookHolySite => 0.5f,
        DiplomaticAct.Rescued => 0.6f,
        DiplomaticAct.BrokeTreaty => 0.8f,
        DiplomaticAct.BrokePromise => 0.5f,
        DiplomaticAct.VotedAgainst => 0.3f,
        DiplomaticAct.DefiedCouncil => 0.4f,
        DiplomaticAct.KeptWord => 0.4f,
        DiplomaticAct.SignedTreaty => 0.4f,
        DiplomaticAct.PlottedAgainst => 0.7f,
    };

    /// <summary>Whether an act is hostile.</summary>
    /// <param name="act">The act.</param>
    /// <returns><c>true</c> for acts that build a grudge.</returns>
    public static bool IsHostile(DiplomaticAct act) => act switch
    {
        DiplomaticAct.Courted or DiplomaticAct.Harmed or DiplomaticAct.Converted or DiplomaticAct.CreatureAttacked or DiplomaticAct.Betrayed or DiplomaticAct.DeclaredWar or DiplomaticAct.WrongedAlly or DiplomaticAct.TurnedAway or DiplomaticAct.CreatureDefeated or DiplomaticAct.TookHolySite
            or DiplomaticAct.BrokeTreaty or DiplomaticAct.BrokePromise or DiplomaticAct.VotedAgainst or DiplomaticAct.DefiedCouncil or DiplomaticAct.PlottedAgainst => true,
        DiplomaticAct.Helped or DiplomaticAct.Allied or DiplomaticAct.MadePeace or DiplomaticAct.Sheltered or DiplomaticAct.Rescued or DiplomaticAct.KeptWord or DiplomaticAct.SignedTreaty => false,
    };

    /// <summary>A new relation with the starting values.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="observer">The god whose view it is.</param>
    /// <param name="subject">The god it regards.</param>
    /// <returns>The relation.</returns>
    public static GodRelation NewRelation(DiplomacyRules rules, int observer, int subject)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new GodRelation
        {
            ObserverGodId = observer,
            SubjectGodId = subject,
            Trust = rules.StartingTrust,
            Fear = 0f,
            Respect = 0f,
            Rivalry = rules.StartingRivalry,
            Gratitude = 0f,
            Suspicion = rules.StartingSuspicion,
            Memories = [],
        };
    }

    /// <summary>How one god sees another, if they have a relation.</summary>
    /// <param name="world">World state.</param>
    /// <param name="observer">The god whose view it is.</param>
    /// <param name="subject">The god it regards.</param>
    /// <returns>The relation, or <c>null</c>.</returns>
    public static GodRelation? FindRelation(WorldState world, int observer, int subject)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Relations.Find(r => r.ObserverGodId == observer && r.SubjectGodId == subject);
    }

    /// <summary>How one god sees another, created with the starting values when they have none yet.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="observer">The god whose view it is.</param>
    /// <param name="subject">The god it regards.</param>
    /// <returns>The relation.</returns>
    public static GodRelation EnsureRelation(DiplomacyRules rules, WorldState world, int observer, int subject)
    {
        if (FindRelation(world, observer, subject) is { } existing)
        {
            return existing;
        }

        var relation = NewRelation(rules, observer, subject);
        world.Diplomacy.Relations.Add(relation);
        return relation;
    }

    /// <summary>The stance between two gods, if recorded.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other god.</param>
    /// <returns>The stance, or <c>null</c>.</returns>
    public static GodStance? FindStance(WorldState world, int a, int b)
    {
        ArgumentNullException.ThrowIfNull(world);
        var (low, high) = a < b ? (a, b) : (b, a);
        return world.Diplomacy.Stances.Find(s => s.GodA == low && s.GodB == high);
    }

    /// <summary>The stance between two gods, created neutral when they have none yet.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other god.</param>
    /// <returns>The stance.</returns>
    public static GodStance EnsureStance(WorldState world, int a, int b)
    {
        if (FindStance(world, a, b) is { } existing)
        {
            return existing;
        }

        var (low, high) = a < b ? (a, b) : (b, a);
        var stance = new GodStance { GodA = low, GodB = high, Status = DiplomaticStatus.Neutral, SinceTick = world.Tick, LastProposalTick = null };
        world.Diplomacy.Stances.Add(stance);
        return stance;
    }

    /// <summary>The status between two gods; neutral when none is recorded, and for a god with itself.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other god.</param>
    /// <returns>The status.</returns>
    public static DiplomaticStatus StatusOf(WorldState world, int a, int b) =>
        a == b ? DiplomaticStatus.Alliance : FindStance(world, a, b)?.Status ?? DiplomaticStatus.Neutral;

    /// <summary>Whether two different gods are allied.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other god.</param>
    /// <returns><c>true</c> for allies.</returns>
    public static bool AreAllied(WorldState world, int a, int b) => a != b && StatusOf(world, a, b) == DiplomaticStatus.Alliance;

    /// <summary>Whether two gods are at war.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One god.</param>
    /// <param name="b">The other god.</param>
    /// <returns><c>true</c> at war.</returns>
    public static bool AreAtWar(WorldState world, int a, int b) => a != b && StatusOf(world, a, b) == DiplomaticStatus.War;

    /// <summary>
    /// The grudge a god bears another: the weight of the hostile acts it remembers, betrayal counting
    /// double and mere courting a third.
    /// </summary>
    /// <param name="relation">The relation.</param>
    /// <returns>The grudge (0 and up).</returns>
    public static float Grudge(GodRelation relation)
    {
        ArgumentNullException.ThrowIfNull(relation);
        var grudge = 0f;
        foreach (var memory in relation.Memories)
        {
            if (IsHostile(memory.Act))
            {
                grudge += memory.Weight * memory.Act switch
                {
                    DiplomaticAct.Betrayed => 2f,
                    DiplomaticAct.Courted => 0.3f,
                    _ => 1f,
                };
            }
        }

        return grudge;
    }

    /// <summary>The gods allied with a god, in id order.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>Its allies.</returns>
    public static List<int> AlliesOf(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var allies = new List<int>();
        foreach (var god in world.Gods)
        {
            if (AreAllied(world, godId, god.Id))
            {
                allies.Add(god.Id);
            }
        }

        return allies;
    }

    /// <summary>
    /// A god's bloc (ADR-015): the god and every god it is allied with, directly or through other
    /// allies, in id order.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The bloc, the god included.</returns>
    public static List<int> BlocOf(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var bloc = new List<int> { godId };
        for (var i = 0; i < bloc.Count; i++)
        {
            foreach (var ally in AlliesOf(world, bloc[i]))
            {
                if (!bloc.Contains(ally))
                {
                    bloc.Add(ally);
                }
            }
        }

        bloc.Sort();
        return bloc;
    }

    /// <summary>The goodwill a god feels for another: the weight of the friendly acts it remembers.</summary>
    /// <param name="relation">The relation.</param>
    /// <returns>The goodwill (0 and up).</returns>
    public static float Goodwill(GodRelation relation)
    {
        ArgumentNullException.ThrowIfNull(relation);
        var goodwill = 0f;
        foreach (var memory in relation.Memories)
        {
            if (!IsHostile(memory.Act))
            {
                goodwill += memory.Weight;
            }
        }

        return goodwill;
    }
}
