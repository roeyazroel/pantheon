using System.Collections.Generic;
using Pantheon.Domain.Belief;

namespace Pantheon.Domain.Creatures;

/// <summary>
/// Data-driven tuning of Creatures, loaded from <c>game/Data/creatures.json</c> (ADR-010). Rates
/// are per simulated second; the Creature moves every tick and decides at most once every
/// <see cref="DecisionIntervalTicks"/> ticks.
/// </summary>
public sealed record CreatureRules
{
    /// <summary>Ticks between decisions (2 ticks: 5 Hz at ×1, spec §89).</summary>
    public required int DecisionIntervalTicks { get; init; }

    /// <summary>The kinds of Creature, the first being the one a god gets unless it chooses another (ADR-021).</summary>
    public required IReadOnlyList<CreatureSpecies> Species { get; init; }

    /// <summary>Walking speed at level 1, in metres per second.</summary>
    public required float BaseSpeed { get; init; }

    /// <summary>Extra speed per level above 1.</summary>
    public required float SpeedPerLevel { get; init; }

    /// <summary>Strength at level 1; it scales the effects of work and violence.</summary>
    public required float BaseStrength { get; init; }

    /// <summary>Extra strength per level above 1.</summary>
    public required float StrengthPerLevel { get; init; }

    /// <summary>Distance in metres within which the Creature notices settlements.</summary>
    public required float PerceptionRadius { get; init; }

    /// <summary>Distance in metres at which a target's appeal is halved.</summary>
    public required float DistanceHalfUtility { get; init; }

    /// <summary>Largest random addition to an option's utility, so near-ties do not always break the same way.</summary>
    public required float DecisionJitter { get; init; }

    /// <summary>Hunger gained per second.</summary>
    public required float HungerPerSecond { get; init; }

    /// <summary>Fatigue gained per second.</summary>
    public required float FatiguePerSecond { get; init; }

    /// <summary>Boredom gained per second.</summary>
    public required float BoredomPerSecond { get; init; }

    /// <summary>Hunger from which the Creature counts as hungry.</summary>
    public required float HungryThreshold { get; init; }

    /// <summary>Fatigue from which it counts as tired.</summary>
    public required float TiredThreshold { get; init; }

    /// <summary>Boredom from which it counts as bored.</summary>
    public required float BoredThreshold { get; init; }

    /// <summary>Health lost per second while starving (hunger at 1).</summary>
    public required float StarvationHealthLossPerSecond { get; init; }

    /// <summary>Health regained per second while resting.</summary>
    public required float RestHealthPerSecond { get; init; }

    /// <summary>Seconds a defeated Creature stays away (spec §28).</summary>
    public required float BanishedSeconds { get; init; }

    /// <summary>A settlement's food or health need from which the Creature sees it as in need.</summary>
    public required float VillageNeedThreshold { get; init; }

    /// <summary>Share of the gap to 1 that a reward closes in the rewarded preference.</summary>
    public required float RewardLearningRate { get; init; }

    /// <summary>Share of the punished preference that a punishment removes.</summary>
    public required float PunishmentLearningRate { get; init; }

    /// <summary>Step by which feedback moves the personality trait behind the action.</summary>
    public required float PersonalityNudge { get; init; }

    /// <summary>Loyalty of a new Creature.</summary>
    public required float StartingLoyalty { get; init; }

    /// <summary>Loyalty gained per reward.</summary>
    public required float LoyaltyPerReward { get; init; }

    /// <summary>Loyalty lost per punishment.</summary>
    public required float LoyaltyPerPunishment { get; init; }

    /// <summary>Seconds after an action during which a reward or punishment still refers to it.</summary>
    public required float FeedbackWindowSeconds { get; init; }

    /// <summary>Seconds a Creature waits where its god sent it, at full loyalty.</summary>
    public required float CommandStaySeconds { get; init; }

    /// <summary>Most memories kept.</summary>
    public required int MemoryCapacity { get; init; }

    /// <summary>Largest distance of a personality trait from 0.5 at birth.</summary>
    public required float PersonalitySpread { get; init; }

    /// <summary>Experience needed for each level from 2, in order (cumulative).</summary>
    public required IReadOnlyList<float> LevelExperience { get; init; }

    /// <summary>How each action plays out.</summary>
    public required IReadOnlyList<CreatureActionRule> Actions { get; init; }

    /// <summary>The preferences a new Creature is born with (spec §25's "initially").</summary>
    public required IReadOnlyList<PreferenceSeed> StartingPreferences { get; init; }

    /// <summary>How Creatures meet and regard each other (ADR-017).</summary>
    public required CreatureBondRules Bonds { get; init; }

    /// <summary>How Creatures fight (ADR-017).</summary>
    public required CreatureCombatRules Combat { get; init; }

    /// <summary>The paths Creatures take up and the attributes that grow with level (spec §23–24, ADR-030).</summary>
    public CallingRules Callings { get; init; } = CallingRules.None;
}

/// <summary>How one Creature action plays out.</summary>
public sealed record CreatureActionRule
{
    /// <summary>The action.</summary>
    public required CreatureAction Action { get; init; }

    /// <summary>Seconds it takes at the target.</summary>
    public required float Seconds { get; init; }

    /// <summary>
    /// Its size, times the Creature's strength where it does work: food given (Feed), food eaten
    /// from the stores (EatFromStores), builder-steps (HelpBuild), people killed (Attack), metres
    /// wandered (Wander) or run (Flee); unused otherwise. A fight's <see cref="Seconds"/> is its
    /// longest length.
    /// </summary>
    public required float Amount { get; init; }

    /// <summary>Experience gained when it is done.</summary>
    public required float Experience { get; init; }

    /// <summary>Hunger it eases.</summary>
    public required float HungerRelief { get; init; }

    /// <summary>Fatigue it eases (negative values tire).</summary>
    public required float FatigueRelief { get; init; }

    /// <summary>Boredom it eases.</summary>
    public required float BoredomRelief { get; init; }

    /// <summary>How kind (1) or cruel (-1) the deed is; moves the Creature's morality.</summary>
    public required float Kindness { get; init; }

    /// <summary>How the people of the settlement it acts on change their belief in its god.</summary>
    public required BeliefImpulse Witness { get; init; }
}

/// <summary>A preference a new Creature is born with.</summary>
public sealed record PreferenceSeed
{
    /// <summary>The situation.</summary>
    public required CreatureSituation Situation { get; init; }

    /// <summary>The action.</summary>
    public required CreatureAction Action { get; init; }

    /// <summary>The preference (0..1).</summary>
    public required float Weight { get; init; }
}
