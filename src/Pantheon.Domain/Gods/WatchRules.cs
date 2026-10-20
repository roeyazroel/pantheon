namespace Pantheon.Domain.Gods;

/// <summary>
/// How a god keeps watch over the villages it leads (ADR-070): when another god's Creature comes
/// within <see cref="Radius"/> of one, it answers by how it stands with that god, at once.
/// </summary>
public sealed record WatchRules
{
    /// <summary>No watch: gods ignore other Creatures in their lands (the pinned rules of older tests).</summary>
    public static WatchRules None { get; } = new() { Radius = 0f };

    /// <summary>How near a village a visiting Creature is noticed, in metres; 0 turns the watch off.</summary>
    public required float Radius { get; init; }

    /// <summary>Ticks between looks.</summary>
    public int IntervalTicks { get; init; } = 10;

    /// <summary>Seconds a god holds one visit in mind: it remembers and tells of a visitor once in this time.</summary>
    public float CooldownSeconds { get; init; } = 60f;

    /// <summary>Hostility (0..1) from which a god drives a visitor out: its Creature is set on it.</summary>
    public float RepelHostility { get; init; } = 0.75f;

    /// <summary>Hostility (0..1) up to which a god welcomes a visitor: its Creature comes to greet it.</summary>
    public float WelcomeHostility { get; init; } = 0.25f;

    /// <summary>How sure a god must be of its Creature to set it on a visitor; less sure, it only watches.</summary>
    public float RepelConfidence { get; init; } = 0.4f;
}
