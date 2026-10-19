using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.HolySites;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.HolySites;

namespace Pantheon.Simulation.Miracles;

/// <summary>
/// The authority for miracles (spec §73, ADR-009). <see cref="Check"/> answers whether a request
/// may be cast and what it costs; <see cref="Cast"/> checks it again and, when accepted, spends the
/// power, starts the cooldown, applies the effects and lets the witnesses' belief respond. Both
/// are deterministic and draw no random numbers.
/// </summary>
public sealed class MiracleCaster
{
    // A miracle counts as help to a rival's followers only when their food, health or water need is at least this,
    // and as a rescue that makes their god owe a favour from MajorNeed (spec §41).
    private const float NeedForHelp = 0.5f;
    private const float MajorNeed = 0.75f;

    private readonly DivineRules _rules;
    private readonly SettlementRules _settlementRules;
    private readonly MiracleBook _book;
    private readonly IActRecorder? _ledger;
    private readonly DisasterRules? _disasters;
    private readonly DomainRules? _domains;
    private readonly HolySiteRules? _holySites;
    private readonly DiplomacyRules? _diplomacy;
    private readonly MiracleEffects _effects;

    /// <summary>Creates a caster over validated content.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="settlementRules">Settlement rules, for food storage and the drought.</param>
    /// <param name="book">The miracles.</param>
    /// <param name="ledger">Where the gods whose followers a miracle reaches note it (ADR-012); <c>null</c> for none.</param>
    /// <param name="weather">Weather rules, for the clouds Rain calls (ADR-013); <c>null</c> where there is no weather, and Rain then calls none.</param>
    /// <param name="disasters">Disaster rules, for the fires and earthquakes miracles start (ADR-014); <c>null</c> where there are no disasters, and those miracles then start none.</param>
    /// <param name="creatures">Creature rules, for the miracles that reach Creatures (ADR-021); <c>null</c> where there are no Creatures.</param>
    /// <param name="domains">Domain rules, for domain costs and boons (ADR-021); <c>null</c> for none. A signature miracle always needs its domain.</param>
    /// <param name="holySites">Holy site rules, for the cheaper miracles of a site's keeper (ADR-021); <c>null</c> for none.</param>
    /// <param name="diplomacy">Diplomacy rules, for the council's decrees (ADR-025) and veiled miracles (ADR-026); <c>null</c> for none, and miracles cannot be veiled.</param>
    public MiracleCaster(DivineRules rules, SettlementRules settlementRules, MiracleBook book, IActRecorder? ledger = null, WeatherRules? weather = null, DisasterRules? disasters = null, CreatureRules? creatures = null, DomainRules? domains = null, HolySiteRules? holySites = null, DiplomacyRules? diplomacy = null)
    {
        _diplomacy = diplomacy;
        _ledger = ledger;
        _disasters = disasters;
        _domains = domains;
        _holySites = holySites;
        DivineRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(book);
        _rules = rules;
        _settlementRules = settlementRules;
        _book = book;
        _effects = new MiracleEffects(settlementRules, ledger, weather, disasters, creatures, domains);
    }

    /// <summary>The domain rules, if gods have domains (ADR-021).</summary>
    public DomainRules? Domains => _domains;

