namespace Pantheon.Domain.Migration;

/// <summary>
/// Data-driven tuning of migration, loaded from <c>game/Data/migration.json</c> (ADR-016). Rates
/// are per simulated second; values without units are normalised to 0..1.
/// </summary>
public sealed record MigrationRules
{
    /// <summary>Ticks between migration steps.</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Weight of hunger (the settlement's food need) in the pressure to leave.</summary>
    public required float FamineWeight { get; init; }

    /// <summary>Weight of the share of people without a roof.</summary>
    public required float HomelessWeight { get; init; }

    /// <summary>Weight of danger: fire on the land or the ground shaking.</summary>
    public required float DangerWeight { get; init; }

    /// <summary>Weight of war: two gods at war each followed by at least <see cref="WarPresence"/> of the people.</summary>
    public required float WarWeight { get; init; }

    /// <summary>Share of the people a god at war must hold for its war to reach the settlement.</summary>
    public required float WarPresence { get; init; }

    /// <summary>Weight of the followers' fear of their gods.</summary>
    public required float FearWeight { get; init; }

    /// <summary>Weight of the followers' doubt in their gods.</summary>
    public required float DoubtWeight { get; init; }

    /// <summary>Love keeps people home: pressure times <c>1 - LoveLoyalty × love</c>.</summary>
    public required float LoveLoyalty { get; init; }

    /// <summary>Pressure below which nobody wants to leave.</summary>
    public required float PressureThreshold { get; init; }

    /// <summary>Share of the population that comes to want to leave per second at full pressure (scaled by the pressure past the threshold).</summary>
    public required float EmigrationPerSecond { get; init; }

    /// <summary>Share per second by which the wish to leave fades while the pressure is below the threshold.</summary>
    public required float UrgeFadePerSecond { get; init; }

    /// <summary>Fewest people who set out together.</summary>
    public required int MinGroupSize { get; init; }

    /// <summary>People set out once this share of the population (or <see cref="MinGroupSize"/>, if more) wants to leave.</summary>
    public required float GroupShare { get; init; }

    /// <summary>Largest share of a settlement's people in one group.</summary>
    public required float MaxGroupShare { get; init; }

    /// <summary>People who always stay behind.</summary>
    public required int MinRemaining { get; init; }

    /// <summary>Most groups on the road at once.</summary>
    public required int MaxGroups { get; init; }

    /// <summary>Farthest destination in metres, in a straight line.</summary>
    public required float MaxDistance { get; init; }

    /// <summary>A destination's pressure must be at least this much lower than the origin's.</summary>
    public required float BetterBy { get; init; }

    /// <summary>Weight of free housing in a destination's appeal.</summary>
    public required float RoomWeight { get; init; }

    /// <summary>Weight of a destination's food security.</summary>
    public required float FoodWeight { get; init; }

    /// <summary>Weight of a destination's safety from fire and shaking.</summary>
    public required float SafetyWeight { get; init; }

    /// <summary>Weight of finding the migrants' own gods followed there.</summary>
    public required float FaithWeight { get; init; }

    /// <summary>Weight of distance (per <see cref="MaxDistance"/>) against a destination.</summary>
    public required float DistanceWeight { get; init; }

    /// <summary>Walking speed in metres per second.</summary>
    public required float Speed { get; init; }

    /// <summary>Food each migrant takes along, at most their share of the stores.</summary>
    public required float FoodTakenPerPerson { get; init; }

    /// <summary>Food each migrant eats per second on the road.</summary>
    public required float FoodPerPersonPerSecond { get; init; }

    /// <summary>Health lost per second on the road without food.</summary>
    public required float HungerHealthLossPerSecond { get; init; }

    /// <summary>Health regained per second on the road with food.</summary>
    public required float HealthRecoveryPerSecond { get; init; }

    /// <summary>Share of the group that dies per second at zero health (scaled by <c>1 - health</c>).</summary>
    public required float DeathsPerSecond { get; init; }

    /// <summary>Migrants give up following a god by this share times their fear or doubt of it, whichever is higher.</summary>
    public required float AbandonShare { get; init; }

    /// <summary>Love that welcomed migrants gain for the god that took them in.</summary>
    public required float WelcomeLove { get; init; }

    /// <summary>Share of the migrants' other beliefs (and of those who follow no god) moved to a god that demands conversion.</summary>
    public required float ConversionDemand { get; init; }

    /// <summary>Fear of the converting god among those made to convert.</summary>
    public required float ConversionFear { get; init; }

    /// <summary>Doubt of the converting god among those made to convert.</summary>
    public required float ConversionDoubt { get; init; }

    /// <summary>Love migrants lose for a god that turned them away.</summary>
    public required float TurnedAwayLoss { get; init; }

    /// <summary>A god's followers short of this share of room makes a welcoming AI god turn migrants away.</summary>
    public required float OverwhelmedHomeless { get; init; }

    /// <summary>Weight of unhappiness in the pressure to leave (ADR-023).</summary>
    public required float UnhappinessWeight { get; init; }

    /// <summary>Weight of instability in the pressure to leave (ADR-023).</summary>
    public required float InstabilityWeight { get; init; }

    /// <summary>Extra share of a group's speed per level of its origin's navigation (spec §36).</summary>
    public required float NavigationPerLevel { get; init; }
}
