using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Miracles;

/// <summary>
/// The authority for miracles (spec §73, ADR-009). <see cref="Check"/> answers whether a request
/// may be cast and what it costs; <see cref="Cast"/> checks it again and, when accepted, spends the
/// power, starts the cooldown, applies the effects and lets the witnesses' belief respond. Both
/// are deterministic and draw no random numbers.
/// </summary>
public sealed class MiracleCaster
{
    // A miracle counts as help to a rival's followers only when their food or health need is at least this.
    private const float NeedForHelp = 0.5f;

    private readonly DivineRules _rules;
    private readonly SettlementRules _settlementRules;
    private readonly MiracleBook _book;
    private readonly IActRecorder? _ledger;

    /// <summary>Creates a caster over validated content.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="settlementRules">Settlement rules, for food storage.</param>
    /// <param name="book">The miracles.</param>
    /// <param name="ledger">Where the gods whose followers a miracle reaches note it (ADR-012); <c>null</c> for none.</param>
    public MiracleCaster(DivineRules rules, SettlementRules settlementRules, MiracleBook book, IActRecorder? ledger = null)
    {
        _ledger = ledger;
        DivineRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(book);
        _rules = rules;
        _settlementRules = settlementRules;
        _book = book;
    }

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
            if (rival.Id != godId && !DiplomacyModel.AreAllied(world, godId, rival.Id))
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
        var cost = ComputeCost(_rules, miracle, influence, rivals);
        var rejection =
            issuerGodId is { } issuer && issuer != god.Id ? CastRejection.NotAuthorized
            : god.Rank < miracle.MinimumRank ? CastRejection.RankTooLow
            : cooldown > 0 ? CastRejection.OnCooldown
            : !inWorld ? CastRejection.OutsideWorld
            : influence < miracle.InfluenceRequirement ? CastRejection.OutsideInfluence
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
        var affected = new List<int>();
        var needs = new Dictionary<int, float>();
        var deaths = 0;
        foreach (var settlement in world.Settlements)
        {
            var distance = Distance(settlement.Position, request.Target);
            var conditions = BeliefModel.AssessConditions(_rules, settlement);
            if (distance <= reach)
            {
                affected.Add(settlement.Id);
                needs[settlement.Id] = Math.Max(conditions.FoodNeed, conditions.HealthNeed);
                deaths += ApplyEffects(miracle, settlement);
            }

            var closeness = distance <= reach ? 1f : SimMath.Clamp01(1f - ((distance - reach) / Math.Max(1f, _rules.WitnessRadius)));
            if (closeness > 0f && settlement.Population > 0)
            {
                Witness(world, miracle, settlement, god.Id, closeness, conditions);
            }
        }

        NoteByRivals(world, miracle, god.Id, affected, needs);

        ApplyRegionEffects(miracle, world.Regions, request.Target);
        events.Add(new MiracleCast
        {
            Tick = world.Tick,
            GodId = god.Id,
            MiracleId = miracle.Id,
            Target = request.Target,
            Cost = check.Cost,
            AffectedSettlementIds = affected,
            Deaths = deaths,
        });
        return check;
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

    /// <summary>Applies the settlement effects to one settlement the miracle reaches.</summary>
    /// <returns>People killed.</returns>
    private int ApplyEffects(MiracleDefinition miracle, SettlementState settlement)
    {
        var deaths = 0;
        foreach (var effect in miracle.Effects)
        {
            switch (effect.Kind)
            {
                case MiracleEffectKind.AddFood:
                    var storage = SettlementEconomy.ComputeStorage(_settlementRules, settlement.Housing);
                    settlement.Food = Math.Max(settlement.Food, Math.Min(storage, settlement.Food + effect.Amount));
                    break;
                case MiracleEffectKind.Heal:
                    settlement.Health = Math.Min(1f, settlement.Health + effect.Amount);
                    break;
                case MiracleEffectKind.Smite:
                    var killed = Math.Min(settlement.Population, (int)MathF.Round(effect.Amount));
                    settlement.Population -= killed;
                    deaths += killed;
                    break;
                case MiracleEffectKind.Bless:
                    break;
            }
        }

        return deaths;
    }

    /// <summary>Applies the region effects (blessing) to every region whose centre the miracle reaches.</summary>
    private static void ApplyRegionEffects(MiracleDefinition miracle, RegionGrid grid, GroundPosition target)
    {
        foreach (var effect in miracle.Effects)
        {
            if (effect.Kind != MiracleEffectKind.Bless)
            {
                continue;
            }

            foreach (var region in grid.Cells)
            {
                if (Distance(grid.GetRegionCenter(region.Id), target) <= miracle.Radius)
                {
                    region.Blessing = Math.Max(region.Blessing, SimMath.Clamp01(effect.Amount));
                }
            }
        }
    }

    /// <summary>
    /// Lets the people of a settlement who saw the miracle change their belief in the god, and plants
    /// doubt about every other god they follow: another god can work wonders here.
    /// </summary>
    private void Witness(WorldState world, MiracleDefinition miracle, SettlementState settlement, int godId, float closeness, SettlementConditions conditions)
    {
        var need = BeliefModel.NeedFor(conditions, miracle.Gratitude);
        var loveScale = _rules.GratitudeFloor + ((1f - _rules.GratitudeFloor) * need);
        foreach (var other in settlement.Beliefs)
        {
            if (other.GodId != godId && other.Adherence > 0f && !DiplomacyModel.AreAllied(world, godId, other.GodId))
            {
                other.Doubt = BeliefModel.ApplyChange(other.Doubt, _rules.RivalMiracleDoubt * closeness);
            }
        }

        var belief = BeliefModel.FindOrAdd(_rules, settlement, godId);
        BeliefModel.ApplyImpulse(belief, miracle.Witness, closeness, loveScale);
    }

    /// <summary>
    /// Every other god whose followers the miracle reached notes it once: harm when it struck them,
    /// help when they were in real need (spec §41), and courting otherwise (ADR-012).
    /// </summary>
    private void NoteByRivals(WorldState world, MiracleDefinition miracle, int godId, List<int> affected, Dictionary<int, float> needs)
    {
        if (_ledger is null || affected.Count == 0)
        {
            return;
        }

        var harms = false;
        foreach (var effect in miracle.Effects)
        {
            harms |= effect.Kind == MiracleEffectKind.Smite;
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

                var act = harms ? DiplomaticAct.Harmed : needs.GetValueOrDefault(id) >= NeedForHelp ? DiplomaticAct.Helped : DiplomaticAct.Courted;
                _ledger.Record(world, rival.Id, godId, act, id);
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
