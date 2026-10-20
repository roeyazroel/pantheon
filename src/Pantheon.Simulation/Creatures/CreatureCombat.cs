using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// Fights between Creatures (spec §28, §111, ADR-017). Each tick two fighters trade blows by their
/// power (strength raised by combat skill), with jitter from a stream derived from the seed and the
/// tick. The first worn down to <c>YieldHealth</c> is beaten: a merciful winner lets it go;
/// otherwise it is banished, scarred, less loyal and set back in experience, and the people who
/// saw it think less of its god. A fight that runs its course is a draw; one that a fighter leaves
/// is broken off.
/// </summary>
public sealed class CreatureCombat
{
    /// <summary>Purpose of the derived random stream for blows.</summary>
    public const string RandomStream = "creature.combat";

    // Morality moves by this share of a deed's kindness, as for other deeds.
    private const float MoralityStep = 0.05f;

    // A winner at war with the loser's god shows this much less mercy.
    private const float WarMercyPenalty = 0.3f;

    private readonly CreatureRules _rules;
    private readonly IActRecorder? _ledger;
    private readonly Domain.Gods.DomainRules? _domains;

    /// <summary>Creates the combat over validated rules.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="ledger">Where a Creature's defeat is recorded for its god; <c>null</c> for none.</param>
    /// <param name="domains">Domain rules, for the War domain's harder blows (ADR-021); <c>null</c> for none.</param>
    public CreatureCombat(CreatureRules rules, IActRecorder? ledger = null, Domain.Gods.DomainRules? domains = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
        _ledger = ledger;
        _domains = domains;
    }

    /// <summary>Plays one tick of every fight, each pair once, in id order.</summary>
    /// <param name="world">World state.</param>
    /// <param name="events">Receives <see cref="CreatureFightEnded"/> and new levels.</param>
    public void Update(WorldState world, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        SimulationRandom? random = null;
        foreach (var creature in world.Creatures)
        {
            if (!IsFighting(creature))
            {
                continue;
            }

            var other = world.Creatures.Find(c => c.Id == creature.Activity.TargetCreatureId);
            if (other is null || !IsFighting(other) || other.Activity.TargetCreatureId != creature.Id)
            {
                BreakOff(world, creature, other, events);
                continue;
            }

            if (creature.Id < other.Id)
            {
                random ??= new SimulationRandom(SeedMixer.Derive(world.Seed, RandomStream, world.Tick));
                Exchange(world, creature, other, random, events);
            }
        }
    }

    /// <summary>Whether a Creature is in a fight now.</summary>
    /// <param name="creature">The Creature.</param>
    /// <returns><c>true</c> while it fights.</returns>
    public static bool IsFighting(CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var activity = creature.Activity;
        return activity.Phase == CreaturePhase.Performing && activity.Action == CreatureAction.Fight && activity.TargetCreatureId is not null;
    }

