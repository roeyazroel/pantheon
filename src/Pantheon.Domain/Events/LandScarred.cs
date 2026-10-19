namespace Pantheon.Domain.Events;

/// <summary>The land a village lives from is scarred for good (spec §18, ADR-028): repeated harm left damage that will not heal by itself.</summary>
public sealed record LandScarred : WorldEvent
{
    /// <summary>The village.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The mean scar of its land now (0..1).</summary>
    public required float Scar { get; init; }
}
