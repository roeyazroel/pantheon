using Pantheon.Domain.GreatEvents;

namespace Pantheon.Domain.Events;

/// <summary>A great event began (ADR-034).</summary>
public sealed record GreatEventBegan : WorldEvent
{
    /// <summary>The event.</summary>
    public required int EventId { get; init; }

    /// <summary>What it is.</summary>
    public required GreatEventKind Kind { get; init; }

    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>The god it concerns, if any.</summary>
    public int? GodId { get; init; }
}

/// <summary>A great event ended (ADR-034).</summary>
public sealed record GreatEventEnded : WorldEvent
{
    /// <summary>The event.</summary>
    public required int EventId { get; init; }

    /// <summary>What it was.</summary>
    public required GreatEventKind Kind { get; init; }

    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>The most villages, gods or people it reached.</summary>
    public required int Peak { get; init; }
}

/// <summary>The plague reached a village (ADR-034).</summary>
public sealed record PlagueStruck : WorldEvent
{
    /// <summary>The village.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The village it came from, or <c>null</c> for an outbreak.</summary>
    public int? FromSettlementId { get; init; }
}

/// <summary>The ancient beast was slain (ADR-035).</summary>
public sealed record BeastSlain : WorldEvent
{
    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>Where it fell.</summary>
    public required Pantheon.Domain.World.GroundPosition Position { get; init; }

    /// <summary>The gods who harmed it, the most first.</summary>
    public required System.Collections.Generic.IReadOnlyList<int> SlayerGodIds { get; init; }
}
