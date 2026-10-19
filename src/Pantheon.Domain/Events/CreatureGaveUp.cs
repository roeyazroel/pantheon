using Pantheon.Domain.Creatures;

namespace Pantheon.Domain.Events;

/// <summary>Why a Creature gave up what it was doing (spec §86's behaviour tree, ADR-047).</summary>
public enum GiveUpReason
{
    /// <summary>The village it went to feed no longer needs it.</summary>
    NoLongerInNeed,

    /// <summary>The village it went to help build has nothing left to build.</summary>
    NothingToBuild,

    /// <summary>The stores it went to eat from are empty.</summary>
    StoresEmpty,

    /// <summary>The village it went to attack now follows an ally of its god.</summary>
    NowAnAlly,

    /// <summary>The village it went to is gone.</summary>
    VillageGone,

    /// <summary>Its hunger or weariness became too pressing to go on with what its god commanded.</summary>
    Desperate,

    /// <summary>The Creature it sought or played with is gone.</summary>
    OtherGone,
}

/// <summary>A Creature gave up what it was doing because its reason was gone (ADR-047).</summary>
public sealed record CreatureGaveUp : WorldEvent
{
    /// <summary>The Creature.</summary>
    public required int CreatureId { get; init; }

    /// <summary>What it gave up.</summary>
    public required CreatureAction Action { get; init; }

    /// <summary>The village it concerned, if any.</summary>
    public int? SettlementId { get; init; }

    /// <summary>Why.</summary>
    public required GiveUpReason Reason { get; init; }
}
