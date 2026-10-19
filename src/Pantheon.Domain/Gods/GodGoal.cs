namespace Pantheon.Domain.Gods;

/// <summary>What an AI god sets itself to do over the next minutes (spec §47, §87, ADR-036).</summary>
public enum GodGoalKind
{
    /// <summary>Protect its home village from what threatens it.</summary>
    ProtectHome,

    /// <summary>Convert a village it reaches but does not lead.</summary>
    Convert,

    /// <summary>Win a god's friendship, towards an alliance.</summary>
    Befriend,

    /// <summary>Weaken a god that leads it or that it hates.</summary>
    Weaken,

    /// <summary>Prepare its people for a drought, a hard season or the plague.</summary>
    Prepare,

    /// <summary>Bring down the ancient beast.</summary>
    SlayBeast,

    /// <summary>Win back its doubting followers' faith.</summary>
    Recover,
}

/// <summary>A goal an AI god holds (ADR-036): what, about which village or god, since when, and how much it matters.</summary>
public sealed class GodGoal
{
    /// <summary>What it is.</summary>
    public required GodGoalKind Kind { get; init; }

    /// <summary>The village it concerns, if any.</summary>
    public int? SettlementId { get; init; }

    /// <summary>The god it concerns, if any.</summary>
    public int? GodId { get; init; }

    /// <summary>When the god set itself the goal.</summary>
    public required long SinceTick { get; init; }

    /// <summary>How much it matters, as the planner weighed it.</summary>
    public required float Weight { get; set; }
}
