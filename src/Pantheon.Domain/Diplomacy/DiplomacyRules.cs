namespace Pantheon.Domain.Diplomacy;

/// <summary>
/// Data-driven tuning of relations between gods, loaded from <c>game/Data/diplomacy.json</c>
/// (ADR-012). Rates are per diplomacy step; act effects apply once per act.
/// </summary>
public sealed record DiplomacyRules
{
    /// <summary>Ticks between diplomacy steps.</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Trust of gods who have not met yet.</summary>
    public required float StartingTrust { get; init; }

    /// <summary>Rivalry of gods who have not met yet.</summary>
    public required float StartingRivalry { get; init; }

    /// <summary>Suspicion of gods who have not met yet.</summary>
    public required float StartingSuspicion { get; init; }

    /// <summary>Share of the gap to its resting value that trust, rivalry, suspicion and gratitude close per step.</summary>
    public required float DriftPerStep { get; init; }

    /// <summary>Share of a memory's weight that fades per step.</summary>
    public required float MemoryFadePerStep { get; init; }

    /// <summary>Share of a betrayal's weight that fades per step; betrayal is remembered far longer.</summary>
    public required float BetrayalFadePerStep { get; init; }

    /// <summary>Most memories one god keeps of another.</summary>
    public required int MemoryCapacity { get; init; }

    /// <summary>Simulated seconds a proposal waits for an answer.</summary>
    public required float ProposalSeconds { get; init; }

    /// <summary>Simulated seconds after a proposal before either god of the pair may propose again.</summary>
    public required float ProposalCooldownSeconds { get; init; }

    /// <summary>Simulated seconds after a change of status before the same pair may change it again.</summary>
    public required float StatusCooldownSeconds { get; init; }

    /// <summary>Gratitude gained when the other helps one's followers.</summary>
    public required float HelpGratitude { get; init; }

    /// <summary>Rivalry gained when the other courts one's followers with kindness.</summary>
    public required float CourtingRivalry { get; init; }

    /// <summary>Trust lost when the other strikes one's followers.</summary>
    public required float HarmTrustLoss { get; init; }

    /// <summary>Rivalry gained when the other strikes one's followers or wins a settlement from one.</summary>
    public required float HarmRivalry { get; init; }

    /// <summary>Trust the betrayed god loses when an ally breaks the alliance.</summary>
    public required float BetrayalTrustLoss { get; init; }

    /// <summary>Trust every other god loses in a god that betrays an ally (reputation, spec §40).</summary>
    public required float BetrayalReputationLoss { get; init; }

    /// <summary>Share by which a people at war resist the enemy god's pull in settlements where their god holds the majority.</summary>
    public required float WarResistance { get; init; }

    /// <summary>How much more a Creature wants to attack a settlement whose main god is at war with its own (added to the drive).</summary>
    public required float WarCreatureAggression { get; init; }

    /// <summary>Rivalry and lack of trust at which relations sour to hostile, and above which (inverted) they warm to friendly.</summary>
    public required float HostilityThreshold { get; init; }

    /// <summary>Treaties, favours and the Divine Council (ADR-025).</summary>
    public required Politics.PoliticsRules Politics { get; init; }

    /// <summary>War goals, intelligence, proxy wars and holy wars (ADR-026).</summary>
    public required Conflict.ConflictRules Conflict { get; init; }
}
