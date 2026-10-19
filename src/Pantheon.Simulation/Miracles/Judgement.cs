using System;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Miracles;

/// <summary>The Justice domain's judgement (ADR-022): who wronged a god, and the punishment of their followers.</summary>
internal sealed class Judgement
{
    private readonly Domain.Gods.DomainRules? _domains;

    /// <summary>Creates the judgement over the domain rules, whose Justice boon sets the grudge that makes a god guilty.</summary>
    /// <param name="domains">Domain rules; without them only war makes a god guilty.</param>
    public Judgement(Domain.Gods.DomainRules? domains) => _domains = domains;

    /// <summary>Whether a god wronged the caster: they are at war, or the caster bears it a grudge of at least the Justice domain's measure (ADR-022).</summary>
    public bool IsGuilty(WorldState world, int casterId, int godId)
    {
        if (godId == casterId || DiplomacyModel.AreAllied(world, casterId, godId))
        {
            return false;
        }

        if (DiplomacyModel.AreAtWar(world, casterId, godId))
        {
            return true;
        }

        return _domains is not null && DiplomacyModel.FindRelation(world, casterId, godId) is { } view && DiplomacyModel.Grudge(view) >= _domains.Boons.JusticeGuiltGrudge;
    }

    /// <summary>Whether a settlement's people follow, in any number, a god who wronged the caster.</summary>
    public bool HasGuilty(WorldState world, int casterId, SettlementState settlement)
    {
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence > 0.05f && IsGuilty(world, casterId, belief.GodId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The followers of the gods who wronged the caster are punished: some die, and the rest doubt their god.</summary>
    /// <returns>People killed.</returns>
    public int Judge(WorldState world, SettlementState settlement, int casterId, float amount)
    {
        var deaths = 0;
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence <= 0.05f || !IsGuilty(world, casterId, belief.GodId))
            {
                continue;
            }

            var killed = Math.Min(settlement.Population, (int)MathF.Round(amount * settlement.Population * SimMath.Clamp01(belief.Adherence)));
            settlement.Population -= killed;
            deaths += killed;
            belief.Faith = BeliefModel.ApplyChange(belief.Faith, -amount);
            belief.Doubt = BeliefModel.ApplyChange(belief.Doubt, amount);
        }

        return deaths;
    }
}
