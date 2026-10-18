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

    /// <summary>The god's rank is below the miracle's minimum.</summary>
    RankTooLow,

    /// <summary>The god cast the miracle too recently.</summary>
    OnCooldown,

    /// <summary>The target lies outside the world.</summary>
    OutsideWorld,

    /// <summary>The god's influence at the target is below the miracle's requirement.</summary>
    OutsideInfluence,

    /// <summary>The god lacks the Divine Power.</summary>
    NotEnoughPower,
}
