using System;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Creatures;

/// <summary>A Creature personality trait (spec §26).</summary>
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

    /// <summary><see cref="CreaturePersonality.Courage"/>.</summary>
    Courage,

    /// <summary><see cref="CreaturePersonality.Protectiveness"/>.</summary>
    Protectiveness,

    /// <summary><see cref="CreaturePersonality.Jealousy"/>.</summary>
    Jealousy,

    /// <summary><see cref="CreaturePersonality.Playfulness"/>.</summary>
    Playfulness,
}

/// <summary>A trait behind an action, and whether the action draws on its lack.</summary>
/// <param name="Trait">The trait.</param>
/// <param name="Inverse">Whether the action draws on the trait's lack (running away on a lack of courage).</param>
public readonly record struct TraitDrive(CreatureTrait Trait, bool Inverse);

/// <summary>
/// How a Creature's personality drives what it does and how it evolves (spec §26, ADR-037):
/// <list type="bullet">
/// <item>the traits behind each action;</item>
/// <item>rewards and punishments nudging them;</item>
/// <item>guarding its god's people;</item>
/// <item>envying stronger Creatures;</item>
/// <item>defending its god's villages from the beast.</item>
/// </list>
/// </summary>
public static class CreatureTemperament
{
    private static readonly TraitDrive[] None = [];
    private static readonly TraitDrive[] Kind = [new(CreatureTrait.Kindness, false)];
    private static readonly TraitDrive[] Playful = [new(CreatureTrait.Kindness, false), new(CreatureTrait.Playfulness, false)];
    private static readonly TraitDrive[] Cruel = [new(CreatureTrait.Aggression, false)];
    private static readonly TraitDrive[] Bold = [new(CreatureTrait.Aggression, false), new(CreatureTrait.Courage, false)];
    private static readonly TraitDrive[] Timid = [new(CreatureTrait.Courage, true)];
    private static readonly TraitDrive[] Idle = [new(CreatureTrait.Laziness, false)];
    private static readonly TraitDrive[] Roaming = [new(CreatureTrait.Curiosity, false)];

    /// <summary>
    /// The traits behind an action. Feeding and building take kindness; play and friendship take
    /// kindness and playfulness; attacking and raiding take aggression; fighting takes aggression and
    /// courage; fleeing takes a lack of courage; resting laziness; roaming curiosity.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>Its traits; none for foraging and going where sent.</returns>
    public static ReadOnlySpan<TraitDrive> Traits(CreatureAction action) => action switch
    {
        CreatureAction.Feed or CreatureAction.HelpBuild => Kind,
        CreatureAction.Play or CreatureAction.Befriend => Playful,
        CreatureAction.Attack or CreatureAction.EatFromStores => Cruel,
        CreatureAction.Fight => Bold,
        CreatureAction.Flee => Timid,
        CreatureAction.Rest => Idle,
        CreatureAction.Wander or CreatureAction.Ignore => Roaming,
        CreatureAction.Forage or CreatureAction.GoTo => None,
    };

    /// <summary>How much the personality drives an action: 0.5 plus the mean of its traits (inverted where it draws on their lack); 1 with none.</summary>
    /// <param name="personality">The personality.</param>
    /// <param name="action">The action.</param>
    /// <returns>A factor from 0.5 to 1.5.</returns>
    public static float Drive(CreaturePersonality personality, CreatureAction action)
    {
        ArgumentNullException.ThrowIfNull(personality);
        var traits = Traits(action);
        if (traits.Length == 0)
        {
            return 1f;
        }

        var sum = 0f;
        foreach (var drive in traits)
        {
            var value = Get(personality, drive.Trait);
            sum += drive.Inverse ? 1f - value : value;
        }

        return 0.5f + (sum / traits.Length);
    }

