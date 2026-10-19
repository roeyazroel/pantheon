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
}
