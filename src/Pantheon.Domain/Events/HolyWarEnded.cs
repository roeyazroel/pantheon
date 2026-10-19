namespace Pantheon.Domain.Events;

/// <summary>A holy war ended (spec §46, ADR-026).</summary>
public sealed record HolyWarEnded : WorldEvent
{
    /// <summary>The attacking settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The settlement attacked.</summary>
    public required int DefenderSettlementId { get; init; }

    /// <summary>The god the attackers fought for.</summary>
    public required int GodId { get; init; }

    /// <summary>People killed.</summary>
    public required int Killed { get; init; }

    /// <summary>People converted.</summary>
    public required int Converted { get; init; }

    /// <summary>Whether it ended because the attacked village turned to the attackers' god.</summary>
    public required bool Won { get; init; }
}
