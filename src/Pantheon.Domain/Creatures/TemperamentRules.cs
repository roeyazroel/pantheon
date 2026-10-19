namespace Pantheon.Domain.Creatures;

/// <summary>
/// How a Creature's courage, protectiveness, jealousy and playfulness act and evolve (spec §26,
/// ADR-037). <see cref="None"/> leaves them still and lets them change nothing but the drives.
/// </summary>
public sealed record TemperamentRules
{
    /// <summary>Inert: traits never move by experience, and none guards, defends or envies.</summary>
    public static TemperamentRules None { get; } = new()
    {
        GuardFight = 0f, GuardRadius = 0f, DefendDrive = 2f, EnvyRivalry = 0f, CouragePerVictory = 0f, CouragePerDefeat = 0f, CouragePerFlight = 0f,
        CouragePerWound = 0f, JealousyPerDefeat = 0f, JealousyPerReward = 0f, PlayfulnessPerPlay = 0f,
    };

    /// <summary>Added to the will to fight, and taken from the will to flee, at full protectiveness, when the other Creature stands near its god's people.</summary>
    public required float GuardFight { get; init; }

    /// <summary>Metres from a village its god leads within which a Creature guards it.</summary>
    public required float GuardRadius { get; init; }

    /// <summary>Protectiveness times courage from which a Creature goes by itself to fight the beast ravaging its god's village.</summary>
    public required float DefendDrive { get; init; }

    /// <summary>Rivalry gained, at full jealousy, on meeting a Creature stronger than itself.</summary>
    public required float EnvyRivalry { get; init; }

    /// <summary>Courage gained by winning a fight.</summary>
    public required float CouragePerVictory { get; init; }

    /// <summary>Courage lost by being beaten.</summary>
    public required float CouragePerDefeat { get; init; }

    /// <summary>Courage lost by running from another Creature.</summary>
    public required float CouragePerFlight { get; init; }

    /// <summary>Courage lost, at the full health lost, when the beast or a miracle wounds it.</summary>
    public required float CouragePerWound { get; init; }

    /// <summary>Jealousy gained by being beaten.</summary>
    public required float JealousyPerDefeat { get; init; }

    /// <summary>Jealousy a reward eases, and a punishment adds.</summary>
    public required float JealousyPerReward { get; init; }

    /// <summary>Playfulness gained by playing with another Creature.</summary>
    public required float PlayfulnessPerPlay { get; init; }
}
