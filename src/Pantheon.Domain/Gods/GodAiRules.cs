using System.Collections.Generic;

namespace Pantheon.Domain.Gods;

/// <summary>
/// Data-driven tuning of the gods' strategic utility AI (spec §47–48, §87, ADR-011), loaded from
/// <c>game/Data/god_ai.json</c>.
/// </summary>
public sealed record GodAiRules
{
    /// <summary>Ticks between an AI god's strategic evaluations (spec §87: every 2–5 seconds).</summary>
    public required int EvaluationIntervalTicks { get; init; }

    /// <summary>Utility of doing nothing and saving Divine Power; an action must beat it.</summary>
    public required float Patience { get; init; }

    /// <summary>Largest random addition to a candidate's utility.</summary>
    public required float DecisionJitter { get; init; }

    /// <summary>Settlements of this population count as fully valuable; smaller ones count less (square root).</summary>
    public required float ReferencePopulation { get; init; }

    /// <summary>Distance in metres beyond which the AI does not send its Creature to a settlement it already stands near.</summary>
    public required float CreatureSendDistance { get; init; }

    /// <summary>Drought (0..1) from which an AI god thinks rain worth calling; the value grows from there to a parched land (ADR-013).</summary>
    public required float RainDrought { get; init; }

    /// <summary>How the strategy of each personality weighs its goals.</summary>
    public required IReadOnlyList<GodProfile> Profiles { get; init; }
}

/// <summary>The weights of one personality's goals (each 0..1, except where noted).</summary>
public sealed record GodProfile
{
    /// <summary>The personality.</summary>
    public required GodPersonality Personality { get; init; }

    /// <summary>Helping its own followers in need.</summary>
    public required float Protect { get; init; }

    /// <summary>Winning people who do not follow it with kind miracles.</summary>
    public required float Convert { get; init; }

    /// <summary>Frightening people, rivals' followers above all, with harsh miracles.</summary>
    public required float Intimidate { get; init; }

    /// <summary>Blessing the land of its people and of those it courts.</summary>
    public required float Bless { get; init; }

    /// <summary>Extra appeal of targets in hardship or full of doubt.</summary>
    public required float Opportunism { get; init; }

    /// <summary>How much it hates to spend: utility falls by <c>Thrift × cost / power</c>.</summary>
    public required float Thrift { get; init; }

    /// <summary>Sending its Creature to the settlement it courts most.</summary>
    public required float SendCreature { get; init; }

    /// <summary>What it teaches its Creature, from -1 (rewards cruelty) to 1 (rewards kindness).</summary>
    public required float CreatureKindness { get; init; }

    /// <summary>Appetite for alliances and peace.</summary>
    public required float Cooperation { get; init; }

    /// <summary>Appetite for war.</summary>
    public required float Aggression { get; init; }

    /// <summary>Willingness to break an alliance when it pays.</summary>
    public required float Treachery { get; init; }

    /// <summary>How heavily it weighs what others did to it.</summary>
    public required float Grudge { get; init; }
}
