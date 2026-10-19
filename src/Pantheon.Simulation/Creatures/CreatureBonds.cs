using System;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// How Creatures regard each other, as pure functions (spec §27, ADR-017): finding a view, the
/// urges to fight, play with or flee another Creature, what each kind of encounter does to the
/// views on both sides, and how views fade.
/// </summary>
public static class CreatureBonds
{
    // The urge to fight before anything else is weighed.
    private const float BaseFight = 0.05f;

    // The urge to play before anything else is weighed.
    private const float BaseBefriend = 0.2f;

    // The urge to walk on when another Creature is met.
    private const float IgnoreDrive = 0.3f;

    /// <summary>A Creature's view of another, if they have met.</summary>
    /// <param name="creature">The Creature whose view it is.</param>
    /// <param name="otherId">The Creature it regards.</param>
    /// <returns>The view, or <c>null</c>.</returns>
    public static CreatureRelation? Find(CreatureState creature, int otherId)
    {
        ArgumentNullException.ThrowIfNull(creature);
        foreach (var relation in creature.Relations)
        {
            if (relation.OtherCreatureId == otherId)
            {
                return relation;
            }
        }

        return null;
    }

    /// <summary>A Creature's view of another, created with the starting values when they have not met.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="creature">The Creature whose view it is.</param>
    /// <param name="otherId">The Creature it regards.</param>
    /// <returns>The view.</returns>
    public static CreatureRelation Ensure(CreatureBondRules rules, CreatureState creature, int otherId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (Find(creature, otherId) is { } found)
        {
            return found;
        }

        var relation = new CreatureRelation
        {
            OtherCreatureId = otherId,
            Friendship = 0f,
            Fear = 0f,
            Rivalry = 0f,
            Respect = 0f,
            Trust = rules.StartingTrust,
            LastMetTick = -1,
            Encounters = [],
        };
        creature.Relations.Add(relation);
        creature.Relations.Sort(static (a, b) => a.OtherCreatureId.CompareTo(b.OtherCreatureId));
        return relation;
    }

