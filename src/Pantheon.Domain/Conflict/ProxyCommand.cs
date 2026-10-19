namespace Pantheon.Domain.Conflict;

/// <summary>What a god may do through others (spec §45, ADR-026).</summary>
public enum ProxyKind
{
    /// <summary>Stir a god's followers in a settlement against it: resentment, and perhaps a revolt.</summary>
    Incite,

    /// <summary>Spread propaganda against a god among its followers in a settlement: doubt.</summary>
    Propaganda,
}

/// <summary>A god acts in secret through mortals in a settlement against another god (spec §45).</summary>
/// <param name="GodId">The acting god.</param>
/// <param name="SettlementId">The settlement.</param>
/// <param name="TargetGodId">The god whose followers are worked on.</param>
/// <param name="Kind">What it does.</param>
public sealed record ProxyCommand(int GodId, int SettlementId, int TargetGodId, ProxyKind Kind);
