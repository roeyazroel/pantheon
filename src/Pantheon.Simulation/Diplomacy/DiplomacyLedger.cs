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
            case DiplomaticAct.Sheltered:
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
            case DiplomaticAct.TookHolySite:
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
            case DiplomaticAct.WrongedAlly:
            case DiplomaticAct.TurnedAway:
            case DiplomaticAct.CreatureDefeated:
            case DiplomaticAct.VotedAgainst:
                relation.Trust = SimMath.Clamp01(relation.Trust - (_rules.HarmTrustLoss / 2f));
                relation.Rivalry = SimMath.Clamp01(relation.Rivalry + (_rules.HarmRivalry / 2f));
                break;
            case DiplomaticAct.Rescued:
                relation.Gratitude = SimMath.Clamp01(relation.Gratitude + (2f * _rules.HelpGratitude));
                Politics.FavourBook.Owe(_rules.Politics, world, actorGodId, observerGodId, settlementId);
                break;
            case DiplomaticAct.BrokeTreaty:
                relation.Trust = SimMath.Clamp01(relation.Trust - (_rules.BetrayalTrustLoss * _rules.Politics.TreatyBetrayalShare));
                relation.Suspicion = SimMath.Clamp01(relation.Suspicion + (_rules.BetrayalTrustLoss * _rules.Politics.TreatyBetrayalShare));
                break;
            case DiplomaticAct.BrokePromise:
                relation.Trust = SimMath.Clamp01(relation.Trust - _rules.Politics.BrokenPromiseTrustLoss);
                break;
            case DiplomaticAct.DefiedCouncil:
                relation.Trust = SimMath.Clamp01(relation.Trust - _rules.Politics.DefianceTrustLoss);
                break;
            case DiplomaticAct.KeptWord:
            case DiplomaticAct.SignedTreaty:
                relation.Trust = SimMath.Clamp01(relation.Trust + (_rules.HelpGratitude / 2f));
                break;
            case DiplomaticAct.HolyWar:
            case DiplomaticAct.Dictated:
                relation.Trust = SimMath.Clamp01(relation.Trust - (_rules.HarmTrustLoss / 2f));
                relation.Rivalry = SimMath.Clamp01(relation.Rivalry + _rules.HarmRivalry);
                break;
            case DiplomaticAct.CovertAct:
            case DiplomaticAct.PlottedAgainst:
                relation.Trust = SimMath.Clamp01(relation.Trust - _rules.HarmTrustLoss);
                relation.Suspicion = SimMath.Clamp01(relation.Suspicion + (_rules.BetrayalTrustLoss / 2f));
                break;
        }

        Remember(relation, world.Tick, act, settlementId);
        Conflict.WarBook.Score(_rules.Conflict, world, actorGodId, observerGodId, act);
        if (act is DiplomaticAct.Harmed or DiplomaticAct.CreatureAttacked or DiplomaticAct.Converted or DiplomaticAct.Betrayed or DiplomaticAct.DeclaredWar or DiplomaticAct.BrokeTreaty)
        {
            TellAllies(world, observerGodId, actorGodId, settlementId);
        }
    }

    /// <summary>
    /// The victim's allies note the wrong too, at half strength (ADR-015): a god remembers who
    /// attacked its friends.
    /// </summary>
    private void TellAllies(WorldState world, int victimGodId, int actorGodId, int? settlementId)
    {
        foreach (var ally in DiplomacyModel.AlliesOf(world, victimGodId))
        {
            if (ally == actorGodId)
            {
                continue;
            }

            var relation = DiplomacyModel.EnsureRelation(_rules, world, ally, actorGodId);
            relation.Trust = SimMath.Clamp01(relation.Trust - (_rules.HarmTrustLoss / 2f));
            relation.Rivalry = SimMath.Clamp01(relation.Rivalry + (_rules.HarmRivalry / 2f));
            Remember(relation, world.Tick, DiplomaticAct.WrongedAlly, settlementId);
        }
    }

    /// <summary>Adds a memory, forgetting the weakest beyond the capacity.</summary>
    private void Remember(GodRelation relation, long tick, DiplomaticAct act, int? settlementId)
    {
        relation.Memories.Add(new DiplomaticMemory { Tick = tick, Act = act, SettlementId = settlementId, Weight = DiplomacyModel.InitialWeight(act) });
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
