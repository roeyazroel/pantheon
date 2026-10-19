using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Culture;

/// <summary>
/// What culture adds when people witness a miracle, and what the world notes of the god that cast
/// it (spec §3.2, §7, §8, §33, §34, ADR-024). Called by the miracle caster.
/// </summary>
public static class WitnessCulture
{
    // A miracle answers a need from this much of it.
    private const float Needed = 0.3f;

    // A god's own followers count among the struck when they are at least this share of a settlement.
    private const float OwnPeople = 0.2f;

    /// <summary>
    /// After a settlement's witnesses took a miracle in: help in need breeds dependency and charity,
    /// blessings of the land nature worship, and harm to the god's own followers resentment (and,
    /// among the fearful, sacrifice).
    /// </summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="settlement">The witnesses' settlement.</param>
    /// <param name="belief">Their belief in the caster, after the impulse.</param>
    /// <param name="miracle">The miracle.</param>
    /// <param name="need">How much they needed it (0..1).</param>
    /// <param name="closeness">How close they were (0..1).</param>
    public static void Witnessed(CultureRules rules, SettlementState settlement, GodBelief belief, MiracleDefinition miracle, float need, float closeness)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(miracle);
        var culture = settlement.Culture;
        var nudge = rules.DoctrineFromMiracle * closeness;
        if (CultureModel.Harms(miracle))
        {
            if (belief.Adherence >= OwnPeople)
            {
                var outrage = CultureModel.OutrageFactor(rules, culture, miracle);
                belief.Resentment = BeliefModel.ApplyChange(belief.Resentment, rules.ResentmentFromHarm * closeness * outrage);
                culture.Sacrifice = SimMath.Clamp01(culture.Sacrifice + (nudge * belief.Fear));
            }

            return;
        }

        if (miracle.Gratitude != MiracleNeed.None && need >= Needed)
        {
            belief.Dependency = BeliefModel.ApplyChange(belief.Dependency, rules.DependencyFromHelp * need * closeness);
            culture.Charity = SimMath.Clamp01(culture.Charity + nudge);
        }

        if (CultureModel.Blesses(miracle))
        {
            culture.NatureWorship = SimMath.Clamp01(culture.NatureWorship + nudge);
        }
    }

    /// <summary>The extra doubt betraying love brings when the god was known for mercy (§34): the deeper the contradiction, the deeper the shock.</summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="settlement">The witnesses' settlement.</param>
    /// <param name="miracle">The harmful miracle.</param>
    /// <returns>Factor on the doubt of betrayed love.</returns>
    public static float BetrayalFactor(CultureRules rules, WorldState world, int godId, SettlementState settlement, MiracleDefinition miracle)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var god = GodStanding.Find(world, godId);
        var known = god is not null && (god.Traits.Contains(GodTrait.Compassionate) || god.Traits.Contains(GodTrait.Merciful)) ? rules.Contradiction : 0f;
        return (1f + known) * CultureModel.OutrageFactor(rules, settlement.Culture, miracle);
    }

    /// <summary>
    /// What the world notes of a cast: help or gifts, harm (and cruelty to its own, vengeance on the
    /// guilty), and upheaval. Unseen miracles go unnoted.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The caster.</param>
    /// <param name="miracle">The miracle.</param>
    /// <param name="affected">The settlements it reached.</param>
    /// <param name="needs">How much each needed help.</param>
    /// <param name="deaths">People it killed.</param>
    /// <param name="isGuilty">Whether a god wronged the caster.</param>
    public static void NoteCast(WorldState world, int godId, MiracleDefinition miracle, IReadOnlyList<int> affected, IReadOnlyDictionary<int, float> needs, int deaths, Func<int, bool> isGuilty)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(miracle);
        ArgumentNullException.ThrowIfNull(isGuilty);
        if (!CultureModel.Harms(miracle))
        {
            var helped = false;
            foreach (var id in affected)
            {
                helped |= needs.TryGetValue(id, out var need) && need >= Needed;
            }

            ConductModel.Note(world, godId, helped ? ConductModel.Act.Help : ConductModel.Act.Gift, helped ? 1f : 0.5f);
            return;
        }

        ConductModel.Note(world, godId, ConductModel.Act.Harm, 1f + (0.05f * deaths));
        if (CultureModel.Ravages(miracle))
        {
            ConductModel.Note(world, godId, ConductModel.Act.Upheaval);
        }

        bool own = false, guilty = false;
        foreach (var id in affected)
        {
            if (world.Settlements.Find(s => s.Id == id) is not { } settlement)
            {
                continue;
            }

            foreach (var belief in settlement.Beliefs)
            {
                own |= belief.GodId == godId && belief.Adherence >= OwnPeople;
                guilty |= belief.GodId != godId && belief.Adherence >= OwnPeople && isGuilty(belief.GodId);
            }
        }

        if (own)
        {
            ConductModel.Note(world, godId, ConductModel.Act.Cruelty);
        }

        if (guilty)
        {
            ConductModel.Note(world, godId, ConductModel.Act.Vengeance);
        }
    }
}
