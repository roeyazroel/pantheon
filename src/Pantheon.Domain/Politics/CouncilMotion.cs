namespace Pantheon.Domain.Politics;

/// <summary>What the Divine Council may vote on (spec §42).</summary>
public enum CouncilMotionKind
{
    /// <summary>Condemn a god: every god trusts it less and its people doubt it.</summary>
    Condemn,

    /// <summary>Protect a holy site: no harmful miracle may fall there.</summary>
    ProtectSacredLand,

    /// <summary>Prohibit a practice everywhere.</summary>
    ProhibitPractice,

    /// <summary>Coordinate disaster response: help where disaster struck costs less.</summary>
    CoordinateDisasterResponse,

    /// <summary>Declare pantheon-wide war: every god that voted for it goes to war with a god.</summary>
    PantheonWar,

    /// <summary>Establish neutral territory: no god may convert or strike in a settlement.</summary>
    NeutralTerritory,
}

/// <summary>A practice the council may prohibit.</summary>
public enum Practice
{
    /// <summary>Striking mortals with lightning and judgement.</summary>
    Smiting,

    /// <summary>Loosing fires, earthquakes and floods.</summary>
    Cataclysms,

    /// <summary>Terrifying mortals into obedience.</summary>
    Terror,
}

/// <summary>Why a council was summoned (spec §42: periodic major events).</summary>
public enum CouncilTrigger
{
    /// <summary>A god broke an alliance or a treaty.</summary>
    Betrayal,

    /// <summary>A god, or a god and its ally, began to dominate the world.</summary>
    Dominance,

    /// <summary>A deadly disaster.</summary>
    Disaster,

    /// <summary>A motion a god tabled, at the regular session.</summary>
    Tabled,
}

/// <summary>A vote's choice.</summary>
public enum VoteChoice
{
    /// <summary>For the motion.</summary>
    Yes,

    /// <summary>Against it.</summary>
    No,

    /// <summary>Neither.</summary>
    Abstain,
}

/// <summary>A motion before the council (spec §42).</summary>
public sealed record CouncilMotion
{
    /// <summary>What is voted on.</summary>
    public required CouncilMotionKind Kind { get; init; }

    /// <summary>The god that tabled it; <c>null</c> when the event that summoned the council did.</summary>
    public int? ProposerGodId { get; init; }

    /// <summary>The god condemned or warred upon.</summary>
    public int? TargetGodId { get; init; }

    /// <summary>The holy site protected.</summary>
    public int? SiteId { get; init; }

    /// <summary>The settlement made neutral.</summary>
    public int? SettlementId { get; init; }

    /// <summary>The practice prohibited.</summary>
    public Practice? Practice { get; init; }

    /// <summary>Whether the ballot is secret: only the tally is known (spec §42: votes may be public or secret).</summary>
    public required bool Secret { get; init; }
}
