using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Belief;

/// <summary>
/// One step of the belief model (spec §7–9, §13, ADR-009): every settlement's belief drifts and
/// its people convert, fall away or break away in a schism (ADR-021), every god gains the Divine Power its worshippers raise and
/// reaches the rank its following earns, and blessings on the land fade.
/// <see cref="IntervalSystem"/> runs it once every <see cref="DivineRules.StepIntervalTicks"/> ticks.
/// </summary>
public sealed class BeliefSystem : ISimulationSystem
{
    // A belief is forgotten once it holds fewer followers than this and its Faith is below ForgottenFaith.
    private const float ForgottenFollowers = 0.5f;

    // Blessings this small are cleared, so the land does not carry traces forever.
    private const float VanishedBlessing = 0.001f;

    private readonly DivineRules _rules;
    private readonly SettlementRules _settlementRules;
    private readonly DiplomacyRules? _diplomacy;
    private readonly IActRecorder? _ledger;

    /// <summary>Creates the system with validated rules.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="settlementRules">Settlement rules, for the workforce that worships.</param>
    /// <param name="diplomacy">Diplomacy rules, for peoples at war resisting the enemy; <c>null</c> to ignore diplomacy.</param>
    /// <param name="ledger">Where conversions of a rival's settlement are recorded; <c>null</c> for none.</param>
    public BeliefSystem(DivineRules rules, SettlementRules settlementRules, DiplomacyRules? diplomacy = null, IActRecorder? ledger = null)
    {
        DivineRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        _rules = rules;
        _settlementRules = settlementRules;
        _diplomacy = diplomacy;
        _ledger = ledger;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        foreach (var settlement in world.Settlements)
        {
            StepSettlement(world, settlement, context.Events);
        }

        foreach (var god in world.Gods)
        {
            god.DivinePower += GodStanding.ComputePowerPerStep(_rules, _settlementRules, world, god.Id);
            var rank = GodStanding.RankFor(_rules, GodStanding.CountFollowers(world, god.Id));
            if (rank > god.Rank)
            {
                god.Rank = rank;
                context.Events.Add(new RankAchieved { Tick = world.Tick, GodId = god.Id, Rank = rank });
            }
        }

        FadeBlessings(world.Regions);
    }

    /// <summary>Drifts every belief of a settlement from its values before the step, then converts and forgets.</summary>
    private void StepSettlement(WorldState world, SettlementState settlement, List<WorldEvent> events)
    {
        var beliefs = settlement.Beliefs;
        if (beliefs.Count == 0)
        {
            Schisms.Step(_rules, world, settlement, events);
            return;
        }

        var conditions = BeliefModel.AssessConditions(_rules, settlement, Hazards.Of(_settlementRules, world, settlement));
        if (settlement.SanctuaryGodId is not null && world.Tick >= settlement.SanctuaryUntilTick)
        {
            settlement.SanctuaryGodId = null;
            settlement.SanctuaryUntilTick = 0;
        }

        var steadfast = settlement.SanctuaryGodId is { } sheltered ? beliefs.FindIndex(b => b.GodId == sheltered) : -1;
        var changes = BeliefModel.ComputeAdherenceChanges(_rules, beliefs, WarResistance(world, beliefs), Schisms.OldFaithShare(settlement), steadfast);
        int? gainer = null;
        int? loser = null;
        for (var i = 0; i < beliefs.Count; i++)
        {
            var belief = beliefs[i];
            var hadMajority = belief.Adherence >= BeliefModel.MajorityShare;
            (belief.Faith, belief.Love, belief.Fear, belief.Doubt) = BeliefModel.Drift(_rules, belief, conditions);
            belief.Adherence = SimMath.Clamp01(belief.Adherence + changes[i]);
            var hasMajority = belief.Adherence >= BeliefModel.MajorityShare;
            if (hasMajority != hadMajority)
            {
                events.Add(new FollowingChanged { Tick = world.Tick, SettlementId = settlement.Id, GodId = belief.GodId, Gained = hasMajority });
                if (hasMajority)
                {
                    gainer = belief.GodId;
                }
                else
                {
                    loser = belief.GodId;
                }
            }
        }

        if (gainer is { } won && loser is { } lost)
        {
            _ledger?.Record(world, lost, won, DiplomaticAct.Converted, settlement.Id);
        }

        beliefs.RemoveAll(b => b.Adherence * Math.Max(0, settlement.Population) < ForgottenFollowers && b.Faith < _rules.ForgottenFaith);
        Schisms.Step(_rules, world, settlement, events);
    }

    /// <summary>
    /// Pull factors for a settlement's gods: where the god most people follow is at war with
    /// another god, the people resist that enemy's pull by <c>WarResistance</c>.
    /// </summary>
    private float[]? WarResistance(WorldState world, List<GodBelief> beliefs)
    {
        if (_diplomacy is null || beliefs.Count < 2 || world.Diplomacy.Stances.Count == 0)
        {
            return null;
        }

        GodBelief? main = null;
        foreach (var belief in beliefs)
        {
            if (belief.Adherence >= BeliefModel.MajorityShare)
            {
                main = belief;
            }
        }

        if (main is null)
        {
            return null;
        }

        var factors = new float[beliefs.Count];
        for (var i = 0; i < beliefs.Count; i++)
        {
            factors[i] = DiplomacyModel.AreAtWar(world, main.GodId, beliefs[i].GodId) ? 1f - _diplomacy.WarResistance : 1f;
        }

        return factors;
    }

    /// <summary>Lets every region's blessing fade by its decay share.</summary>
    private void FadeBlessings(RegionGrid regions)
    {
        foreach (var region in regions.Cells)
        {
            if (region.Blessing <= 0f)
            {
                continue;
            }

            var blessing = region.Blessing * (1f - _rules.BlessingDecayPerStep);
            region.Blessing = blessing < VanishedBlessing ? 0f : blessing;
        }
    }
}