    /// <summary>
    /// Whether a Creature is getting its breath back after a fight (ADR-070): for <c>RespiteSeconds</c>
    /// after a fight it fought or saw off, it is not drawn into another.
    /// </summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="tick">The tick now.</param>
    /// <returns><c>true</c> while it recovers.</returns>
    public static bool Recovering(CreatureRules rules, CreatureState creature, long tick)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        var respite = (long)(rules.Combat.RespiteSeconds / SimulationTime.SecondsPerTick);
        return respite > 0 && creature.Memories.Exists(m => m.Action == CreatureAction.Fight && tick - m.Tick < respite && m.Tick <= tick);
    }

    /// <summary>One tick of blows, then the end of the fight if one is beaten or time is up.</summary>
    private void Exchange(WorldState world, CreatureState a, CreatureState b, SimulationRandom random, List<WorldEvent> events)
    {
        var combat = _rules.Combat;
        var powerA = CreatureBonds.Power(_rules, a);
        var powerB = CreatureBonds.Power(_rules, b);
        var blow = combat.DamagePerSecond * SimulationTime.SecondsPerTick * 2f / (powerA + powerB);
        b.Health = SimMath.Clamp01(b.Health - (blow * powerA * Fury(world, a) * Jitter(random) / CreatureAttributes.Resilience(_rules, b)));
        a.Health = SimMath.Clamp01(a.Health - (blow * powerB * Fury(world, b) * Jitter(random) / CreatureAttributes.Resilience(_rules, a)));
        a.Activity.TicksLeft--;
        b.Activity.TicksLeft--;
        var aDown = a.Health <= combat.YieldHealth;
        var bDown = b.Health <= combat.YieldHealth;
        if (aDown || bDown)
        {
            // When both fall in the same tick, the one with less health left is beaten; a tie goes to the lower id.
            var aLoses = aDown && (!bDown || a.Health < b.Health);
            Beat(world, aLoses ? b : a, aLoses ? a : b, events);
        }
        else if (a.Activity.TicksLeft <= 0 || b.Activity.TicksLeft <= 0)
        {
            Draw(world, a, b, events);
        }
    }

    /// <summary>How much harder a Creature strikes for its god's War domain (ADR-021): 1 without it.</summary>
    private float Fury(WorldState world, CreatureState creature) =>
        _domains is not null && Domains.DomainModel.Holds(world, creature.GodId, Domain.Gods.GodDomain.War) ? 1f + _domains.Boons.WarCreatureDamage : 1f;

    /// <summary>A share of usual strength for one blow: 1 ± <c>Jitter</c>.</summary>
    private float Jitter(SimulationRandom random) => 1f + (_rules.Combat.Jitter * ((2f * random.NextFloat()) - 1f));

    /// <summary>The fight has a winner: it lets the loser go if merciful enough, or the loser is banished.</summary>
    private void Beat(WorldState world, CreatureState winner, CreatureState loser, List<WorldEvent> events)
    {
        var combat = _rules.Combat;
        var tick = world.Tick;
        var spared = Mercy(world, winner, loser) >= combat.SpareThreshold;
        Finish(world, winner, loser);
        CreatureBonds.Beaten(_rules.Bonds, winner, loser, spared, tick);
        var ratio = Math.Clamp(loser.Level / (float)winner.Level, 0.25f, 2f);
        Gain(winner, FightRule.Experience + (combat.WinnerExperience * ratio), tick, events);
        winner.Personality.Aggression = SimMath.Clamp01(winner.Personality.Aggression + combat.AggressionPerVictory);
        CreatureTemperament.Shift(winner.Personality, CreatureTrait.Courage, _rules.Temperament.CouragePerVictory);
        CreatureCalling.Add(winner.Callings, Domain.Creatures.CreatureSpecialisation.Warrior, _rules.Callings.VictoryCalling);
        CreatureModel.Reinforce(winner, winner.Activity.Situation, CreatureAction.Fight, combat.FightLearningRate);
        Witness(world, winner, loser);
        if (spared)
        {
            winner.Morality = Math.Clamp(winner.Morality + MoralityStep, -1f, 1f);
            CreatureCalling.Add(winner.Callings, Domain.Creatures.CreatureSpecialisation.Guardian, _rules.Callings.MercyCalling);
            Gain(loser, FightRule.Experience * 0.5f, tick, events);
        }
        else
        {
            Defeat(world, winner, loser);
        }

        Stop(winner);
        if (spared)
        {
            Stop(loser);
        }

        events.Add(new CreatureFightEnded { Tick = tick, CreatureId = winner.Id, OtherCreatureId = loser.Id, Outcome = spared ? FightOutcome.Spared : FightOutcome.Banished });
    }

    /// <summary>
    /// What a defeat costs the loser (spec §28): banishment (temporary absence), a scar, loyalty,
    /// experience towards its next level, some of its aggression and liking for a fight; and its
    /// god remembers it.
    /// </summary>
    private void Defeat(WorldState world, CreatureState winner, CreatureState loser)
    {
        var combat = _rules.Combat;
        CreatureBanishment.Banish(_rules, loser);
        loser.Scars++;
        loser.Loyalty = SimMath.Clamp01(loser.Loyalty - combat.LoyaltyPerDefeat);
        CreatureModel.LoseProgress(_rules, loser, combat.DefeatExperienceLoss);
        loser.Personality.Aggression = SimMath.Clamp01(loser.Personality.Aggression - combat.AggressionPerDefeat);
        CreatureTemperament.Shift(loser.Personality, CreatureTrait.Courage, -_rules.Temperament.CouragePerDefeat);
        CreatureTemperament.Shift(loser.Personality, CreatureTrait.Jealousy, _rules.Temperament.JealousyPerDefeat);
        CreatureModel.Reinforce(loser, loser.Activity.Situation, CreatureAction.Fight, -2f * combat.FightLearningRate);
        if (winner.GodId != loser.GodId)
        {
            _ledger?.Record(world, loser.GodId, winner.GodId, DiplomaticAct.CreatureDefeated, null);
        }
    }

    /// <summary>Time ran out with neither beaten.</summary>
    private void Draw(WorldState world, CreatureState a, CreatureState b, List<WorldEvent> events)
    {
        Finish(world, a, b);
        CreatureBonds.Drew(_rules.Bonds, a, b, world.Tick);
        Gain(a, FightRule.Experience, world.Tick, events);
        Gain(b, FightRule.Experience, world.Tick, events);
        Stop(a);
        Stop(b);
        events.Add(new CreatureFightEnded { Tick = world.Tick, CreatureId = a.Id, OtherCreatureId = b.Id, Outcome = FightOutcome.Drew });
    }

    /// <summary>The opponent left the fight (punished away, starved, or gone): the one who stayed has chased it off.</summary>
    private void BreakOff(WorldState world, CreatureState stayer, CreatureState? leaver, List<WorldEvent> events)
    {
        CreatureModel.Remember(_rules, stayer, world.Tick, CreatureAction.Fight, stayer.Activity.Situation, null, leaver?.Id);
        CreatureModel.Relieve(FightRule, stayer.Needs, 0.5f);
        Stop(stayer);
        if (leaver is null)
        {
            return;
        }

        CreatureBonds.Fled(_rules.Bonds, leaver, stayer, world.Tick);
        CreatureTemperament.Shift(leaver.Personality, CreatureTrait.Courage, -_rules.Temperament.CouragePerFlight);
        events.Add(new CreatureFightEnded { Tick = world.Tick, CreatureId = stayer.Id, OtherCreatureId = leaver.Id, Outcome = FightOutcome.BrokeOff });
    }

    /// <summary>What every finished fight gives both fighters: combat skill, eased boredom, a cruel deed, and a memory.</summary>
    private void Finish(WorldState world, CreatureState a, CreatureState b)
    {
        foreach (var (self, other) in new[] { (a, b), (b, a) })
        {
            self.CombatSkill = SimMath.Clamp01(self.CombatSkill + (_rules.Combat.SkillPerFight * (1f - self.CombatSkill)));
            CreatureModel.Relieve(FightRule, self.Needs, 1f);
            self.Morality = Math.Clamp(self.Morality + (FightRule.Kindness * MoralityStep), -1f, 1f);
            CreatureModel.Remember(_rules, self, world.Tick, CreatureAction.Fight, self.Activity.Situation, null, other.Id);
        }
    }

    /// <summary>
    /// How merciful the winner is towards the loser: kindness tempered by aggression, plus friendship,
    /// less when their gods are at war, and less for a rival it is jealous of (ADR-037).
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="winner">The winner.</param>
    /// <param name="loser">The loser.</param>
    /// <returns>Mercy; at <c>SpareThreshold</c> or more the loser is let go.</returns>
    public static float Mercy(WorldState world, CreatureState winner, CreatureState loser)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(winner);
        ArgumentNullException.ThrowIfNull(loser);
        var traits = winner.Personality;
        var view = CreatureBonds.Find(winner, loser.Id);
        var war = DiplomacyModel.AreAtWar(world, winner.GodId, loser.GodId) ? WarMercyPenalty : 0f;
        return (traits.Kindness * (1f - traits.Aggression)) + (view?.Friendship ?? 0f) - war - (0.5f * traits.Jealousy * (view?.Rivalry ?? 0f));
    }

    /// <summary>
    /// Reputation (spec §28): followers of the winner's god who see the fight think more of their
    /// god and fear it; followers of the loser's god doubt theirs.
    /// </summary>
    private void Witness(WorldState world, CreatureState winner, CreatureState loser)
    {
        var combat = _rules.Combat;
        var x = (winner.Position.X + loser.Position.X) / 2f;
        var z = (winner.Position.Z + loser.Position.Z) / 2f;
        foreach (var settlement in world.Settlements)
        {
            var dx = settlement.Position.X - x;
            var dz = settlement.Position.Z - z;
            if ((dx * dx) + (dz * dz) > combat.WitnessRadius * combat.WitnessRadius || settlement.Population <= 0)
            {
                continue;
            }

            if (BeliefModel.Find(settlement, winner.GodId) is { } proud)
            {
                BeliefModel.ApplyImpulse(proud, combat.VictoryWitness, 1f, 1f);
            }

            if (BeliefModel.Find(settlement, loser.GodId) is { } shaken)
            {
                BeliefModel.ApplyImpulse(shaken, combat.DefeatWitness, 1f, 1f);
            }
        }
    }

    /// <summary>Adds experience and reports a new level.</summary>
    private void Gain(CreatureState creature, float experience, long tick, List<WorldEvent> events)
    {
        if (CreatureModel.AddExperience(_rules, creature, experience))
        {
            events.Add(new CreatureChanged { Tick = tick, CreatureId = creature.Id, Change = CreatureChange.LevelledUp });
        }
    }

    /// <summary>The Creature leaves the fight and thinks what to do next.</summary>
    private static void Stop(CreatureState creature)
    {
        var activity = creature.Activity;
        if (activity.Phase == CreaturePhase.Banished)
        {
            return;
        }

        activity.Phase = CreaturePhase.Deciding;
        activity.TicksLeft = 0;
        activity.Path.Clear();
    }

    /// <summary>The fight's rule.</summary>
    private CreatureActionRule FightRule => CreatureModel.RuleFor(_rules, CreatureAction.Fight);
}
