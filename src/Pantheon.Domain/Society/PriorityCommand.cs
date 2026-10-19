namespace Pantheon.Domain.Society;

/// <summary>A god asks a settlement to put something first (spec §29): the intent the society authority checks (§73).</summary>
/// <param name="GodId">The god that asks.</param>
/// <param name="SettlementId">The settlement.</param>
/// <param name="Priority">What to put first; <see cref="SettlementPriority.None"/> lets it run itself again.</param>
public sealed record PriorityCommand(int GodId, int SettlementId, SettlementPriority Priority);
