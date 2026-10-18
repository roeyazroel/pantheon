using System;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Diplomacy;

/// <summary>
/// Records acts in the observer's relation (spec §40–41): a memory, and the immediate change in
/// trust, rivalry and gratitude. Memories beyond the capacity are forgotten, weakest first.
/// </summary>
public sealed class DiplomacyLedger : IActRecorder
{
    private readonly DiplomacyRules _rules;

    /// <summary>Creates the ledger.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    public DiplomacyLedger(DiplomacyRules rules)
    {
        DiplomacyRulesValidator.Validate(rules);
        _rules = rules;
    }

    /// <inheritdoc />
    public void Record(WorldState world, int observerGodId, int actorGodId, DiplomaticAct act, int? settlementId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (observerGodId == actorGodId)
        {
            return;
        }

        var relation = DiplomacyModel.EnsureRelation(_rules, world, observerGodId, actorGodId);
        switch (act)
        {
            case DiplomaticAct.Helped:
                relation.Gratitude = SimMath.Clamp01(relation.Gratitude + _rules.HelpGratitude);
                break;
            case DiplomaticAct.Courted:
                relation.Rivalry = SimMath.Clamp01(relation.Rivalry + _rules.CourtingRivalry);
                relation.Suspicion = SimMath.Clamp01(relation.Suspicion + (_rules.CourtingRivalry / 2f));
                break;
            case DiplomaticAct.Harmed:
            case DiplomaticAct.CreatureAttacked:
                relation.Trust = SimMath.Clamp01(relation.Trust - _rules.HarmTrustLoss);
                relation.Rivalry = SimMath.Clamp01(relation.Rivalry + _rules.HarmRivalry);
                break;
            case DiplomaticAct.Converted:
                relation.Rivalry = SimMath.Clamp01(relation.Rivalry + _rules.HarmRivalry);
                relation.Suspicion = SimMath.Clamp01(relation.Suspicion + (_rules.HarmRivalry / 2f));
                break;
            case DiplomaticAct.Betrayed:
                relation.Trust = SimMath.Clamp01(relation.Trust - _rules.BetrayalTrustLoss);
                relation.Suspicion = SimMath.Clamp01(relation.Suspicion + _rules.BetrayalTrustLoss);
                break;
            case DiplomaticAct.DeclaredWar:
                relation.Trust = SimMath.Clamp01(relation.Trust - _rules.HarmTrustLoss);
                relation.Rivalry = SimMath.Clamp01(relation.Rivalry + (2f * _rules.HarmRivalry));
                break;
            case DiplomaticAct.Allied:
            case DiplomaticAct.MadePeace:
                relation.Trust = SimMath.Clamp01(relation.Trust + _rules.HelpGratitude);
                break;
        }

        relation.Memories.Add(new DiplomaticMemory { Tick = world.Tick, Act = act, SettlementId = settlementId, Weight = DiplomacyModel.InitialWeight(act) });
        if (relation.Memories.Count > _rules.MemoryCapacity)
        {
            var weakest = 0;
            for (var i = 1; i < relation.Memories.Count; i++)
            {
                if (relation.Memories[i].Weight < relation.Memories[weakest].Weight)
                {
                    weakest = i;
                }
            }

            relation.Memories.RemoveAt(weakest);
        }
    }
}
