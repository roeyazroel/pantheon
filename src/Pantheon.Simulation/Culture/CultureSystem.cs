using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Society;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Culture;

/// <summary>
/// One culture step (spec §8, §11, §32–35, ADR-024): doctrines drift towards what each settlement
/// lives through; secularism rises and falls, and anti-divine settlements turn people from every
/// god and spread their reason; temples send missionaries; charitable villages feed hungry
/// neighbours; resentful villages rise against the gods they fear; and gods' conduct fades while
/// their traits emerge. It raises <see cref="DoctrineEmbraced"/>, <see cref="SecularismRose"/>,
/// <see cref="Rebellion"/> and <see cref="TraitEarned"/>.
/// </summary>
public sealed class CultureSystem : ISimulationSystem
{
    private static readonly Doctrine[] Doctrines = Enum.GetValues<Doctrine>();

    // A god's followers must hold this share, fear it this much and the settlement be this unsettled for a revolt.
    private const float RevoltShare = 0.3f;
    private const float RevoltFear = 0.25f;
    private const float RevoltOrder = 0.5f;

    // Food stores this full let a charitable village give, and stores this empty make a neighbour starving.
    private const float Plenty = 0.5f;
    private const float Starving = 0.6f;

    private readonly DivineRules _divine;
    private readonly CultureRules _rules;
    private readonly SettlementRules _settlementRules;
    private readonly SocietyRules? _society;
    private readonly int _intervalTicks;

    /// <summary>Creates the system over validated rules.</summary>
    /// <param name="divine">Divine rules, with the culture rules.</param>
    /// <param name="settlementRules">Settlement rules, for stores.</param>
    /// <param name="society">Society rules, for trade range; <c>null</c> to use the missions' range.</param>
    /// <param name="intervalTicks">Ticks between culture steps.</param>
    public CultureSystem(DivineRules divine, SettlementRules settlementRules, SocietyRules? society, int intervalTicks)
    {
        DivineRulesValidator.Validate(divine);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _divine = divine;
        _rules = divine.Culture;
        _settlementRules = settlementRules;
        _society = society;
        _intervalTicks = intervalTicks;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        var count = world.Settlements.Count;
        for (var i = 0; i < count; i++)
        {
            var settlement = world.Settlements[i];
            if (settlement.Population <= 0)
            {
                continue;
            }

            DriftDoctrines(world, settlement, seconds, context.Events);
            Secularism(world, settlement, seconds, context.Events);
            Missionaries(world, settlement, seconds);
            Charity(world, settlement, seconds);
            Revolt(world, settlement, context.Events);
        }

        foreach (var god in world.Gods)
        {
            ConductModel.Fade(god.Conduct, _rules.ConductFadePerSecond * seconds);
            var traits = ConductModel.Traits(_rules, god.Conduct);
            foreach (var trait in traits)
            {
                if (!god.Traits.Contains(trait))
                {
                    context.Events.Add(new TraitEarned { Tick = world.Tick, GodId = god.Id, Trait = trait });
                }
            }

            god.Traits.Clear();
            god.Traits.AddRange(traits);
        }
    }

    /// <summary>Every doctrine drifts towards what the settlement lives through; one past <see cref="CultureRules.Embraced"/> is reported.</summary>
    private void DriftDoctrines(WorldState world, SettlementState settlement, float seconds, List<WorldEvent> events)
    {
        var culture = settlement.Culture;
        var war = SocietyModel.War(world, settlement);
        var neighbours = Neighbours(world, settlement).Count;
        var blessing = SettlementEconomy.SurveyFarmland(world.Regions, settlement.RegionId, _settlementRules.FarmlandRadius).MeanBlessing;
        var drift = Math.Min(1f, _rules.DoctrineDriftPerSecond * seconds);
        foreach (var doctrine in Doctrines)
        {
            var before = culture.Get(doctrine);
            var target = CultureModel.DoctrineTarget(world, settlement, war, neighbours, blessing, doctrine);
            var after = SimMath.Clamp01(before + ((target - before) * drift));
            culture.Set(doctrine, after);
            if (before < _rules.Embraced && after >= _rules.Embraced)
            {
                events.Add(new DoctrineEmbraced { Tick = world.Tick, SettlementId = settlement.Id, Doctrine = doctrine });
            }
        }
    }

    /// <summary>Secularism drifts to its target; a change of stage is reported; anti-divine settlements turn people away and spread their reason.</summary>
    private void Secularism(WorldState world, SettlementState settlement, float seconds, List<WorldEvent> events)
    {
        var culture = settlement.Culture;
        var before = CultureModel.Stage(_rules, culture.Secularism);
        var drift = Math.Min(1f, _rules.SecularDriftPerSecond * seconds);
        culture.Secularism = SimMath.Clamp01(culture.Secularism + ((CultureModel.SecularTarget(_rules, settlement) - culture.Secularism) * drift));
        var after = CultureModel.Stage(_rules, culture.Secularism);
        if (after != before)
        {
            events.Add(new SecularismRose { Tick = world.Tick, SettlementId = settlement.Id, Stage = after, Previous = before });
        }

        if (after != SecularStage.AntiDivine)
        {
            return;
        }

        foreach (var belief in settlement.Beliefs)
        {
            belief.Adherence = SimMath.Clamp01(belief.Adherence * (1f - _rules.AntiDivineApostasy));
        }

        foreach (var neighbour in Neighbours(world, settlement))
        {
            neighbour.Culture.Secularism = SimMath.Clamp01(neighbour.Culture.Secularism + (_rules.SecularSpreadPerSecond * culture.Secularism * seconds));
        }
    }

