namespace Pantheon.Domain.Diplomacy;

/// <summary>What one god can propose to another.</summary>
public enum ProposalKind
{
    /// <summary>An alliance (spec §113: offer alliance).</summary>
    Alliance,

    /// <summary>Peace, ending a war (spec §113: request peace).</summary>
    Peace,
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
}
