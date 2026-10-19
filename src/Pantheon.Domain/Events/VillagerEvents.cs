using Pantheon.Domain.Villagers;

namespace Pantheon.Domain.Events;

/// <summary>A god began to follow a villager (spec §80, ADR-043).</summary>
public sealed record VillagerFollowed : WorldEvent
{
    /// <summary>The villager.</summary>
    public required int VillagerId { get; init; }

    /// <summary>Their name.</summary>
    public required string Name { get; init; }

    /// <summary>The god who follows them.</summary>
    public required int GodId { get; init; }

    /// <summary>Their village.</summary>
    public required int SettlementId { get; init; }
}

/// <summary>A followed villager fled danger.</summary>
public sealed record VillagerFled : WorldEvent
{
    /// <summary>The villager.</summary>
    public required int VillagerId { get; init; }

    /// <summary>Their name.</summary>
    public required string Name { get; init; }

    /// <summary>The god who follows them.</summary>
    public required int GodId { get; init; }

    /// <summary>The village they fled.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The village they flee to, if any is safe.</summary>
    public int? RefugeId { get; init; }

    /// <summary>The danger.</summary>
    public required VillagerFate Cause { get; init; }
}

/// <summary>A followed villager turned to another god, or came to believe.</summary>
public sealed record VillagerTurned : WorldEvent
{
    /// <summary>The villager.</summary>
    public required int VillagerId { get; init; }

    /// <summary>Their name.</summary>
    public required string Name { get; init; }

    /// <summary>The god who follows them.</summary>
    public required int GodId { get; init; }

    /// <summary>The god they left, if any.</summary>
    public int? FromGodId { get; init; }

    /// <summary>The god they turned to.</summary>
    public required int ToGodId { get; init; }
}

/// <summary>A followed villager died.</summary>
public sealed record VillagerDied : WorldEvent
{
    /// <summary>The villager.</summary>
    public required int VillagerId { get; init; }

    /// <summary>Their name.</summary>
    public required string Name { get; init; }

    /// <summary>The god who followed them.</summary>
    public required int GodId { get; init; }

    /// <summary>Their village.</summary>
    public required int SettlementId { get; init; }

    /// <summary>What took them.</summary>
    public required VillagerFate Fate { get; init; }

    /// <summary>Their age, in whole years.</summary>
    public required int Age { get; init; }
}
