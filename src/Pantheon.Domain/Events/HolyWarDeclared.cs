namespace Pantheon.Domain.Events;

/// <summary>A settlement went to holy war, of its own will, on a neighbour of another faith (spec §46, ADR-026).</summary>
public sealed record HolyWarDeclared : WorldEvent
{
    /// <summary>The attacking settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The settlement attacked.</summary>
    public required int DefenderSettlementId { get; init; }

    /// <summary>The god the attackers fight for.</summary>
    public required int GodId { get; init; }

    /// <summary>The god of the attacked.</summary>
    public required int EnemyGodId { get; init; }
}
