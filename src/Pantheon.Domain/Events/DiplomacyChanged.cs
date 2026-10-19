using Pantheon.Domain.Diplomacy;

namespace Pantheon.Domain.Events;

/// <summary>Two gods' status changed, or one proposed something to the other.</summary>
public sealed record DiplomacyChanged : WorldEvent
{
    /// <summary>The god that acted.</summary>
    public required int ActorGodId { get; init; }

    /// <summary>The other god.</summary>
    public required int OtherGodId { get; init; }

    /// <summary>What it did.</summary>
    public required DiplomaticCommandKind Kind { get; init; }

    /// <summary>Their status afterwards.</summary>
    public required DiplomaticStatus Status { get; init; }

    /// <summary>Whether the act betrayed an ally.</summary>
    public required bool Betrayal { get; init; }

    /// <summary>What kind of proposal an accept or decline answered; <c>null</c> for other moves (ADR-025).</summary>
    public Diplomacy.ProposalKind? Answered { get; init; }
}
