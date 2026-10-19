namespace Pantheon.Domain.Diplomacy;

/// <summary>What one god can propose to another.</summary>
public enum ProposalKind
{
    /// <summary>An alliance (spec §113: offer alliance).</summary>
    Alliance,

    /// <summary>Peace, ending a war (spec §113: request peace).</summary>
    Peace,

    /// <summary>A treaty, on the proposal's terms (spec §38, ADR-025).</summary>
    Treaty,

    /// <summary>A request, which calls in a favour when one is owed (spec §41).</summary>
    Request,

    /// <summary>The victor's terms of peace, by its war's goal (spec §44, ADR-026).</summary>
    Surrender,
}

/// <summary>A proposal waiting for an answer.</summary>
public sealed class DiplomaticProposal
{
    /// <summary>The proposing god.</summary>
    public required int FromGodId { get; init; }

    /// <summary>The god asked.</summary>
    public required int ToGodId { get; init; }

    /// <summary>What is proposed.</summary>
    public required ProposalKind Kind { get; init; }

    /// <summary>Tick after which the proposal lapses unanswered.</summary>
    public required long ExpiresAtTick { get; init; }

    /// <summary>The treaty's terms, for <see cref="ProposalKind.Treaty"/>.</summary>
    public Politics.TreatyTerms? Terms { get; init; }

    /// <summary>What is asked, for <see cref="ProposalKind.Request"/>.</summary>
    public Politics.RequestAsk? Ask { get; init; }
}