    /// <summary>Whether two Creatures may meet now: they never have, or their last meeting is long enough ago.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="relation">One side's view, if any.</param>
    /// <param name="tick">Current tick.</param>
    /// <returns><c>true</c> when they may meet.</returns>
    public static bool CanMeet(CreatureBondRules rules, CreatureRelation? relation, long tick)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return relation is null || relation.LastMetTick < 0 || tick - relation.LastMetTick >= (long)MathF.Round(rules.MeetCooldownSeconds * SimulationTime.TicksPerSecond);
    }

    /// <summary>Whether the view counts the other as a friend.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="relation">The view, if any.</param>
    /// <returns><c>true</c> for a friend.</returns>
    public static bool IsFriend(CreatureBondRules rules, CreatureRelation? relation) => relation is not null && relation.Friendship >= rules.FriendThreshold;

    /// <summary>Whether the view counts the other as a rival.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="relation">The view, if any.</param>
    /// <returns><c>true</c> for a rival.</returns>
    public static bool IsRival(CreatureBondRules rules, CreatureRelation? relation) => relation is not null && relation.Rivalry >= rules.RivalThreshold;

    /// <summary>The situation in which a Creature meets another: the Creature of a god at war with its own, or not.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="other">The one it meets.</param>
    /// <returns>The situation.</returns>
    public static CreatureSituation SituationFor(WorldState world, CreatureState creature, CreatureState other)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(other);
        return DiplomacyModel.AreAtWar(world, creature.GodId, other.GodId) ? CreatureSituation.MeetsEnemyCreature : CreatureSituation.MeetsCreature;
    }

    /// <summary>A Creature's fighting power: strength, raised by combat skill, and by the Warrior's path (ADR-030).</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>Power.</returns>
    public static float Power(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        return CreatureModel.Strength(rules, creature) * (1f + (rules.Combat.SkillPower * creature.CombatSkill)) * CallingEffects.PowerFactor(rules.Callings, creature);
    }

    /// <summary>How sure a Creature is of beating another: the ratio of their power and health, from 0.25 to 1 (being stronger adds no wish to fight).</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="other">Its opponent.</param>
    /// <returns>Confidence.</returns>
    public static float Confidence(CreatureRules rules, CreatureState creature, CreatureState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var mine = Power(rules, creature) * Math.Max(creature.Health, 0.05f);
        var theirs = Power(rules, other) * Math.Max(other.Health, 0.05f);
        return Math.Clamp(mine / theirs, 0.25f, 1f);
    }

    /// <summary>The urges a Creature feels towards another it meets or sees.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="other">The other Creature.</param>
    /// <returns>The drives to fight, play, flee and walk on, each at least 0.</returns>
    public static SocialDrives Drives(CreatureRules rules, WorldState world, CreatureState creature, CreatureState other)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(other);
        var bonds = rules.Bonds;
        var view = Find(creature, other.Id);
        var friendship = view?.Friendship ?? 0f;
        var fear = view?.Fear ?? 0f;
        var rivalry = view?.Rivalry ?? 0f;
        var trust = view?.Trust ?? bonds.StartingTrust;
        var status = DiplomacyModel.StatusOf(world, creature.GodId, other.GodId);
        var war = status switch
        {
            DiplomaticStatus.War => bonds.WarFightDrive,
            DiplomaticStatus.Hostile => 0.5f * bonds.WarFightDrive,
            DiplomaticStatus.Neutral or DiplomaticStatus.Friendly or DiplomaticStatus.Alliance => 0f,
        };
        var ally = status == DiplomaticStatus.Alliance ? bonds.AllianceBefriendDrive : 0f;
        var confidence = Confidence(rules, creature, other);
        var fight = (BaseFight + (0.5f * rivalry) + war - (0.8f * friendship) - (0.6f * fear)) * confidence;
        var befriend = BaseBefriend + (0.5f * friendship) + (0.3f * trust) + ally + (0.2f * creature.Needs.Boredom) - (0.6f * rivalry) - war - (0.3f * fear);
        var threatened = war > 0f || rivalry > 0f || fear > 0f;
        var flee = threatened ? (0.8f * fear) + (0.5f * ((1f / confidence) - 1f)) - 0.2f : 0f;
        return new SocialDrives(Math.Max(0f, fight), Math.Max(0f, befriend), Math.Max(0f, flee), IgnoreDrive);
    }

    /// <summary>They played together: friendship and trust grow on this side.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="creature">The Creature whose view changes.</param>
    /// <param name="otherId">Its playmate.</param>
    /// <param name="tick">Current tick.</param>
    public static void Played(CreatureBondRules rules, CreatureState creature, int otherId, long tick)
    {
        var view = Ensure(rules, creature, otherId);
        view.Friendship = Gain(view.Friendship, rules.FriendshipPerPlay);
        view.Trust = Gain(view.Trust, rules.TrustPerPlay);
        Note(rules, view, tick, EncounterKind.Played);
    }

    /// <summary>One greeted the other: a little friendship on both sides.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="a">One Creature.</param>
    /// <param name="b">The other.</param>
    /// <param name="tick">Current tick.</param>
    public static void Greeted(CreatureBondRules rules, CreatureState a, CreatureState b, long tick)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        foreach (var (self, other) in new[] { (a, b), (b, a) })
        {
            var view = Ensure(rules, self, other.Id);
            view.Friendship = Gain(view.Friendship, rules.FriendshipPerGreeting);
            Note(rules, view, tick, EncounterKind.Greeted);
        }
    }

    /// <summary>They met and walked on: remembered only the first time.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="a">One Creature.</param>
    /// <param name="b">The other.</param>
    /// <param name="tick">Current tick.</param>
    public static void PassedBy(CreatureBondRules rules, CreatureState a, CreatureState b, long tick)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        foreach (var (self, other) in new[] { (a, b), (b, a) })
        {
            var view = Ensure(rules, self, other.Id);
            if (view.Encounters.Count == 0)
            {
                Note(rules, view, tick, EncounterKind.Met);
            }
        }
    }

    /// <summary>A fight starts: rivalry grows and friendship breaks on both sides; a friend attacked feels betrayed.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="attacker">The Creature that attacked.</param>
    /// <param name="defender">The Creature attacked.</param>
    /// <param name="betrayal">Whether the defender counted the attacker as a friend.</param>
    /// <param name="tick">Current tick.</param>
    public static void FightStarted(CreatureBondRules rules, CreatureState attacker, CreatureState defender, bool betrayal, long tick)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        foreach (var (self, other) in new[] { (attacker, defender), (defender, attacker) })
        {
            var view = Ensure(rules, self, other.Id);
            view.Rivalry = Gain(view.Rivalry, rules.RivalryPerFight);
            view.Friendship = Lose(view.Friendship, rules.FriendshipLossPerFight);
        }

        if (betrayal)
        {
            var view = Ensure(rules, defender, attacker.Id);
            view.Trust = Lose(view.Trust, rules.BetrayalTrustLoss);
            Note(rules, view, tick, EncounterKind.Betrayed);
        }
    }

    /// <summary>A fight ended with a winner: the loser fears and respects it (a spared loser trusts and likes it instead of fearing it); the winner's rivalry halves.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="winner">The winner.</param>
    /// <param name="loser">The loser.</param>
    /// <param name="spared">Whether the winner let the loser go.</param>
    /// <param name="tick">Current tick.</param>
    public static void Beaten(CreatureBondRules rules, CreatureState winner, CreatureState loser, bool spared, long tick)
    {
        ArgumentNullException.ThrowIfNull(winner);
        ArgumentNullException.ThrowIfNull(loser);
        var loserView = Ensure(rules, loser, winner.Id);
        loserView.Respect = Gain(loserView.Respect, rules.RespectPerDefeat);
        // The winner has settled the score: its rivalry halves, and it fears the loser less.
        var winnerView = Ensure(rules, winner, loser.Id);
        winnerView.Respect = Gain(winnerView.Respect, rules.RespectPerFight);
        winnerView.Fear = Lose(winnerView.Fear, rules.FearPerDefeat);
        winnerView.Rivalry = Lose(winnerView.Rivalry, 0.5f);
        if (spared)
        {
            loserView.Trust = Gain(loserView.Trust, rules.TrustPerSpare);
            loserView.Friendship = Gain(loserView.Friendship, rules.FriendshipPerSpare);
            Note(rules, loserView, tick, EncounterKind.WasSpared);
            Note(rules, winnerView, tick, EncounterKind.Spared);
        }
        else
        {
            loserView.Fear = Gain(loserView.Fear, rules.FearPerDefeat);
            Note(rules, loserView, tick, EncounterKind.Lost);
            Note(rules, winnerView, tick, EncounterKind.Won);
        }
    }

    /// <summary>A fight ended with neither able to win: each respects the other more.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="a">One Creature.</param>
    /// <param name="b">The other.</param>
    /// <param name="tick">Current tick.</param>
    public static void Drew(CreatureBondRules rules, CreatureState a, CreatureState b, long tick)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        foreach (var (self, other) in new[] { (a, b), (b, a) })
        {
            var view = Ensure(rules, self, other.Id);
            view.Respect = Gain(view.Respect, rules.RespectPerFight);
            Note(rules, view, tick, EncounterKind.Drew);
        }
    }

    /// <summary>One fled from the other: it fears the other more; the other remembers chasing it off.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="fleeing">The Creature that fled.</param>
    /// <param name="from">The Creature it fled from.</param>
    /// <param name="tick">Current tick.</param>
    public static void Fled(CreatureBondRules rules, CreatureState fleeing, CreatureState from, long tick)
    {
        ArgumentNullException.ThrowIfNull(fleeing);
        ArgumentNullException.ThrowIfNull(from);
        var view = Ensure(rules, fleeing, from.Id);
        view.Fear = Gain(view.Fear, rules.FearPerFlight);
        Note(rules, view, tick, EncounterKind.Fled);
        Note(rules, Ensure(rules, from, fleeing.Id), tick, EncounterKind.ChasedOff);
    }

    /// <summary>Another Creature attacked this Creature's god's people: rivalry grows.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="victim">The Creature whose god's people were attacked.</param>
    /// <param name="attackerId">The attacking Creature.</param>
    /// <param name="tick">Current tick.</param>
    public static void AttackedOurPeople(CreatureBondRules rules, CreatureState victim, int attackerId, long tick)
    {
        var view = Ensure(rules, victim, attackerId);
        view.Rivalry = Gain(view.Rivalry, rules.RivalryPerAttack);
        if (view.Encounters.Count > 0 && view.Encounters[^1].Kind == EncounterKind.AttackedOurPeople)
        {
            view.Encounters.RemoveAt(view.Encounters.Count - 1);
        }

        Note(rules, view, tick, EncounterKind.AttackedOurPeople);
    }

    /// <summary>Lets fear, rivalry and friendship fade and trust drift back to where it starts.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="creature">The Creature whose views fade.</param>
    /// <param name="seconds">Simulated seconds.</param>
    public static void Fade(CreatureBondRules rules, CreatureState creature, float seconds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        foreach (var view in creature.Relations)
        {
            view.Fear = SimMath.Clamp01(view.Fear - (rules.FearFadePerSecond * seconds));
            view.Rivalry = SimMath.Clamp01(view.Rivalry - (rules.RivalryFadePerSecond * seconds));
            view.Friendship = SimMath.Clamp01(view.Friendship - (rules.FriendshipFadePerSecond * seconds));
            var drift = rules.TrustDriftPerSecond * seconds;
            view.Trust = view.Trust < rules.StartingTrust
                ? Math.Min(rules.StartingTrust, view.Trust + drift)
                : Math.Max(rules.StartingTrust, view.Trust - drift);
        }
    }

    /// <summary>Closes a share of the gap to 1.</summary>
    private static float Gain(float value, float share) => SimMath.Clamp01(value + (share * (1f - value)));

    /// <summary>Removes a share of the value.</summary>
    private static float Lose(float value, float share) => SimMath.Clamp01(value * (1f - share));

    /// <summary>Remembers an encounter, forgetting the oldest beyond the capacity.</summary>
    private static void Note(CreatureBondRules rules, CreatureRelation view, long tick, EncounterKind kind)
    {
        view.Encounters.Add(new CreatureEncounter { Tick = tick, Kind = kind });
        if (view.Encounters.Count > rules.EncounterMemory)
        {
            view.Encounters.RemoveRange(0, view.Encounters.Count - rules.EncounterMemory);
        }
    }
}

/// <summary>The urges a Creature feels towards another (ADR-017), each at least 0.</summary>
/// <param name="Fight">To fight it.</param>
/// <param name="Befriend">To play with it.</param>
/// <param name="Flee">To run from it.</param>
/// <param name="Ignore">To walk on.</param>
public readonly record struct SocialDrives(float Fight, float Befriend, float Flee, float Ignore);
