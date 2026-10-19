using Pantheon.Domain.Hand;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Events;

/// <summary>A god's hand lifted villagers from a settlement (spec §55, ADR-030).</summary>
public sealed record VillagersLifted : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>The settlement they were lifted from.</summary>
    public required int SettlementId { get; init; }

    /// <summary>How many.</summary>
    public required int People { get; init; }

    /// <summary>Whether they were lifted out of danger.</summary>
    public required bool FromDanger { get; init; }
}

/// <summary>A god's hand set villagers down: in a settlement, or on open land from which they walk to one (spec §55, ADR-030).</summary>
public sealed record VillagersSetDown : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>The settlement they were lifted from.</summary>
    public required int FromSettlementId { get; init; }

    /// <summary>The settlement they joined or walk to; <c>null</c> when they drowned.</summary>
    public required int? ToSettlementId { get; init; }

    /// <summary>How many.</summary>
    public required int People { get; init; }

    /// <summary>Whether they were carried out of danger to safety: a rescue.</summary>
    public required bool Rescued { get; init; }

    /// <summary>Whether they were set down in the sea and drowned.</summary>
    public required bool Drowned { get; init; }
}

/// <summary>Something a god's hand threw struck the land (spec §55, ADR-030).</summary>
public sealed record HandStruck : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>What was thrown.</summary>
    public required HeldKind Kind { get; init; }

    /// <summary>Where it struck.</summary>
    public required GroundPosition Position { get; init; }

    /// <summary>The settlement it struck, if any.</summary>
    public required int? SettlementId { get; init; }

    /// <summary>Houses it destroyed.</summary>
    public required int HousesLost { get; init; }

    /// <summary>People it killed, the thrown ones included.</summary>
    public required int Deaths { get; init; }
}
