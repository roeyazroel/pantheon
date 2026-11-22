namespace Pantheon.Domain.World;

/// <summary>
/// The choices a world was made with that its rules go on reading (ADR-082): how often calamities
/// strike and how strong its rival gods play, with the village count and the rivals' temper it was
/// founded with, kept so the world can tell how it began. Chosen on the New World page.
/// </summary>
public sealed record WorldSetup
{
    /// <summary>How often disasters, plagues and the beast strike: their onset rates scaled (<c>disasters.json</c>'s <c>Calamities</c>).</summary>
    public CalamityLevel Calamities { get; init; } = CalamityLevel.Normal;

    /// <summary>How strongly the AI gods play: their starting Divine Power and how sharply they choose (<c>god_ai.json</c>'s <c>Strengths</c>).</summary>
    public RivalStrength Rivals { get; init; } = RivalStrength.Even;

    /// <summary>The temper the rivals were born with: which personalities they could draw.</summary>
    public RivalTemper Temper { get; init; } = RivalTemper.Mixed;

    /// <summary>How many villages the island was founded with for its size.</summary>
    public VillageCount Villages { get; init; } = VillageCount.Normal;

    /// <summary>The setup of a world made before these choices, and the default of a new one.</summary>
    public static WorldSetup Default { get; } = new();
}

/// <summary>How often calamities strike a world (ADR-082).</summary>
public enum CalamityLevel
{
    /// <summary>Rarely.</summary>
    Gentle,

    /// <summary>As the content's rates say.</summary>
    Normal,

    /// <summary>Often.</summary>
    Harsh,
}

/// <summary>How strongly a world's AI gods play (ADR-082).</summary>
public enum RivalStrength
{
    /// <summary>Less power at the start, slower and less sure choices.</summary>
    Weak,

    /// <summary>As the content's rules say.</summary>
    Even,

    /// <summary>More power at the start, quicker and surer choices.</summary>
    Strong,
}

/// <summary>Which personalities a world's rival gods may be born with (ADR-082).</summary>
public enum RivalTemper
{
    /// <summary>Any personality.</summary>
    Mixed,

    /// <summary>The kinder personalities only (<c>world_sizes.json</c>'s <c>Tempers</c>).</summary>
    Peaceful,

    /// <summary>The harsher personalities only.</summary>
    Warlike,
}

/// <summary>How many villages an island is founded with for its size (ADR-082).</summary>
public enum VillageCount
{
    /// <summary>Fewer than the size's own number.</summary>
    Few,

    /// <summary>The size's own number.</summary>
    Normal,

    /// <summary>More than the size's own number.</summary>
    Many,
}
