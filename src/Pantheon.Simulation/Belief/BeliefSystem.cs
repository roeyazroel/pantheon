using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Belief;

/// <summary>
/// One step of the belief model (spec §7–9, §13, ADR-009): every settlement's belief drifts and
/// its people convert or fall away, every god gains the Divine Power its worshippers raise and
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

    /// <summary>Creates the system with validated rules.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="settlementRules">Settlement rules, for the workforce that worships.</param>
    public BeliefSystem(DivineRules rules, SettlementRules settlementRules)
    {
        DivineRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        _rules = rules;
        _settlementRules = settlementRules;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        foreach (var settlement in world.Settlements)
        {
            StepSettlement(settlement, world.Tick, context.Events);
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
    private void StepSettlement(SettlementState settlement, long tick, List<WorldEvent> events)
    {
        var beliefs = settlement.Beliefs;
        if (beliefs.Count == 0)
        {
            return;
        }

        var conditions = BeliefModel.AssessConditions(_rules, settlement);
        var changes = BeliefModel.ComputeAdherenceChanges(_rules, beliefs);
        for (var i = 0; i < beliefs.Count; i++)
        {
            var belief = beliefs[i];
            var hadMajority = belief.Adherence >= BeliefModel.MajorityShare;
            (belief.Faith, belief.Love, belief.Fear, belief.Doubt) = BeliefModel.Drift(_rules, belief, conditions);
            belief.Adherence = SimMath.Clamp01(belief.Adherence + changes[i]);
            var hasMajority = belief.Adherence >= BeliefModel.MajorityShare;
            if (hasMajority != hadMajority)
            {
                events.Add(new FollowingChanged { Tick = tick, SettlementId = settlement.Id, GodId = belief.GodId, Gained = hasMajority });
            }
        }

        beliefs.RemoveAll(b => b.Adherence * Math.Max(0, settlement.Population) < ForgottenFollowers && b.Faith < _rules.ForgottenFaith);
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