    /// <summary>A trait's value.</summary>
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
            CreatureTrait.Courage => personality.Courage,
            CreatureTrait.Protectiveness => personality.Protectiveness,
            CreatureTrait.Jealousy => personality.Jealousy,
            CreatureTrait.Playfulness => personality.Playfulness,
        };
    }

    /// <summary>Moves a trait by a step, kept within 0..1.</summary>
    /// <param name="personality">Personality to change.</param>
    /// <param name="trait">Trait.</param>
    /// <param name="step">Change, up or down.</param>
    public static void Shift(CreaturePersonality personality, CreatureTrait trait, float step)
    {
        ArgumentNullException.ThrowIfNull(personality);
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
            case CreatureTrait.Courage:
                personality.Courage = value;
                break;
            case CreatureTrait.Protectiveness:
                personality.Protectiveness = value;
                break;
            case CreatureTrait.Jealousy:
                personality.Jealousy = value;
                break;
            case CreatureTrait.Playfulness:
                personality.Playfulness = value;
                break;
        }
    }

    /// <summary>
    /// What its god's judgement does to the personality (spec §26's reinforcement and divine
    /// treatment): the traits behind the judged action move with it (a rewarded flight makes it more
    /// cowardly), and a reward eases its jealousy while a punishment feeds it.
    /// </summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="personality">Personality to change.</param>
    /// <param name="action">The judged action.</param>
    /// <param name="feedback">Reward or punishment.</param>
    public static void Judge(CreatureRules rules, CreaturePersonality personality, CreatureAction action, CreatureFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(personality);
        if (feedback == CreatureFeedback.None)
        {
            return;
        }

        var rewarded = feedback == CreatureFeedback.Rewarded;
        foreach (var drive in Traits(action))
        {
            Shift(personality, drive.Trait, rewarded != drive.Inverse ? rules.PersonalityNudge : -rules.PersonalityNudge);
        }

        Shift(personality, CreatureTrait.Jealousy, rewarded ? -rules.Temperament.JealousyPerReward : rules.Temperament.JealousyPerReward);
    }

    /// <summary>How much a Creature guards a place: its protectiveness when the place lies within <c>GuardRadius</c> of a village its god leads, else 0.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="at">The place.</param>
    /// <returns>The guard (0..1).</returns>
    public static float Guard(CreatureRules rules, WorldState world, CreatureState creature, GroundPosition at)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        var reach = rules.Temperament.GuardRadius;
        if (reach <= 0f)
        {
            return 0f;
        }

        foreach (var settlement in world.Settlements)
        {
            var dx = settlement.Position.X - at.X;
            var dz = settlement.Position.Z - at.Z;
            if (settlement.Population > 0 && (dx * dx) + (dz * dz) <= reach * reach && Politics.TreatyModel.Leader(settlement) == creature.GodId)
            {
                return creature.Personality.Protectiveness;
            }
        }

        return 0f;
    }

    /// <summary>
    /// Envy on meeting (spec §26's jealous): a Creature that meets one stronger than itself grows
    /// that much more of a rival, by its jealousy; allies' Creatures are not envied.
    /// </summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature that may envy.</param>
    /// <param name="other">The one it meets.</param>
    public static void Envy(CreatureRules rules, WorldState world, CreatureState creature, CreatureState other)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(other);
        var envy = rules.Temperament.EnvyRivalry * creature.Personality.Jealousy;
        if (envy <= 0f || CreatureBonds.Power(rules, other) <= CreatureBonds.Power(rules, creature) || DiplomacyModel.StatusOf(world, creature.GodId, other.GodId) == DiplomaticStatus.Alliance)
        {
            return;
        }

        var view = CreatureBonds.Ensure(rules.Bonds, creature, other.Id);
        view.Rivalry = SimMath.Clamp01(view.Rivalry + envy);
    }

    /// <summary>Whether a Creature is protective and brave enough to go by itself against the beast that ravages its god's people.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns><c>true</c> when protectiveness times courage reaches <c>DefendDrive</c>.</returns>
    public static bool Defends(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        return creature.Personality.Protectiveness * creature.Personality.Courage >= rules.Temperament.DefendDrive;
    }

    /// <summary>What a wound does to its courage (spec §26's trauma): it falls by <c>CouragePerWound</c> times the health lost.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The wounded Creature.</param>
    /// <param name="healthLost">Health it lost (0..1).</param>
    public static void Wounded(CreatureRules rules, CreatureState creature, float healthLost)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        Shift(creature.Personality, CreatureTrait.Courage, -rules.Temperament.CouragePerWound * Math.Max(0f, healthLost));
    }
}
