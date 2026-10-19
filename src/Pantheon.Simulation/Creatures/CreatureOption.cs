using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Creatures;

/// <summary>Something a Creature could do next, with how much it wants to.</summary>
/// <param name="Situation">The situation it would act in.</param>
/// <param name="Action">The action.</param>
/// <param name="SettlementId">The settlement it concerns, if any.</param>
/// <param name="Target">Where it would go.</param>
/// <param name="Utility">How much it wants to, before the random jitter: preference × drive × personality × closeness.</param>
/// <param name="CreatureId">The Creature it concerns, if any: one to fight, play with or flee (ADR-017).</param>
public readonly record struct CreatureOption(CreatureSituation Situation, CreatureAction Action, int? SettlementId, GroundPosition Target, float Utility, int? CreatureId = null);
