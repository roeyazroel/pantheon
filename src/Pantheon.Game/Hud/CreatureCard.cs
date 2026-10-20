namespace Pantheon.Game.Hud;

/// <summary>The Creature's status on the HUD (spec §57).</summary>
public sealed record CreatureCard
{
    /// <summary>Name, species and level.</summary>
    public required string Title { get; init; }

    /// <summary>What it is doing.</summary>
    public required string Doing { get; init; }

    /// <summary>Health and needs.</summary>
    public required string Condition { get; init; }

    /// <summary>Loyalty and what its deeds add up to.</summary>
    public required string Temperament { get; init; }

    /// <summary>Its character in §26's words (ADR-037), for example "brave, playful and loyal".</summary>
    public string Character { get; init; } = string.Empty;

    /// <summary>The paths it walks and what the next calling level brings (ADR-030).</summary>
    public string Paths { get; init; } = string.Empty;

    /// <summary>Its attributes (spec §23).</summary>
    public string Attributes { get; init; } = string.Empty;

    /// <summary>How it regards the Creatures it has met (ADR-017).</summary>
    public required string Bonds { get; init; }

    /// <summary>What a reward or punishment would judge now, or why there is nothing to judge.</summary>
    public required string JudgeHint { get; init; }

    /// <summary>Whether a reward or punishment would apply now.</summary>
    public required bool CanJudge { get; init; }

    /// <summary>Whether the Creature is banished.</summary>
    public required bool Banished { get; init; }

    /// <summary>Its name, for the card's title (ADR-059).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Its species.</summary>
    public string Species { get; init; } = string.Empty;

    /// <summary>Its level.</summary>
    public int Level { get; init; }

    /// <summary>Its health (0..1).</summary>
    public float Health { get; init; }

    /// <summary>Its hunger (0..1).</summary>
    public float Hunger { get; init; }

    /// <summary>Its tiredness (0..1).</summary>
    public float Fatigue { get; init; }

    /// <summary>Its boredom (0..1).</summary>
    public float Boredom { get; init; }

    /// <summary>Its loyalty to its god (0..1).</summary>
    public float Loyalty { get; init; }

    /// <summary>What its deeds add up to, from cruel (-1) to kind (1).</summary>
    public float Morality { get; init; }
}
