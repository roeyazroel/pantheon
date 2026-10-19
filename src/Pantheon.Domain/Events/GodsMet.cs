namespace Pantheon.Domain.Events;

/// <summary>How two gods came to know of each other (ADR-036).</summary>
public enum MeetingWay
{
    /// <summary>Their villages lie near each other.</summary>
    Neighbours,

    /// <summary>Both are followed in one village.</summary>
    SharedVillage,

    /// <summary>A road joins their villages.</summary>
    Road,

    /// <summary>Their Creatures met.</summary>
    Creatures,

    /// <summary>One's deeds touched the other's people.</summary>
    Deeds,

    /// <summary>They sat in the Divine Council.</summary>
    Council,

    /// <summary>They have dealings already: a stance, a treaty or a proposal.</summary>
    Diplomacy,
}

/// <summary>Two gods met: each now knows of the other (spec §11, ADR-036).</summary>
public sealed record GodsMet : WorldEvent
{
    /// <summary>One god, the lower id.</summary>
    public required int GodId { get; init; }

    /// <summary>The other god.</summary>
    public required int OtherGodId { get; init; }

    /// <summary>How they met.</summary>
    public required MeetingWay Way { get; init; }

    /// <summary>The village where they met, for neighbours, a shared village or a road.</summary>
    public int? SettlementId { get; init; }
}
