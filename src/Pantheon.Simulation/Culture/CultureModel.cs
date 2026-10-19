using System;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Culture;

/// <summary>
/// The rules of culture (spec §7, §8, §33, §35, ADR-024) as pure functions: the deeper feelings of
/// belief, what each doctrine tends to, how far a settlement turns from the gods, and the factors
/// they put on worship, conversion and miracles.
/// </summary>
public static class CultureModel
{
    /// <summary>A settlement's stage of turning from the gods.</summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="secularism">Its secularism (0..1).</param>
    /// <returns>The stage.</returns>
    public static SecularStage Stage(CultureRules rules, float secularism)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return secularism >= rules.AntiDivine ? SecularStage.AntiDivine
            : secularism >= rules.Atheist ? SecularStage.Atheist
            : secularism >= rules.Agnostic ? SecularStage.Agnostic
            : SecularStage.Devout;
    }

    /// <summary>
    /// One step of a belief's dependency and resentment (§7, §8): fear breeds resentment, love
    /// dissolves it, sacrifice softens it; dependency fades in comfort.
    /// </summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="belief">The belief.</param>
    /// <param name="culture">The settlement's culture.</param>
    public static void DriftFeelings(CultureRules rules, GodBelief belief, CultureState culture)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(culture);
        var resentment = SimMath.Clamp01(belief.Resentment);
        var appeased = 1f - (rules.DoctrineEffect * culture.Sacrifice);
        resentment += rules.ResentmentFromFear * SimMath.Clamp01(belief.Fear) * (1f - resentment) * appeased;
        resentment -= rules.ResentmentFromLove * SimMath.Clamp01(belief.Love) * resentment;
        belief.Resentment = SimMath.Clamp01(resentment);
        belief.Dependency = SimMath.Clamp01(belief.Dependency * (1f - rules.DependencyFade));
    }

    /// <summary>Extra doubt in hardship among the dependent, who expect their god to protect them (§8).</summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="belief">The belief.</param>
    /// <param name="hardship">The settlement's hardship (0..1).</param>
    /// <returns>Doubt to add this step.</returns>
    public static float Expectation(CultureRules rules, GodBelief belief, float hardship)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(belief);
        return rules.DependencyExpectation * SimMath.Clamp01(belief.Dependency) * SimMath.Clamp01(hardship) * (1f - SimMath.Clamp01(belief.Doubt));
    }

    /// <summary>
    /// Factor on the Divine Power a belief raises: the dependent and those who sacrifice in fear
    /// worship harder; a secular settlement worships little (§35).
    /// </summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="belief">The belief.</param>
    /// <param name="culture">The settlement's culture.</param>
    /// <returns>The factor.</returns>
    public static float WorshipFactor(CultureRules rules, GodBelief belief, CultureState culture)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(belief);
        ArgumentNullException.ThrowIfNull(culture);
        var harder = 1f + (rules.DependencyWorship * SimMath.Clamp01(belief.Dependency)) + (rules.SacrificeWorship * culture.Sacrifice * SimMath.Clamp01(belief.Fear));
        return harder * (1f - (rules.SecularPowerLoss * SimMath.Clamp01(culture.Secularism)));
    }

    /// <summary>Factor on a god's pull in a settlement: secular people resist every god, isolationists every god but the one most of them follow.</summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="culture">The settlement's culture.</param>
    /// <param name="isMain">Whether the god is the one most of its people follow.</param>
    /// <returns>The factor (0..1).</returns>
    public static float PullFactor(CultureRules rules, CultureState culture, bool isMain)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(culture);
        var secular = 1f - (rules.SecularResistance * SimMath.Clamp01(culture.Secularism));
        var closed = isMain ? 1f : 1f - (rules.DoctrineEffect * culture.Isolationism);
        return Math.Max(0f, secular * closed);
    }

    /// <summary>
    /// Factor on what a miracle does to its witnesses' belief: secular people are moved less, nature
    /// worshippers more by blessings of the land.
    /// </summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="culture">The witnesses' culture.</param>
    /// <param name="miracle">The miracle.</param>
    /// <returns>The factor.</returns>
    public static float WitnessFactor(CultureRules rules, CultureState culture, MiracleDefinition miracle)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(miracle);
        var resisted = 1f - (rules.MiracleResistance * SimMath.Clamp01(culture.Secularism));
        var land = Blesses(miracle) ? 1f + (rules.DoctrineEffect * culture.NatureWorship) : 1f;
        return Math.Max(0f, resisted * land);
    }

    /// <summary>Factor on the love a miracle earns from those who needed it: charity values help.</summary>
    public static float GratitudeFactor(CultureRules rules, CultureState culture) => 1f + (rules.DoctrineEffect * culture.Charity);

    /// <summary>
    /// Factor on the doubt and resentment harm by a god stirs among its own: pacifists take it hard,
    /// nature worshippers hate the land burned or shaken, militarists accept force.
    /// </summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="culture">The witnesses' culture.</param>
    /// <param name="miracle">The harmful miracle.</param>
    /// <returns>The factor.</returns>
    public static float OutrageFactor(CultureRules rules, CultureState culture, MiracleDefinition miracle)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(culture);
        var land = Ravages(miracle) ? rules.DoctrineEffect * culture.NatureWorship : 0f;
        return Math.Max(0f, 1f + (rules.DoctrineEffect * culture.Pacifism) + land - (rules.DoctrineEffect * culture.Militarism));
    }

    /// <summary>Factor on how fast a settlement learns: knowledge seeking and secular reason (§35's science bonuses).</summary>
    public static float LearningFactor(CultureRules rules, CultureState culture) =>
        1f + (rules.DoctrineEffect * culture.KnowledgeSeeking) + (rules.SecularScience * SimMath.Clamp01(culture.Secularism));

    /// <summary>Factor on the people a settlement needs before it sends settlers: expansionists go sooner.</summary>
    public static float FoundingFactor(CultureRules rules, CultureState culture) => 1f - (0.5f * rules.DoctrineEffect * culture.Expansionism);

    /// <summary>Factor on how strongly a settlement draws migrants: isolationists keep strangers out.</summary>
    public static float WelcomeFactor(CultureRules rules, CultureState culture) => Math.Max(0f, 1f - (rules.DoctrineEffect * culture.Isolationism));

    /// <summary>Whether a miracle blesses the land: blessing, rain, water, regrowth.</summary>
    public static bool Blesses(MiracleDefinition miracle)
    {
        ArgumentNullException.ThrowIfNull(miracle);
        foreach (var effect in miracle.Effects)
        {
            if (effect.Kind is MiracleEffectKind.Bless or MiracleEffectKind.Rain or MiracleEffectKind.Regrow or MiracleEffectKind.Soak or MiracleEffectKind.Water)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a miracle ravages the land: fire, quakes, floods.</summary>
    public static bool Ravages(MiracleDefinition miracle)
    {
        ArgumentNullException.ThrowIfNull(miracle);
        foreach (var effect in miracle.Effects)
        {
            if (effect.Kind is MiracleEffectKind.Ignite or MiracleEffectKind.Quake or MiracleEffectKind.Flood)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a miracle harms people: it strikes, burns, shakes, drowns, judges or terrifies.</summary>
    public static bool Harms(MiracleDefinition miracle)
    {
        ArgumentNullException.ThrowIfNull(miracle);
        foreach (var effect in miracle.Effects)
        {
            if (effect.Kind is MiracleEffectKind.Smite or MiracleEffectKind.Ignite or MiracleEffectKind.Quake or MiracleEffectKind.Flood
                or MiracleEffectKind.Judge or MiracleEffectKind.Terrify)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// What each doctrine tends to in a settlement (§33), from what its people live through and the
    /// god most of them follow: peace and safety make pacifists, war militarists, fear sacrificers,
    /// a loving and helpful god the charitable, a blessed land and a Nature god nature worshippers,
    /// learning seekers of knowledge, crowding and expansion expansionists, and isolation isolationists.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="war">War over it (0..1).</param>
    /// <param name="neighbours">Living settlements within trade range.</param>
    /// <param name="blessing">Mean blessing of its farmland (0..1).</param>
    /// <param name="doctrine">The doctrine.</param>
    /// <returns>The hold it tends to (0..1).</returns>
    public static float DoctrineTarget(WorldState world, SettlementState settlement, float war, int neighbours, float blessing, Doctrine doctrine)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        var main = Main(settlement);
        var god = main is null ? null : GodStanding.Find(world, main.GodId);
        bool Has(GodTrait trait) => god is not null && god.Traits.Contains(trait);
        bool Holds(GodDomain domain) => god is not null && god.Domains.Contains(domain);
        var society = settlement.Society;
        var crowding = settlement.Housing > 0 ? SimMath.Clamp01((float)settlement.Population / settlement.Housing) : 0f;
        var target = doctrine switch
        {
            Doctrine.Pacifism => (0.45f * (1f - war) * society.Safety) + (Has(GodTrait.Merciful) || Has(GodTrait.Compassionate) ? 0.3f : 0f),
            Doctrine.Militarism => (0.7f * war) + (society.Priority == SettlementPriority.Defence ? 0.2f : 0f) + (Holds(GodDomain.War) || Has(GodTrait.Ambitious) ? 0.3f : 0f),
            Doctrine.Sacrifice => (0.8f * (main?.Fear ?? 0f)) + (Holds(GodDomain.Death) ? 0.2f : 0f),
            Doctrine.Charity => (0.6f * (main?.Love ?? 0f)) + (Has(GodTrait.Compassionate) || Has(GodTrait.Generous) ? 0.3f : 0f),
            Doctrine.NatureWorship => (0.6f * blessing) + (Holds(GodDomain.Nature) ? 0.4f : 0f),
            Doctrine.KnowledgeSeeking => (0.8f * society.Education) + (Holds(GodDomain.Knowledge) ? 0.3f : 0f),
            Doctrine.Expansionism => (society.Priority == SettlementPriority.Expansion ? 0.5f : 0f) + (0.3f * crowding) + (Has(GodTrait.Ambitious) ? 0.2f : 0f),
            Doctrine.Isolationism => (0.3f * (1f - Math.Min(1f, neighbours / 4f))) + (god?.RefugeePolicy == RefugeePolicy.TurnAway ? 0.4f : 0f) + (0.2f * war),
        };
        return SimMath.Clamp01(target);
    }

    /// <summary>
    /// The secularism a settlement tends to (§35): doubt, people without a god, an Old Faith,
    /// resentment and science turn it from the gods; faith among its followers holds it.
    /// </summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The target (0..1).</returns>
    public static float SecularTarget(CultureRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var weights = rules.Secular;
        float followed = 0f, doubt = 0f, faith = 0f, resentment = 0f;
        foreach (var belief in settlement.Beliefs)
        {
            var share = SimMath.Clamp01(belief.Adherence);
            followed += share;
            doubt += share * SimMath.Clamp01(belief.Doubt);
            faith += share * SimMath.Clamp01(belief.Faith);
            resentment += share * SimMath.Clamp01(belief.Resentment);
        }

        var godless = SimMath.Clamp01(1f - followed);
        var schism = settlement.OldFaith?.Share ?? 0f;
        var reason = (weights.Science * settlement.Society.Technology.Science) + (0.2f * settlement.Culture.KnowledgeSeeking);
        return SimMath.Clamp01((weights.Doubt * doubt) + (weights.Godless * godless) + (weights.Schism * schism) + (weights.Resentment * resentment) + reason - (weights.Faith * faith));
    }

    /// <summary>The belief most of a settlement follows, if any god holds the largest share.</summary>
    public static GodBelief? Main(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        GodBelief? main = null;
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence > 0.05f && (main is null || belief.Adherence > main.Adherence))
            {
                main = belief;
            }
        }

        return main;
    }
}
