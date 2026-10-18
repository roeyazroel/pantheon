namespace Pantheon.Domain.Belief;

/// <summary>
/// The change a divine act makes to the belief of the people who witness it, before the closeness
/// of the witnesses is taken into account. Each value lies between -1 and 1: a positive value
/// closes that share of the gap to 1, a negative value removes that share of the current value.
/// </summary>
public sealed record BeliefImpulse
{
    /// <summary>Change to Faith.</summary>
    public required float Faith { get; init; }

    /// <summary>Change to Love.</summary>
    public required float Love { get; init; }

    /// <summary>Change to Fear.</summary>
    public required float Fear { get; init; }

    /// <summary>Change to Doubt.</summary>
    public required float Doubt { get; init; }
}
