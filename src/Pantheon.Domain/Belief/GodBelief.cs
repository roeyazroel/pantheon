namespace Pantheon.Domain.Belief;

/// <summary>
/// How one settlement regards one god (spec §7, §109): the share of its people who follow the god
/// and the population's Faith, Love, Fear and Doubt towards it, each from 0 to 1. Followers are
/// <c>Population × Adherence</c>; the shares of all gods add up to at most 1, and the rest of the
/// people follow no god.
/// </summary>
public sealed class GodBelief
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>Share of the population that follows the god (0..1).</summary>
    public required float Adherence { get; set; }

    /// <summary>How strongly the people believe the god exists and has power (0..1).</summary>
    public required float Faith { get; set; }

    /// <summary>How warmly the people regard the god (0..1).</summary>
    public required float Love { get; set; }

    /// <summary>How afraid the people are of the god (0..1).</summary>
    public required float Fear { get; set; }

    /// <summary>How uncertain the people have become about the god (0..1).</summary>
    public required float Doubt { get; set; }
}
