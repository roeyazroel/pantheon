namespace Pantheon.Domain.Miracles;

/// <summary>One effect of a miracle on the settlements or regions it reaches.</summary>
public sealed record MiracleEffect
{
    /// <summary>What the effect does.</summary>
    public required MiracleEffectKind Kind { get; init; }

    /// <summary>How much; the unit depends on <see cref="Kind"/>.</summary>
    public required float Amount { get; init; }
}
