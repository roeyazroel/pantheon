namespace Pantheon.Domain.Diplomacy;

/// <summary>What a god can do towards another (spec §113).</summary>
public enum DiplomaticCommandKind
{
    /// <summary>Propose an alliance.</summary>
    OfferAlliance,

    /// <summary>End an alliance: a betrayal (spec §40).</summary>
    BreakAlliance,

    /// <summary>Declare war.</summary>
    DeclareWar,

    /// <summary>Propose peace to end a war.</summary>
    RequestPeace,

    /// <summary>Accept the other's proposal.</summary>
    Accept,

    /// <summary>Decline the other's proposal.</summary>
    Decline,
}

/// <summary>A god's diplomatic intent towards another god, checked by the authority (spec §73).</summary>
/// <param name="GodId">The acting god.</param>
/// <param name="OtherGodId">The god it acts towards.</param>
/// <param name="Kind">What it does.</param>
public sealed record DiplomaticCommand(int GodId, int OtherGodId, DiplomaticCommandKind Kind);
