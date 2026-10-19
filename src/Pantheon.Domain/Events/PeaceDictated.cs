using Pantheon.Domain.Conflict;

namespace Pantheon.Domain.Events;

/// <summary>A losing god accepted the victor's terms (spec §44, ADR-026).</summary>
public sealed record PeaceDictated : WorldEvent
{
    /// <summary>The victor.</summary>
    public required int WinnerGodId { get; init; }

    /// <summary>The loser.</summary>
    public required int LoserGodId { get; init; }

    /// <summary>What the war was for.</summary>
    public required WarGoal Goal { get; init; }

    /// <summary>The settlement ceded, for a conquest.</summary>
    public required int? SettlementId { get; init; }

    /// <summary>The holy site ceded.</summary>
    public required int? SiteId { get; init; }

    /// <summary>People who turned to the winner.</summary>
    public required int People { get; init; }
}
