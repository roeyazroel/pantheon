using System;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// The rules of a Creature's body, growth and learning as pure functions (spec §23–26, ADR-010):
/// strength and speed by level, levels by experience, needs over time, learning from rewards
/// and punishments, and the personality trait behind each action.
/// </summary>
public static class CreatureModel
{
    /// <summary>Highest level a Creature can reach in the MVP (spec §23).</summary>
    public const int MaxLevel = 10;

    /// <summary>Preference given to an action the Creature has no preference for yet.</summary>
    public const float UnknownPreference = 0.25f;

    /// <summary>Strength at the Creature's level.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>Strength.</returns>
    public static float Strength(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        return (rules.BaseStrength + (rules.StrengthPerLevel * (creature.Level - 1))) * SpeciesOf(rules, creature).Strength;
    }

    /// <summary>Whether the Creature's god holds it in its hand now (ADR-030): it does nothing until set down.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns><c>true</c> while it is held.</returns>
    public static bool IsHeld(WorldState world, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        return world.Gods.Find(g => g.Id == creature.GodId)?.Held?.Kind == Domain.Hand.HeldKind.Creature;
    }

    /// <summary>The Creature's species (ADR-021); the first species when its own is not listed, as a Creature from older content.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>The species.</returns>
    public static CreatureSpecies SpeciesOf(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return FindSpecies(rules, creature.Species) ?? rules.Species[0];
    }

