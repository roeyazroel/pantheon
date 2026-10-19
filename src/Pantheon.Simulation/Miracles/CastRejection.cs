namespace Pantheon.Simulation.Miracles;

/// <summary>Why the authority refuses a miracle (spec §73), or <see cref="None"/> when it may be cast.</summary>
public enum CastRejection
{
    /// <summary>The miracle may be cast.</summary>
    None,

    /// <summary>No god has the requested id.</summary>
    UnknownGod,

    /// <summary>No miracle has the requested id.</summary>
    UnknownMiracle,

    /// <summary>The issuer does not control the god.</summary>
    NotAuthorized,

    /// <summary>The miracle is a domain's signature and the god does not hold that domain (ADR-021).</summary>
    DomainRequired,

    /// <summary>The god's rank is below the miracle's minimum.</summary>
    RankTooLow,

    /// <summary>The god cast the miracle too recently.</summary>
    OnCooldown,

    /// <summary>The target lies outside the world.</summary>
    OutsideWorld,

    /// <summary>The god's influence at the target is below the miracle's requirement.</summary>
    OutsideInfluence,

    /// <summary>What the miracle acts on is not there: no cloud to carry, no rival Creature to wound, or the god's own Creature is not there to teach (ADR-021).</summary>
    NothingThere,

    /// <summary>The god lacks the Divine Power.</summary>
    NotEnoughPower,

    /// <summary>A territorial agreement keeps the god out of the land where it would cast (ADR-025).</summary>
    TreatyForbids,
}