    /// <summary>
    /// Divine Power a miracle costs: the base cost raised for weak influence, and raised again by how
    /// far the strongest rival's influence there exceeds the caster's (spec §10).
    /// </summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="miracle">The miracle.</param>
    /// <param name="influence">The god's influence at the target (0..1).</param>
    /// <param name="rivalInfluence">The strongest other god's influence at the target (0..1).</param>
    /// <returns>The cost.</returns>
    public static float ComputeCost(DivineRules rules, MiracleDefinition miracle, float influence, float rivalInfluence = 0f)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(miracle);
        var own = SimMath.Clamp01(influence);
        var contest = Math.Max(0f, SimMath.Clamp01(rivalInfluence) - own);
        return miracle.PowerCost * (1f + (rules.WeakInfluenceCostFactor * (1f - own))) * (1f + (rules.ContestedCostFactor * contest));
    }

    /// <summary>The strongest influence at a point of any god other than the given one and its allies.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god whose rivals count.</param>
    /// <param name="target">The point.</param>
    /// <returns>Influence (0..1); 0 without rivals.</returns>
    public float RivalInfluence(WorldState world, int godId, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(world);
        var strongest = 0f;
        foreach (var rival in world.Gods)
        {
            if (rival.Id != godId && !Politics.TreatyModel.SharesInfluence(world, godId, rival.Id))
            {
                strongest = Math.Max(strongest, InfluenceField.Build(_rules, world, rival.Id).At(target));
            }
        }

        return strongest;
    }

    /// <summary>Checks a request in the order of spec §73 without changing the world.</summary>
    /// <param name="world">World state.</param>
    /// <param name="request">The request.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself, which may act for any god.</param>
    /// <returns>The verdict.</returns>
    public CastCheck Check(WorldState world, CastMiracleRequest request, int? issuerGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(request);
        if (GodStanding.Find(world, request.GodId) is not { } god)
        {
            return new CastCheck(CastRejection.UnknownGod, null, 0f, 0f, 0);
        }

        if (_book.Find(request.MiracleId) is not { } miracle)
        {
            return new CastCheck(CastRejection.UnknownMiracle, null, 0f, 0f, 0);
        }

        var cooldown = CooldownTicksLeft(world, god, miracle.Id);
        var inWorld = IsInsideWorld(world.Regions, request.Target);
        var influence = inWorld ? InfluenceField.Build(_rules, world, god.Id).At(request.Target) : 0f;
        var rivals = inWorld ? RivalInfluence(world, god.Id, request.Target) : 0f;
        var reach = Reach(miracle);
        var cost = ComputeCost(_rules, miracle, influence, rivals) * DomainModel.CostFactor(_domains, god, miracle) * HolySiteModel.CostFactor(_holySites, world, god.Id)
            * Politics.CouncilModel.CostFactor(_diplomacy?.Politics, world, miracle, request.Target, reach)
            * (request.Veiled && _diplomacy is not null ? _diplomacy.Conflict.VeilCostFactor : 1f);
        var rejection =
            issuerGodId is { } issuer && issuer != god.Id ? CastRejection.NotAuthorized
            : miracle.RequiresDomain && !DomainModel.Holds(god, miracle.Domain) ? CastRejection.DomainRequired
            : god.Rank < miracle.MinimumRank ? CastRejection.RankTooLow
            : cooldown > 0 ? CastRejection.OnCooldown
            : !inWorld ? CastRejection.OutsideWorld
            : influence < miracle.InfluenceRequirement ? CastRejection.OutsideInfluence
            : !_effects.HasTarget(world, god.Id, miracle, request.Target) ? CastRejection.NothingThere
            : Politics.TreatyModel.ForbiddenLand(world, god.Id, request.Target, reach) is not null ? CastRejection.TreatyForbids
            : god.DivinePower < cost ? CastRejection.NotEnoughPower
            : CastRejection.None;
        return new CastCheck(rejection, miracle, influence, cost, cooldown);
    }

    /// <summary>Checks a request and, when it is accepted, casts the miracle.</summary>
    /// <param name="world">World state to change.</param>
    /// <param name="request">The request.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <param name="events">Receives the <see cref="MiracleCast"/> event of an accepted cast.</param>
    /// <returns>The verdict; the world changed only if it is accepted.</returns>
    public CastCheck Cast(WorldState world, CastMiracleRequest request, int? issuerGodId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var check = Check(world, request, issuerGodId);
        if (!check.IsAccepted || check.Miracle is not { } miracle)
        {
            return check;
        }

        var god = GodStanding.Find(world, request.GodId)!;
        god.DivinePower = Math.Max(0f, god.DivinePower - check.Cost);
        StartCooldown(god, miracle, world.Tick);

        var reach = Reach(miracle);
        var unseen = IsUnseen(miracle);
        var veiled = request.Veiled && _diplomacy is not null;
        var seen = !unseen && !veiled;
        var affected = new List<int>();
        var needs = new Dictionary<int, float>();
        var deaths = 0;
        foreach (var settlement in world.Settlements)
        {
            var distance = Distance(settlement.Position, request.Target);
            var conditions = AssessConditions(world, settlement);
            if (distance <= reach)
            {
                affected.Add(settlement.Id);
                needs[settlement.Id] = Math.Max(Math.Max(conditions.FoodNeed, conditions.HealthNeed), conditions.WaterNeed);
                deaths += _effects.ApplyToSettlement(world, miracle, settlement, god.Id);
            }

            var closeness = distance <= reach ? 1f : SimMath.Clamp01(1f - ((distance - reach) / Math.Max(1f, _rules.WitnessRadius)));
            if (closeness > 0f && settlement.Population > 0 && seen)
            {
                Witness(world, miracle, settlement, god.Id, closeness, conditions);
            }
            else if (veiled && distance <= reach && settlement.Population > 0)
            {
                Conflict.Veil.Witnessed(_diplomacy!.Conflict, settlement, god.Id, miracle);
            }
        }

        var exposed = veiled && !unseen && Conflict.Veil.Exposed(_diplomacy!.Conflict, world, god.Id, miracle, affected);
        if (seen || exposed)
        {
            NoteByRivals(world, miracle, god.Id, affected, needs, events);
            Politics.Defiance.Note(_ledger, world, god.Id, miracle, request.Target, reach, affected, events);
        }

        if (deaths > 0 && _domains is not null && DomainModel.Holds(god, GodDomain.Death))
        {
            god.DivinePower += deaths * _domains.Boons.DeathPowerPerDeath;
        }

        if (seen || exposed)
        {
            Culture.WitnessCulture.NoteCast(world, god.Id, miracle, affected, needs, deaths, other => _effects.IsGuilty(world, god.Id, other));
        }

        if (exposed && Conflict.Veil.Victim(world, god.Id, affected) is { } victim)
        {
            Conflict.Covert.Expose(_ledger, world, god.Id, victim, miracle.Id, affected[0], events);
        }

        MiracleEffects.ApplyToRegions(miracle, world.Regions, request.Target);
        _effects.ApplyToWorld(world, miracle, god.Id, request.Target, affected, events);
        events.Add(new MiracleCast
        {
            Tick = world.Tick,
            GodId = god.Id,
            MiracleId = miracle.Id,
            Target = request.Target,
            Cost = check.Cost,
            AffectedSettlementIds = affected,
            Deaths = deaths,
            Veiled = veiled && !exposed,
        });
        return check;
    }

    /// <summary>How a settlement is faring, the land's hazards included: the conditions that decide how much a miracle is needed.</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Its conditions.</returns>
    public SettlementConditions AssessConditions(WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(world);
        return BeliefModel.AssessConditions(_rules, settlement, Hazards.Of(_settlementRules, world, settlement));
    }

    /// <summary>The settlements a miracle cast at a target would reach: those whose footprint lies within its radius.</summary>
    /// <param name="world">World state.</param>
    /// <param name="miracle">The miracle.</param>
    /// <param name="target">Where it would be cast.</param>
    /// <returns>The settlements, in world order.</returns>
    public IReadOnlyList<SettlementState> FindReached(WorldState world, MiracleDefinition miracle, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(miracle);
        var reach = Reach(miracle);
        return world.Settlements.FindAll(settlement => Distance(settlement.Position, target) <= reach);
    }

    /// <summary>Ticks until a god can cast a miracle again.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <param name="miracleId">The miracle.</param>
    /// <returns>0 when ready.</returns>
    public static long CooldownTicksLeft(WorldState world, GodState god, string miracleId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        foreach (var cooldown in god.Cooldowns)
        {
            if (string.Equals(cooldown.MiracleId, miracleId, StringComparison.Ordinal))
            {
                return Math.Max(0, cooldown.ReadyAtTick - world.Tick);
            }
        }

        return 0;
    }

    /// <summary>Whether nobody sees the miracle worked, as Sow Discord (ADR-021): no witness changes belief and no god notes it.</summary>
    private static bool IsUnseen(MiracleDefinition miracle) => miracle.Effects.Any(effect => effect.Kind == MiracleEffectKind.Discord);

    /// <summary>Distance from the target within which a settlement's centre must lie for the miracle to reach it.</summary>
    private float Reach(MiracleDefinition miracle) => miracle.Radius + _rules.SettlementRadius;

    /// <summary>Whether a point lies on the world's ground plane.</summary>
    private static bool IsInsideWorld(RegionGrid grid, GroundPosition target) => grid.TryGetRegionIdAt(target.X, target.Z, out _);

    /// <summary>Records when the god may cast the miracle again.</summary>
    private static void StartCooldown(GodState god, MiracleDefinition miracle, long tick)
    {
        var ready = tick + miracle.CooldownTicks;
        foreach (var cooldown in god.Cooldowns)
        {
            if (string.Equals(cooldown.MiracleId, miracle.Id, StringComparison.Ordinal))
            {
                cooldown.ReadyAtTick = ready;
                return;
            }
        }

        god.Cooldowns.Add(new MiracleCooldown { MiracleId = miracle.Id, ReadyAtTick = ready });
    }

    /// <summary>Whether a god wronged another, so that Judgement finds its followers guilty (ADR-022).</summary>
    /// <param name="world">World state.</param>
    /// <param name="casterId">The god that judges.</param>
    /// <param name="godId">The god judged.</param>
    /// <returns><c>true</c> when they are at war or the judge bears it a grudge.</returns>
    public bool IsGuilty(WorldState world, int casterId, int godId) => _effects.IsGuilty(world, casterId, godId);

    /// <summary>How readily a settlement's own region would burn now (0 without disaster rules).</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Flammability (0..1).</returns>
    public float FlammabilityAt(WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        return _disasters is null ? 0f : DisasterModel.Flammability(_disasters, world.Regions.Cells[settlement.RegionId]);
    }

    /// <summary>
    /// Lets the people of a settlement who saw the miracle change their belief in the god, and plants
    /// doubt about every other god they follow: another god can work wonders here.
    /// </summary>
    private void Witness(WorldState world, MiracleDefinition miracle, SettlementState settlement, int godId, float closeness, SettlementConditions conditions)
    {
        var need = BeliefModel.NeedFor(conditions, miracle.Gratitude);
        var loveScale = _rules.GratitudeFloor + ((1f - _rules.GratitudeFloor) * need);
        if (need > 0f)
        {
            loveScale = Math.Min(1f, loveScale * Culture.CultureModel.GratitudeFactor(_rules.Culture, settlement.Culture));
        }

        closeness *= Culture.CultureModel.WitnessFactor(_rules.Culture, settlement.Culture, miracle);
        var trickery = _domains is not null && DomainModel.Holds(world, godId, GodDomain.Trickery) ? 1f + _domains.Boons.TrickeryDoubt : 1f;
        foreach (var other in settlement.Beliefs)
        {
            if (other.GodId != godId && other.Adherence > 0f && !DiplomacyModel.AreAllied(world, godId, other.GodId))
            {
                other.Doubt = BeliefModel.ApplyChange(other.Doubt, _rules.RivalMiracleDoubt * trickery * closeness);
            }
        }

        var belief = BeliefModel.FindOrAdd(_rules, settlement, godId);
        var love = belief.Love;
        BeliefModel.ApplyImpulse(belief, miracle.Witness, closeness, loveScale);
        if (miracle.Witness.Love < 0f && love > 0f)
        {
            // A god that harms people who love it contradicts all they knew of it (spec §34), the more so if it was known for mercy (ADR-024).
            var shock = Culture.WitnessCulture.BetrayalFactor(_rules.Culture, world, godId, settlement, miracle);
            belief.Doubt = BeliefModel.ApplyChange(belief.Doubt, _rules.Schism.BetrayalDoubt * love * closeness * shock);
        }

        Culture.WitnessCulture.Witnessed(_rules.Culture, settlement, belief, miracle, need, closeness);
    }

    /// <summary>
    /// Every other god whose followers the miracle reached notes it once: harm when it struck them,
    /// help when they were in real need (spec §41), and courting otherwise (ADR-012).
    /// </summary>
    private void NoteByRivals(WorldState world, MiracleDefinition miracle, int godId, List<int> affected, Dictionary<int, float> needs, ICollection<WorldEvent> events)
    {
        if (_ledger is null || affected.Count == 0)
        {
            return;
        }

        var harms = false;
        foreach (var effect in miracle.Effects)
        {
            harms |= effect.Kind is MiracleEffectKind.Smite or MiracleEffectKind.Ignite or MiracleEffectKind.Quake or MiracleEffectKind.Flood or MiracleEffectKind.Judge;
        }

        foreach (var rival in world.Gods)
        {
            if (rival.Id == godId)
            {
                continue;
            }

            foreach (var id in affected)
            {
                if (world.Settlements.Find(s => s.Id == id) is not { } settlement || BeliefModel.Find(settlement, rival.Id) is not { Adherence: > 0.05f })
                {
                    continue;
                }

                var need = needs.GetValueOrDefault(id);
                var act = harms ? DiplomaticAct.Harmed : need >= MajorNeed && settlement.Population > 0 ? DiplomaticAct.Rescued : need >= NeedForHelp ? DiplomaticAct.Helped : DiplomaticAct.Courted;
                _ledger.Record(world, rival.Id, godId, act, id);
                if (act == DiplomaticAct.Rescued)
                {
                    events.Add(new FavourOwed { Tick = world.Tick, CreditorGodId = godId, DebtorGodId = rival.Id, SettlementId = id });
                }

                break;
            }
        }
    }

    /// <summary>Distance between two ground positions in metres.</summary>
    private static float Distance(GroundPosition a, GroundPosition b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