    /// <summary>A species by name.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="name">Its name.</param>
    /// <returns>The species, or <c>null</c> when none has the name.</returns>
    public static CreatureSpecies? FindSpecies(CreatureRules rules, string? name)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var species in rules.Species)
        {
            if (string.Equals(species.Name, name, StringComparison.Ordinal))
            {
                return species;
            }
        }

        return null;
    }

    /// <summary>Walking speed at the Creature's level, in metres per second.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>Speed.</returns>
    public static float Speed(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        return (rules.BaseSpeed + (rules.SpeedPerLevel * (creature.Level - 1))) * SpeciesOf(rules, creature).Speed;
    }

    /// <summary>The level an amount of experience earns.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="experience">Experience.</param>
    /// <returns>From 1 to <see cref="MaxLevel"/>.</returns>
    public static int LevelFor(CreatureRules rules, float experience)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var level = 1;
        foreach (var needed in rules.LevelExperience)
        {
            if (experience >= needed)
            {
                level++;
            }
        }

        return Math.Min(level, MaxLevel);
    }

    /// <summary>The rule of an action.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="action">The action.</param>
    /// <returns>The rule.</returns>
    /// <exception cref="ArgumentException">The rules do not define the action (validation prevents it).</exception>
    public static CreatureActionRule RuleFor(CreatureRules rules, CreatureAction action)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules.Actions)
        {
            if (rule.Action == action)
            {
                return rule;
            }
        }

        throw new ArgumentException($"The creature rules define no action {action}.", nameof(action));
    }

    /// <summary>The Creature's learned preference for an action in a situation.</summary>
    /// <param name="creature">The Creature.</param>
    /// <param name="situation">The situation.</param>
    /// <param name="action">The action.</param>
    /// <returns>The preference, or <see cref="UnknownPreference"/> when it has none.</returns>
    public static float Preference(CreatureState creature, CreatureSituation situation, CreatureAction action) =>
        FindPreference(creature, situation, action)?.Weight ?? UnknownPreference;

    /// <summary>The Creature's preference entry for an action in a situation, if it has one.</summary>
    /// <param name="creature">The Creature.</param>
    /// <param name="situation">The situation.</param>
    /// <param name="action">The action.</param>
    /// <returns>The entry, or <c>null</c>.</returns>
    public static LearnedPreference? FindPreference(CreatureState creature, CreatureSituation situation, CreatureAction action)
    {
        ArgumentNullException.ThrowIfNull(creature);
        foreach (var preference in creature.Preferences)
        {
            if (preference.Situation == situation && preference.Action == action)
            {
                return preference;
            }
        }

        return null;
    }

    /// <summary>
    /// Learns from the god's feedback (spec §25): a reward closes <c>RewardLearningRate</c> of the gap
    /// to 1, a punishment removes <c>PunishmentLearningRate</c> of the preference.
    /// </summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature; a missing preference is added first.</param>
    /// <param name="situation">The situation of the judged action.</param>
    /// <param name="action">The judged action.</param>
    /// <param name="feedback">Reward or punishment.</param>
    /// <param name="boost">Extra share of learning, such as its god's Knowledge (ADR-021); the species' factor applies as well.</param>
    /// <returns>The new preference.</returns>
    public static float Learn(CreatureRules rules, CreatureState creature, CreatureSituation situation, CreatureAction action, CreatureFeedback feedback, float boost = 0f)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        var preference = FindPreference(creature, situation, action);
        if (preference is null)
        {
            preference = new LearnedPreference { Situation = situation, Action = action, Weight = UnknownPreference };
            creature.Preferences.Add(preference);
        }

        var learning = CreatureAttributes.Intelligence(rules, creature) * (1f + Math.Max(0f, boost));
        preference.Weight = feedback switch
        {
            CreatureFeedback.None => preference.Weight,
            CreatureFeedback.Rewarded => preference.Weight + (Math.Min(1f, rules.RewardLearningRate * learning) * (1f - preference.Weight)),
            CreatureFeedback.Punished => preference.Weight - (Math.Min(1f, rules.PunishmentLearningRate * learning) * preference.Weight),
        };
        preference.Weight = SimMath.Clamp01(preference.Weight);
        return preference.Weight;
    }

    /// <summary>Adds experience and raises the level it earns.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="experience">Experience to add; negative amounts add nothing.</param>
    /// <returns><c>true</c> when the Creature reached a new level.</returns>
    public static bool AddExperience(CreatureRules rules, CreatureState creature, float experience)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        creature.Experience += Math.Max(0f, experience);
        var level = LevelFor(rules, creature.Experience);
        if (level <= creature.Level)
        {
            return false;
        }

        creature.Level = level;
        return true;
    }

    /// <summary>Takes away a share of the experience gained since the current level began; a level is never lost (spec §28).</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="share">Share of that progress to lose (0..1).</param>
    public static void LoseProgress(CreatureRules rules, CreatureState creature, float share)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        var start = creature.Level >= 2 && creature.Level - 2 < rules.LevelExperience.Count ? rules.LevelExperience[creature.Level - 2] : 0f;
        var progress = Math.Max(0f, creature.Experience - start);
        creature.Experience -= SimMath.Clamp01(share) * progress;
    }

    /// <summary>Moves a preference by the Creature's own experience (spec §26), without its god: up towards 1 or down towards 0.</summary>
    /// <param name="creature">The Creature; a missing preference is added first.</param>
    /// <param name="situation">The situation.</param>
    /// <param name="action">The action.</param>
    /// <param name="change">Share of the gap to 1 to close (positive) or of the preference to remove (negative), from -1 to 1.</param>
    public static void Reinforce(CreatureState creature, CreatureSituation situation, CreatureAction action, float change)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var preference = FindPreference(creature, situation, action);
        if (preference is null)
        {
            preference = new LearnedPreference { Situation = situation, Action = action, Weight = UnknownPreference };
            creature.Preferences.Add(preference);
        }

        var step = Math.Clamp(change, -1f, 1f);
        preference.Weight = SimMath.Clamp01(step >= 0f ? preference.Weight + (step * (1f - preference.Weight)) : preference.Weight * (1f + step));
    }

    /// <summary>Adds a memory of a deed, forgetting the oldest beyond the capacity.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="tick">Tick at which it finished.</param>
    /// <param name="action">The action.</param>
    /// <param name="situation">The situation it acted in.</param>
    /// <param name="settlementId">The settlement it acted on, if any.</param>
    /// <param name="otherCreatureId">The Creature it acted towards, if any.</param>
    public static void Remember(CreatureRules rules, CreatureState creature, long tick, CreatureAction action, CreatureSituation situation, int? settlementId, int? otherCreatureId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        creature.Memories.Add(new CreatureMemory
        {
            Tick = tick,
            Action = action,
            Situation = situation,
            SettlementId = settlementId,
            OtherCreatureId = otherCreatureId,
            Feedback = CreatureFeedback.None,
        });
        if (creature.Memories.Count > rules.MemoryCapacity)
        {
            creature.Memories.RemoveRange(0, creature.Memories.Count - rules.MemoryCapacity);
        }
    }

    /// <summary>Lets the needs grow over time.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="needs">Needs to change.</param>
    /// <param name="seconds">Simulated seconds.</param>
    public static void GrowNeeds(CreatureRules rules, CreatureNeeds needs, float seconds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(needs);
        needs.Hunger = SimMath.Clamp01(needs.Hunger + (rules.HungerPerSecond * seconds));
        needs.Fatigue = SimMath.Clamp01(needs.Fatigue + (rules.FatiguePerSecond * seconds));
        needs.Boredom = SimMath.Clamp01(needs.Boredom + (rules.BoredomPerSecond * seconds));
    }

    /// <summary>Eases the needs as an action's rule says, times a share of the action that was done.</summary>
    /// <param name="rule">The action's rule.</param>
    /// <param name="needs">Needs to change.</param>
    /// <param name="share">Share of the action's relief that applies (0..1).</param>
    public static void Relieve(CreatureActionRule rule, CreatureNeeds needs, float share)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(needs);
        var s = SimMath.Clamp01(share);
        needs.Hunger = SimMath.Clamp01(needs.Hunger - (rule.HungerRelief * s));
        needs.Fatigue = SimMath.Clamp01(needs.Fatigue - (rule.FatigueRelief * s));
        needs.Boredom = SimMath.Clamp01(needs.Boredom - (rule.BoredomRelief * s));
    }
}