    /// <summary>A temple's missionaries win people who follow no god in the settlements around it, as far as they are open (§32).</summary>
    private void Missionaries(WorldState world, SettlementState home, float seconds)
    {
        foreach (var temple in home.Society.Buildings)
        {
            if (temple.Kind != BuildingKind.Temple || temple.GodId is not { } godId || BeliefModel.Find(home, godId) is not { } faithful)
            {
                continue;
            }

            var zeal = SimMath.Clamp01(faithful.Adherence) * SimMath.Clamp01(faithful.Faith);
            foreach (var target in Neighbours(world, home, _rules.MissionRange))
            {
                var free = 1f - BeliefModel.TotalAdherence(target.Beliefs) - (target.OldFaith?.Share ?? 0f);
                if (free <= 0f)
                {
                    continue;
                }

                var isMain = CultureModel.Main(target)?.GodId == godId;
                var won = Math.Min(free, _rules.MissionPerSecond * zeal * free * CultureModel.PullFactor(_rules, target.Culture, isMain) * seconds);
                if (won <= 0f)
                {
                    continue;
                }

                var belief = BeliefModel.FindOrAdd(_divine, target, godId);
                belief.Adherence = SimMath.Clamp01(belief.Adherence + won);
                belief.Faith = BeliefModel.ApplyChange(belief.Faith, 2f * won);
            }
        }
    }

    /// <summary>A charitable village with full stores gives food to starving neighbours (§33).</summary>
    private void Charity(WorldState world, SettlementState giver, float seconds)
    {
        var charity = giver.Culture.Charity;
        if (charity <= 0f || giver.Food < Plenty * SettlementEconomy.StorageOf(_settlementRules, giver))
        {
            return;
        }

        foreach (var neighbour in Neighbours(world, giver))
        {
            var wanted = Math.Max(0, neighbour.Population) * _divine.ComfortFoodPerPerson;
            if (wanted <= 0f || neighbour.Food >= (1f - Starving) * wanted)
            {
                continue;
            }

            var given = Math.Min(giver.Food * _rules.CharityPerSecond * charity * seconds, SettlementEconomy.StorageOf(_settlementRules, neighbour) - neighbour.Food);
            if (given > 0f)
            {
                giver.Food -= given;
                neighbour.Food += given;
            }
        }
    }

    /// <summary>Followers who fear and resent their god in an unsettled village renounce it (§8: rebellion), tearing down its temple.</summary>
    private void Revolt(WorldState world, SettlementState settlement, List<WorldEvent> events)
    {
        if (settlement.Society.Stability >= RevoltOrder)
        {
            return;
        }

        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence < RevoltShare || belief.Resentment < _rules.RebellionResentment || belief.Fear < RevoltFear)
            {
                continue;
            }

            var renounced = belief.Adherence * _rules.RebellionShare;
            belief.Adherence -= renounced;
            belief.Resentment *= 1f - _rules.RebellionVent;
            belief.Fear *= 0.5f;
            belief.Doubt = BeliefModel.ApplyChange(belief.Doubt, 0.2f);
            settlement.Society.Buildings.RemoveAll(b => b.Kind == BuildingKind.Temple && b.GodId == belief.GodId);
            if (settlement.Society.PriorityGodId == belief.GodId)
            {
                (settlement.Society.Priority, settlement.Society.PriorityGodId) = (SettlementPriority.None, null);
            }

            settlement.Society.Stability = SimMath.Clamp01(settlement.Society.Stability - 0.2f);
            events.Add(new Rebellion { Tick = world.Tick, SettlementId = settlement.Id, GodId = belief.GodId, People = (int)MathF.Round(renounced * settlement.Population) });
            return;
        }
    }

    /// <summary>The other living settlements within a range of regions (trade range unless given).</summary>
    private List<SettlementState> Neighbours(WorldState world, SettlementState settlement, int? range = null)
    {
        var reach = range ?? _society?.TradeRange ?? _rules.MissionRange;
        var (column, row) = world.Regions.GetCoordinates(settlement.RegionId);
        return world.Settlements.FindAll(other =>
        {
            var (c, r) = world.Regions.GetCoordinates(other.RegionId);
            return other.Id != settlement.Id && other.Population > 0 && Math.Max(Math.Abs(c - column), Math.Abs(r - row)) <= reach;
        });
    }
}
