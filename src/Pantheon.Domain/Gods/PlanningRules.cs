namespace Pantheon.Domain.Gods;

/// <summary>How AI gods plan (spec §87's major planning, ADR-036). <see cref="None"/> plans nothing.</summary>
public sealed record PlanningRules
{
    /// <summary>No planning.</summary>
    public static PlanningRules None { get; } = new()
    {
        PlanSeconds = 0f, MaxGoals = 0, MinGoalSeconds = 0f, Threshold = 1f, Boost = 1f, DiplomacyBias = 0f, HomeDanger = 1f, ConvertReach = 0f, Doubt = 1f, Dryness = 1f,
    };

    /// <summary>Whether gods plan at all.</summary>
    public bool Enabled => PlanSeconds > 0f && MaxGoals > 0;

    /// <summary>Seconds between a god's major plans.</summary>
    public required float PlanSeconds { get; init; }

    /// <summary>The most goals a god holds at once.</summary>
    public required int MaxGoals { get; init; }

    /// <summary>Seconds a goal is held at least, unless it is done or moot.</summary>
    public required float MinGoalSeconds { get; init; }

    /// <summary>The least weight for a goal to be taken up.</summary>
    public required float Threshold { get; init; }

    /// <summary>The factor on the utility of an action that serves a goal.</summary>
    public required float Boost { get; init; }

    /// <summary>What a goal adds, at full weight, to the utility of an alliance with a god it means to befriend or a war on one it means to weaken (and takes from the opposite).</summary>
    public required float DiplomacyBias { get; init; }

    /// <summary>The danger at its home (0..1) from which a god sets out to protect it.</summary>
    public required float HomeDanger { get; init; }

    /// <summary>The farthest, in metres, a village a god sets out to convert lies from its own.</summary>
    public required float ConvertReach { get; init; }

    /// <summary>Its followers' mean doubt from which a god sets out to win back their faith.</summary>
    public required float Doubt { get; init; }

    /// <summary>The land's dryness (1 minus the mean soil water) from which a god prepares for a drought.</summary>
    public required float Dryness { get; init; }
}
