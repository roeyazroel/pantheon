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

    /// <summary>Propose a treaty on the command's terms (spec §38).</summary>
    ProposeTreaty,

    /// <summary>Break a treaty: a betrayal (spec §40).</summary>
    BreakTreaty,

    /// <summary>Ask something of the other, calling in a favour it owes (spec §41).</summary>
    Request,

    /// <summary>Break an alliance and take a share of the ally's followers where the god leads (spec §54).</summary>
    Absorb,

    /// <summary>Demand the enemy's surrender on the terms of the war's goal (spec §44, ADR-026).</summary>
    DemandSurrender,
}

/// <summary>A god's diplomatic intent towards another god, checked by the authority (spec §73).</summary>
/// <param name="GodId">The acting god.</param>
/// <param name="OtherGodId">The god it acts towards.</param>
/// <param name="Kind">What it does.</param>
/// <param name="Terms">A treaty's terms, for <see cref="DiplomaticCommandKind.ProposeTreaty"/>.</param>
/// <param name="TreatyId">The treaty, for <see cref="DiplomaticCommandKind.BreakTreaty"/>.</param>
/// <param name="Ask">What is asked, for <see cref="DiplomaticCommandKind.Request"/>.</param>
/// <param name="Aim">What a declared war is for (ADR-026); conquest when <c>null</c>.</param>
public sealed record DiplomaticCommand(int GodId, int OtherGodId, DiplomaticCommandKind Kind, Politics.TreatyTerms? Terms = null, int? TreatyId = null, Politics.RequestAsk? Ask = null, Conflict.WarAim? Aim = null);
