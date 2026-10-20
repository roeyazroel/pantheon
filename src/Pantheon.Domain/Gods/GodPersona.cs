namespace Pantheon.Domain.Gods;

/// <summary>
/// Who a rival god is (spec §47, ADR-065): its epithet, the tale the world tells of it, how it
/// speaks, what it wants and what it dreads, and how its temper leans from its personality's. It is
/// written once, when the world is made, by the scribes (from the seed) or by a language model, and
/// kept in the world: the temper changes what the god does, the rest how it is told.
/// </summary>
public sealed record GodPersona
{
    /// <summary>What the god is called after its name, for example "the Ash-Crowned".</summary>
    public required string Epithet { get; init; }

    /// <summary>The god's history as the island tells it: a few sentences.</summary>
    public required string History { get; init; }

    /// <summary>How the god speaks, for example "curt and cold, speaks of itself as we".</summary>
    public required string Voice { get; init; }

    /// <summary>What the god wants above all.</summary>
    public required string Desire { get; init; }

    /// <summary>What the god dreads.</summary>
    public required string Dread { get; init; }

    /// <summary>How its temper leans from its personality's.</summary>
    public required GodTemper Temper { get; init; }

    /// <summary>Who wrote it: the scribes, or the language model's name.</summary>
    public required string Author { get; init; }
}

/// <summary>
/// How a god's temper leans from its personality's profile (ADR-065), each from -1 to 1: the god AI
/// adds a leaning times <c>god_ai.json</c>'s <c>TemperReach</c> to the profile's weight.
/// </summary>
public sealed record GodTemper
{
    /// <summary>No leaning at all.</summary>
    public static readonly GodTemper Even = new();

    /// <summary>Appetite for war.</summary>
    public float Aggression { get; init; }

    /// <summary>Appetite for alliances and peace.</summary>
    public float Cooperation { get; init; }

    /// <summary>Willingness to break its word when it pays.</summary>
    public float Treachery { get; init; }

    /// <summary>How long it remembers a wrong.</summary>
    public float Grudge { get; init; }

    /// <summary>Care for its own followers.</summary>
    public float Protect { get; init; }

    /// <summary>Zeal to win new followers with kindness.</summary>
    public float Convert { get; init; }

    /// <summary>Taste for ruling by fear.</summary>
    public float Intimidate { get; init; }

    /// <summary>Whether it leans at all.</summary>
    public bool IsEven => Aggression == 0f && Cooperation == 0f && Treachery == 0f && Grudge == 0f && Protect == 0f && Convert == 0f && Intimidate == 0f;
}
