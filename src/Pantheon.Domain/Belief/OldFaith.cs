namespace Pantheon.Domain.Belief;

/// <summary>
/// People who broke away from their god in a schism (spec §34, ADR-021): they keep "the Old Faith"
/// of the god as it was and no longer worship the god as it is. They follow no other god while it
/// lasts; they return when the god is loved there again, or fade into unbelief.
/// </summary>
public sealed class OldFaith
{
    /// <summary>The god they broke away from.</summary>
    public required int GodId { get; init; }

    /// <summary>Share of the settlement's people who keep it (0..1); 0 once it has ended.</summary>
    public required float Share { get; set; }

    /// <summary>Share of the settlement's people who have returned to the god so far.</summary>
    public required float Returned { get; set; }

    /// <summary>Share of the settlement's people who have fallen into unbelief so far.</summary>
    public required float Faded { get; set; }

    /// <summary>Tick of the schism.</summary>
    public required long SinceTick { get; init; }
}
