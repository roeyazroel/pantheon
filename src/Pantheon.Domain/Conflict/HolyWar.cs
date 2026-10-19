namespace Pantheon.Domain.Conflict;

/// <summary>What a god says to its people's holy war (spec §46: encourage, discourage, ignore).</summary>
public enum HolyWarStance
{
    /// <summary>It lets the people be.</summary>
    Ignore,

    /// <summary>It urges them on: fiercer raids.</summary>
    Encourage,

    /// <summary>It asks them to stop: gentler raids and an earlier end, at the cost of their love.</summary>
    Discourage,
}

/// <summary>A holy war a settlement wages of its own will on a neighbour of another faith (spec §46, ADR-026).</summary>
public sealed class HolyWarState
{
    /// <summary>The settlement attacked.</summary>
    public required int DefenderSettlementId { get; init; }

    /// <summary>The god the attackers fight for: their main god when it began.</summary>
    public required int GodId { get; init; }

    /// <summary>The god of the attacked, then.</summary>
    public required int EnemyGodId { get; init; }

    /// <summary>Tick it began.</summary>
    public required long SinceTick { get; init; }

    /// <summary>What the attackers' god says to it.</summary>
    public required HolyWarStance Stance { get; set; }

    /// <summary>People the raids have killed.</summary>
    public required int Killed { get; set; }

    /// <summary>People the raids have converted.</summary>
    public required int Converted { get; set; }
}
