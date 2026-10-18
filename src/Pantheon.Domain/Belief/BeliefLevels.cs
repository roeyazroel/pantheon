namespace Pantheon.Domain.Belief;

/// <summary>A set of belief values, for example a god's home settlement at the start of a world.</summary>
public sealed record BeliefLevels
{
    /// <summary>Share of the population that follows the god (0..1).</summary>
    public required float Adherence { get; init; }

    /// <summary>Faith (0..1).</summary>
    public required float Faith { get; init; }

    /// <summary>Love (0..1).</summary>
    public required float Love { get; init; }

    /// <summary>Fear (0..1).</summary>
    public required float Fear { get; init; }

    /// <summary>Doubt (0..1).</summary>
    public required float Doubt { get; init; }
}
