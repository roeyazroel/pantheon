namespace Pantheon.Domain.Politics;

/// <summary>What one god may ask of another, calling in a favour or not (spec §41).</summary>
public enum AskKind
{
    /// <summary>Divine Power.</summary>
    DivinePower,

    /// <summary>War on a named god.</summary>
    WarSupport,

    /// <summary>Its vote with the asker's in the Divine Council (spec §42: votes may be traded).</summary>
    Backing,

    /// <summary>A territorial agreement: it keeps out of the asker's villages for a while.</summary>
    Territory,

    /// <summary>Miracles for the asker's people in need, within a deadline.</summary>
    Assistance,
}

/// <summary>A request from one god to another (spec §41).</summary>
/// <param name="Kind">What is asked.</param>
/// <param name="TargetGodId">The god a war support is against.</param>
/// <param name="Amount">The Divine Power asked.</param>
public sealed record RequestAsk(AskKind Kind, int? TargetGodId = null, float Amount = 0f);
