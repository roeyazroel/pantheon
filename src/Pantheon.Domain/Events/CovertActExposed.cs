namespace Pantheon.Domain.Events;

/// <summary>A god's veiled miracle or proxy act was traced to it (spec §43, §45, ADR-026).</summary>
public sealed record CovertActExposed : WorldEvent
{
    /// <summary>The god exposed.</summary>
    public required int GodId { get; init; }

    /// <summary>The god whose people it worked on.</summary>
    public required int VictimGodId { get; init; }

    /// <summary>What it did: a miracle's id, or the proxy act's name.</summary>
    public required string Act { get; init; }

    /// <summary>Where.</summary>
    public required int? SettlementId { get; init; }
}
