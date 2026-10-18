using System;
using Pantheon.Domain.Creatures;

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
        return rules.BaseStrength + (rules.StrengthPerLevel * (creature.Level - 1));
    }

    /// <summary>Walking speed at the Creature's level, in metres per second.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>Speed.</returns>
    public static float Speed(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        return rules.BaseSpeed + (rules.SpeedPerLevel * (creature.Level - 1));
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
    /// <returns>The new preference.</returns>
    public static float Learn(CreatureRules rules, CreatureState creature, CreatureSituation situation, CreatureAction action, CreatureFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        var preference = FindPreference(creature, situation, action);
        if (preference is null)
        {
            preference = new LearnedPreference { Situation = situation, Action = action, Weight = UnknownPreference };
            creature.Preferences.Add(preference);
        }

        preference.Weight = feedback switch
        {
            CreatureFeedback.None => preference.Weight,
            CreatureFeedback.Rewarded => preference.Weight + (rules.RewardLearningRate * (1f - preference.Weight)),
            CreatureFeedback.Punished => preference.Weight - (rules.PunishmentLearningRate * preference.Weight),
        };
        preference.Weight = SimMath.Clamp01(preference.Weight);
        return preference.Weight;
    }

    /// <summary>The personality trait that drives an action, if any (kind acts: Kindness; cruel acts: Aggression; rest: Laziness; roaming: Curiosity).</summary>
    /// <param name="action">The action.</param>
    /// <returns>The trait, or <c>null</c> when no trait drives it.</returns>
    public static CreatureTrait? TraitOf(CreatureAction action) => action switch
    {
        CreatureAction.Feed or CreatureAction.HelpBuild or CreatureAction.Play => CreatureTrait.Kindness,
        CreatureAction.Attack or CreatureAction.EatFromStores => CreatureTrait.Aggression,
        CreatureAction.Rest => CreatureTrait.Laziness,
        CreatureAction.Wander or CreatureAction.Ignore => CreatureTrait.Curiosity,
        CreatureAction.Forage or CreatureAction.GoTo => null,
    };

    /// <summary>A personality trait's value.</summary>
    /// <param name="personality">Personality.</param>
    /// <param name="trait">Trait.</param>
    /// <returns>The value (0..1).</returns>
    public static float Get(CreaturePersonality personality, CreatureTrait trait)
    {
        ArgumentNullException.ThrowIfNull(personality);
        return trait switch
        {
            CreatureTrait.Kindness => personality.Kindness,
            CreatureTrait.Aggression => personality.Aggression,
            CreatureTrait.Laziness => personality.Laziness,
            CreatureTrait.Curiosity => personality.Curiosity,
        };
    }

    /// <summary>Moves the trait behind a judged action: up when rewarded, down when punished (spec §26).</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="personality">Personality to change.</param>
    /// <param name="action">The judged action.</param>
    /// <param name="feedback">Reward or punishment.</param>
    public static void Nudge(CreatureRules rules, CreaturePersonality personality, CreatureAction action, CreatureFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(personality);
        if (TraitOf(action) is not { } trait || feedback == CreatureFeedback.None)
        {
            return;
        }

        var step = feedback == CreatureFeedback.Rewarded ? rules.PersonalityNudge : -rules.PersonalityNudge;
        var value = SimMath.Clamp01(Get(personality, trait) + step);
        switch (trait)
        {
            case CreatureTrait.Kindness:
                personality.Kindness = value;
                break;
            case CreatureTrait.Aggression:
                personality.Aggression = value;
                break;
            case CreatureTrait.Laziness:
                personality.Laziness = value;
                break;
            case CreatureTrait.Curiosity:
                personality.Curiosity = value;
                break;
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

/// <summary>A Creature personality trait.</summary>
public enum CreatureTrait
{
    /// <summary><see cref="CreaturePersonality.Kindness"/>.</summary>
    Kindness,

    /// <summary><see cref="CreaturePersonality.Aggression"/>.</summary>
    Aggression,

    /// <summary><see cref="CreaturePersonality.Laziness"/>.</summary>
    Laziness,

    /// <summary><see cref="CreaturePersonality.Curiosity"/>.</summary>
    Curiosity,
}
