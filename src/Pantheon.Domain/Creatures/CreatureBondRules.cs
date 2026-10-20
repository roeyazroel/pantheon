using Pantheon.Domain.Belief;

namespace Pantheon.Domain.Creatures;

/// <summary>
/// Data-driven tuning of how Creatures meet and regard each other (spec §27, ADR-017), part of
/// <see cref="CreatureRules"/>. Changes to a view are shares: a gain closes that share of the gap
/// to 1. Rates are per simulated second.
/// </summary>
public sealed record CreatureBondRules
{
    /// <summary>Distance in metres at which two Creatures meet.</summary>
    public required float MeetRadius { get; init; }

    /// <summary>How far from where its god points a rival Creature may be for a commanded fight to find it, in metres (ADR-062).</summary>
    public float HuntReach { get; init; } = 80f;

    /// <summary>How long a Creature hunts a rival its god set it on before it gives up, in simulated seconds (ADR-062).</summary>
    public float HuntSeconds { get; init; } = 90f;

    /// <summary>Seconds before the same two Creatures meet again.</summary>
    public required float MeetCooldownSeconds { get; init; }

    /// <summary>Distance in metres within which a Creature may go looking for another, to fight or play.</summary>
    public required float SeekRadius { get; init; }

    /// <summary>Distance in metres within which a Creature may run from another.</summary>
    public required float FleeRadius { get; init; }

    /// <summary>Trust in a Creature it has not met; trust drifts back towards it.</summary>
    public required float StartingTrust { get; init; }

    /// <summary>The urge to fight a Creature whose god is at war with its own.</summary>
    public required float WarFightDrive { get; init; }

    /// <summary>The urge to play with a Creature whose god is allied with its own.</summary>
    public required float AllianceBefriendDrive { get; init; }

    /// <summary>Friendship from which the other counts as a friend.</summary>
    public required float FriendThreshold { get; init; }

    /// <summary>Rivalry from which the other counts as a rival.</summary>
    public required float RivalThreshold { get; init; }

    /// <summary>Friendship gained by playing together.</summary>
    public required float FriendshipPerPlay { get; init; }

    /// <summary>Trust gained by playing together.</summary>
    public required float TrustPerPlay { get; init; }

    /// <summary>Friendship gained by a greeting.</summary>
    public required float FriendshipPerGreeting { get; init; }

    /// <summary>Rivalry gained by a fight.</summary>
    public required float RivalryPerFight { get; init; }

    /// <summary>Share of friendship a fight destroys.</summary>
    public required float FriendshipLossPerFight { get; init; }

    /// <summary>Share of trust lost when a friend attacks.</summary>
    public required float BetrayalTrustLoss { get; init; }

    /// <summary>Fear of the Creature that defeated it.</summary>
    public required float FearPerDefeat { get; init; }

    /// <summary>Respect for the Creature that defeated it.</summary>
    public required float RespectPerDefeat { get; init; }

    /// <summary>Respect gained for an opponent it beat, or one it could not beat.</summary>
    public required float RespectPerFight { get; init; }

    /// <summary>Fear of the Creature it fled from.</summary>
    public required float FearPerFlight { get; init; }

    /// <summary>Trust in the Creature that spared it.</summary>
    public required float TrustPerSpare { get; init; }

    /// <summary>Friendship for the Creature that spared it.</summary>
    public required float FriendshipPerSpare { get; init; }

    /// <summary>Rivalry with a Creature that attacked its god's people.</summary>
    public required float RivalryPerAttack { get; init; }

    /// <summary>Fear lost per second.</summary>
    public required float FearFadePerSecond { get; init; }

    /// <summary>Rivalry lost per second.</summary>
    public required float RivalryFadePerSecond { get; init; }

    /// <summary>Friendship lost per second.</summary>
    public required float FriendshipFadePerSecond { get; init; }

    /// <summary>Trust's drift per second back towards <see cref="StartingTrust"/>.</summary>
    public required float TrustDriftPerSecond { get; init; }

    /// <summary>Encounters each Creature remembers of each other Creature.</summary>
    public required int EncounterMemory { get; init; }
}

/// <summary>Data-driven tuning of fights between Creatures (spec §28, §111, ADR-017), part of <see cref="CreatureRules"/>.</summary>
public sealed record CreatureCombatRules
{
    /// <summary>Health each of two equal Creatures takes from the other per second.</summary>
    public required float DamagePerSecond { get; init; }

    /// <summary>Largest share by which a tick's blows are stronger or weaker than usual.</summary>
    public required float Jitter { get; init; }

    /// <summary>How much combat skill adds to power: power = strength × (1 + this × skill).</summary>
    public required float SkillPower { get; init; }

    /// <summary>Health at which a Creature gives up the fight and is beaten.</summary>
    public required float YieldHealth { get; init; }

    /// <summary>Share of the gap to 1 in combat skill that each fight closes.</summary>
    public required float SkillPerFight { get; init; }

    /// <summary>Experience for beating an opponent of its own level; scaled by the levels' ratio.</summary>
    public required float WinnerExperience { get; init; }

    /// <summary>Share of the experience gained towards the next level that a defeat costs (spec §28).</summary>
    public required float DefeatExperienceLoss { get; init; }

    /// <summary>Loyalty a defeat costs (spec §28).</summary>
    public required float LoyaltyPerDefeat { get; init; }

    /// <summary>Aggression a victory adds (spec §26: experience).</summary>
    public required float AggressionPerVictory { get; init; }

    /// <summary>Aggression a defeat removes (spec §26: trauma).</summary>
    public required float AggressionPerDefeat { get; init; }

    /// <summary>Share by which winning raises, and losing lowers twice over, its own liking for a fight.</summary>
    public required float FightLearningRate { get; init; }

    /// <summary>A winner whose mercy (kindness, friendship) reaches this lets the loser go instead of banishing it.</summary>
    public required float SpareThreshold { get; init; }

    /// <summary>Distance in metres within which settlements see a fight.</summary>
    public required float WitnessRadius { get; init; }

    /// <summary>How the winner's followers who see a fight change their belief in its god.</summary>
    public required BeliefImpulse VictoryWitness { get; init; }

    /// <summary>How the loser's followers who see a fight change their belief in its god.</summary>
    public required BeliefImpulse DefeatWitness { get; init; }
}
