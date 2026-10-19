using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;

namespace Pantheon.Simulation.Belief;

/// <summary>
/// The rules of belief and worship as pure functions (spec §7–9, ADR-009). <see cref="BeliefSystem"/>
/// applies them once per belief step; readouts use them to show what the next step does.
/// </summary>
public static class BeliefModel
{
    /// <summary>Share of the people above which a god holds a settlement's majority.</summary>
    public const float MajorityShare = 0.5f;

    /// <summary>How the settlement is faring: its hardship, comfort and needs.</summary>
    /// <param name="rules">Divine rules, for the food the people want in store and the worry drought and danger bring.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <param name="hazards">Its drought and danger (<see cref="Hazards.Of"/>).</param>
    /// <returns>The conditions.</returns>
    public static SettlementConditions AssessConditions(DivineRules rules, SettlementState settlement, Hazards hazards)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var wanted = Math.Max(0, settlement.Population) * rules.ComfortFoodPerPerson;
        var security = wanted > 0f ? SimMath.Clamp01(settlement.Food / wanted) : 1f;
        var health = SimMath.Clamp01(settlement.Health);
        var foodNeed = 1f - security;
        var healthNeed = 1f - health;
        var waterNeed = SimMath.Clamp01(hazards.Drought);
        var dangerNeed = SimMath.Clamp01(hazards.Danger);
        var worry = Math.Max(rules.DroughtWorry * waterNeed, rules.DangerWorry * dangerNeed);
        var hardship = Math.Max(Math.Max(healthNeed, foodNeed), worry);
        return new SettlementConditions(hardship, health * security, foodNeed, healthNeed, waterNeed, dangerNeed);
    }

    /// <summary>The settlement's need that a miracle answers (0..1).</summary>
    /// <param name="conditions">The settlement's conditions.</param>
    /// <param name="need">The need.</param>
    /// <returns>0 when the need is fully met, 1 when it is dire; 1 for <see cref="MiracleNeed.None"/>.</returns>
    public static float NeedFor(SettlementConditions conditions, MiracleNeed need) => need switch
    {
        MiracleNeed.None => 1f,
        MiracleNeed.Food => conditions.FoodNeed,
        MiracleNeed.Health => conditions.HealthNeed,
        MiracleNeed.Water => conditions.WaterNeed,
        MiracleNeed.Danger => conditions.DangerNeed,
    };

    /// <summary>
    /// One step of drift for one belief, from its values before the step: Faith grows with worship,
    /// erodes with Doubt and is forgotten by those who do not follow the god; Love grows with comfort among followers, fades and wears down in
    /// hardship; Fear fades in safety; Doubt drifts towards <see cref="DivineRules.SilenceDoubt"/>
    /// and rises among followers in hardship. Adherence is left to <see cref="ComputeAdherenceChanges"/>.
    /// </summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="belief">Belief before the step.</param>
    /// <param name="conditions">The settlement's conditions.</param>
    /// <returns>Faith, Love, Fear and Doubt after the step.</returns>
    public static (float Faith, float Love, float Fear, float Doubt) Drift(DivineRules rules, GodBelief belief, SettlementConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(belief);
        var adherence = SimMath.Clamp01(belief.Adherence);
        var faith = SimMath.Clamp01(belief.Faith);
        var love = SimMath.Clamp01(belief.Love);
        var fear = SimMath.Clamp01(belief.Fear);
        var doubt = SimMath.Clamp01(belief.Doubt);
        var hardship = SimMath.Clamp01(conditions.Hardship);

        var newFaith = faith
            + (rules.FaithFromWorship * adherence * (1f - faith))
            - (rules.FaithErosionFromDoubt * doubt * faith)
            - (rules.FaithForgettingPerStep * (1f - adherence) * faith);
        var newLove = love
            + (rules.LoveFromComfort * SimMath.Clamp01(conditions.Comfort) * adherence * (1f - love))
            - (rules.LoveDecayPerStep * love)
            - (rules.LoveLossFromHardship * hardship * adherence * love);
        var newFear = fear - (rules.FearDecayPerStep * (1f - hardship) * fear);
        var newDoubt = doubt
            + (rules.DoubtDriftPerStep * (rules.SilenceDoubt - doubt))
            + (rules.DoubtFromHardship * hardship * adherence * (1f - doubt));
        return (SimMath.Clamp01(newFaith), SimMath.Clamp01(newLove), SimMath.Clamp01(newFear), SimMath.Clamp01(newDoubt));
    }

    /// <summary>How strongly a god draws people who follow no god: Faith times the pull of Love and Fear, held back by Doubt.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="belief">Belief.</param>
    /// <returns>Pull (0 and up).</returns>
    public static float ComputePull(DivineRules rules, GodBelief belief)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(belief);
        var attraction = rules.BaseConversion + (rules.LoveConversion * SimMath.Clamp01(belief.Love)) + (rules.FearConversion * SimMath.Clamp01(belief.Fear));
        return SimMath.Clamp01(belief.Faith) * attraction * (1f - SimMath.Clamp01(belief.Doubt));
    }

    /// <summary>How strongly a god's followers are pushed away: Doubt, made worse by Fear and held back by Love.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="belief">Belief.</param>
    /// <returns>Push (0 and up).</returns>
    public static float ComputePush(DivineRules rules, GodBelief belief)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(belief);
        var fragility = rules.BaseApostasy + (rules.FearFragility * SimMath.Clamp01(belief.Fear));
        var loyalty = 1f - (rules.LoveLoyalty * SimMath.Clamp01(belief.Love));
        return SimMath.Clamp01(belief.Doubt) * fragility * Math.Max(0f, loyalty);
    }

    /// <summary>
    /// Changes in every god's adherence for one step, from the values before it. Each god converts
    /// <c>ConversionPerStep × pull</c> of the people who follow no god and loses
    /// <c>ApostasyPerStep × push</c> of its followers. When the gods together would convert more
    /// people than are free, their gains are scaled down so the shares never exceed 1. The people
    /// who leave a god go to the other gods in proportion to their pull, or stay without a god in
    /// proportion to <c>ApostateFreedom</c> (spec §32): conversion between gods.
    /// </summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="beliefs">The settlement's beliefs.</param>
    /// <param name="pullFactors">A factor on each god's pull, in the same order (for example a people at war resisting the enemy); 1 for all when <c>null</c>.</param>
    /// <returns>The change for each belief, in the same order.</returns>
    public static float[] ComputeAdherenceChanges(DivineRules rules, IReadOnlyList<GodBelief> beliefs, IReadOnlyList<float>? pullFactors = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(beliefs);
        var free = 1f - TotalAdherence(beliefs);
        var pulls = new float[beliefs.Count];
        var totalPull = 0f;
        for (var i = 0; i < beliefs.Count; i++)
        {
            pulls[i] = ComputePull(rules, beliefs[i]) * (pullFactors is null ? 1f : Math.Max(0f, pullFactors[i]));
            totalPull += pulls[i];
        }

        var gains = new float[beliefs.Count];
        var totalGain = 0f;
        for (var i = 0; i < beliefs.Count; i++)
        {
            gains[i] = rules.ConversionPerStep * pulls[i] * Math.Max(0f, free);
            totalGain += gains[i];
        }

        var scale = totalGain > free && totalGain > 0f ? Math.Max(0f, free) / totalGain : 1f;

        var changes = new float[beliefs.Count];
        for (var i = 0; i < beliefs.Count; i++)
        {
            changes[i] += gains[i] * scale;
            var loss = Math.Min(rules.ApostasyPerStep * ComputePush(rules, beliefs[i]) * SimMath.Clamp01(beliefs[i].Adherence), SimMath.Clamp01(beliefs[i].Adherence));
            changes[i] -= loss;
            var choice = rules.ApostateFreedom + (totalPull - pulls[i]);
            if (loss <= 0f || choice <= 0f)
            {
                continue;
            }

            for (var j = 0; j < beliefs.Count; j++)
            {
                if (j != i)
                {
                    changes[j] += loss * pulls[j] / choice;
                }
            }
        }

        return changes;
    }

    /// <summary>Sum of every god's adherence in a settlement.</summary>
    /// <param name="beliefs">The settlement's beliefs.</param>
    /// <returns>Total share of the people who follow a god (0..1 in a valid world).</returns>
    public static float TotalAdherence(IReadOnlyList<GodBelief> beliefs)
    {
        ArgumentNullException.ThrowIfNull(beliefs);
        var total = 0f;
        foreach (var belief in beliefs)
        {
            total += SimMath.Clamp01(belief.Adherence);
        }

        return total;
    }

    /// <summary>How hard followers worship: a base, raised by Love and more by Fear.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="belief">Belief.</param>
    /// <returns>Worship intensity.</returns>
    public static float ComputeWorshipIntensity(DivineRules rules, GodBelief belief)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(belief);
        return rules.BaseWorshipIntensity + (rules.LoveWorshipIntensity * SimMath.Clamp01(belief.Love)) + (rules.FearWorshipIntensity * SimMath.Clamp01(belief.Fear));
    }

    /// <summary>
    /// Workers who worship one god in a step: the workforce times <c>WorshipWorkforceShare</c> times
    /// the god's adherence and Faith. Their sum over the gods is the settlement's
    /// <see cref="Settlements.LabourAllocation.Worshippers"/>.
    /// </summary>
    /// <param name="settlementRules">Settlement rules.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <param name="belief">The god's belief there.</param>
    /// <returns>Worshippers.</returns>
    public static float ComputeWorshippers(SettlementRules settlementRules, SettlementState settlement, GodBelief belief)
    {
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(belief);
        var workers = Math.Max(0, settlement.Population) * settlementRules.WorkforceShare;
        return workers * settlementRules.WorshipWorkforceShare * SimMath.Clamp01(belief.Adherence) * SimMath.Clamp01(belief.Faith);
    }

    /// <summary>
    /// Divine Power one settlement raises for one god in a step (spec §9): worshippers (followers ×
    /// Faith) × worship intensity × religious infrastructure × <c>PowerPerWorshipperPerStep</c>.
    /// </summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="settlementRules">Settlement rules.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <param name="belief">The god's belief there.</param>
    /// <returns>Divine Power per step.</returns>
    public static float ComputePower(DivineRules rules, SettlementRules settlementRules, SettlementState settlement, GodBelief belief)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return ComputeWorshippers(settlementRules, settlement, belief)
            * ComputeWorshipIntensity(rules, belief)
            * rules.ShrineInfrastructure
            * rules.PowerPerWorshipperPerStep;
    }

    /// <summary>Applies one value of an impulse: a positive change closes that share of the gap to 1, a negative one removes that share of the value.</summary>
    /// <param name="value">Current value (0..1).</param>
    /// <param name="change">Change (-1..1), already weighted by closeness.</param>
    /// <returns>New value (0..1).</returns>
    public static float ApplyChange(float value, float change)
    {
        var current = SimMath.Clamp01(value);
        var delta = Math.Clamp(change, -1f, 1f);
        return SimMath.Clamp01(delta >= 0f ? current + (delta * (1f - current)) : current + (delta * current));
    }

    /// <summary>Applies a divine act's impulse to one belief.</summary>
    /// <param name="belief">Belief to change.</param>
    /// <param name="impulse">The act's impulse.</param>
    /// <param name="weight">Closeness of the witnesses (0..1).</param>
    /// <param name="loveScale">Share of the Love impulse that applies, for acts that answer a need (0..1).</param>
    public static void ApplyImpulse(GodBelief belief, BeliefImpulse impulse, float weight, float loveScale)
    {
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(impulse);
        var w = SimMath.Clamp01(weight);
        var love = impulse.Love > 0f ? impulse.Love * SimMath.Clamp01(loveScale) : impulse.Love;
        belief.Faith = ApplyChange(belief.Faith, impulse.Faith * w);
        belief.Love = ApplyChange(belief.Love, love * w);
        belief.Fear = ApplyChange(belief.Fear, impulse.Fear * w);
        belief.Doubt = ApplyChange(belief.Doubt, impulse.Doubt * w);
    }

    /// <summary>The settlement's belief in a god, if its people know of it.</summary>
    /// <param name="settlement">Settlement state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The belief, or <c>null</c>.</returns>
    public static GodBelief? Find(SettlementState settlement, int godId)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return Find(settlement.Beliefs, godId);
    }

    /// <summary>The belief in a god among a population's beliefs (a settlement's or a migration group's), if any.</summary>
    /// <param name="beliefs">The beliefs.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The belief, or <c>null</c>.</returns>
    public static GodBelief? Find(IReadOnlyList<GodBelief> beliefs, int godId)
    {
        ArgumentNullException.ThrowIfNull(beliefs);
        foreach (var belief in beliefs)
        {
            if (belief.GodId == godId)
            {
                return belief;
            }
        }

        return null;
    }

    /// <summary>The settlement's belief in a god, created (with no followers yet) when its people first hear of it.</summary>
    /// <param name="rules">Divine rules, for the doubt of new believers.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The existing or new belief.</returns>
    public static GodBelief FindOrAdd(DivineRules rules, SettlementState settlement, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (Find(settlement, godId) is { } existing)
        {
            return existing;
        }

        var belief = new GodBelief { GodId = godId, Adherence = 0f, Faith = 0f, Love = 0f, Fear = 0f, Doubt = SimMath.Clamp01(rules.NewBelieverDoubt) };
        var index = 0;
        while (index < settlement.Beliefs.Count && settlement.Beliefs[index].GodId < godId)
        {
            index++;
        }

        settlement.Beliefs.Insert(index, belief);
        return belief;
    }
}
