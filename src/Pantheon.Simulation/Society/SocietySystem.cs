using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Society;

/// <summary>
/// One society step (spec §29, §30, §36, ADR-023): every settlement's safety, happiness and
/// stability move towards what its life makes them; it grows rich from surplus and trade, learns,
/// advances in technology (and learns from its neighbours), raises the buildings its priority calls
/// for, and under an Expansion priority sends settlers to found new villages. It raises
/// <see cref="TechnologyAdvanced"/>, <see cref="BuildingCompleted"/> and <see cref="SettlementFounded"/>.
/// </summary>
public sealed class SocietySystem : ISimulationSystem
{
    private static readonly TechnologyField[] Fields = Enum.GetValues<TechnologyField>();

    private readonly SocietyRules _rules;
    private readonly SettlementRules _settlementRules;
    private readonly DivineRules _divine;
    private readonly Expansion _expansion;

    /// <summary>Creates the system over validated rules.</summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="settlementRules">Settlement rules, for stores, wells and the workforce.</param>
    /// <param name="divine">Divine rules, for a settlement's conditions.</param>
    public SocietySystem(SocietyRules rules, SettlementRules settlementRules, DivineRules divine)
    {
        SocietyRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(divine);
        _rules = rules;
        _settlementRules = settlementRules;
        _divine = divine;
        _expansion = new Expansion(rules);
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var seconds = _rules.StepIntervalTicks * SimulationTime.SecondsPerTick;
        var count = world.Settlements.Count;
        for (var i = 0; i < count; i++)
        {
            var settlement = world.Settlements[i];
            if (settlement.Population <= 0)
            {
                continue;
            }

            Live(world, settlement, seconds);
            Learn(world, settlement, seconds, context.Events);
            Build(settlement, seconds, world, context.Events);
            _expansion.TryFound(world, settlement, Culture.CultureModel.FoundingFactor(_divine.Culture, settlement.Culture), context.Events);
        }
    }

    /// <summary>Safety, happiness and stability drift towards their targets; wealth grows from surplus and trade.</summary>
    private void Live(WorldState world, SettlementState settlement, float seconds)
    {
        var society = settlement.Society;
        var hazards = Hazards.Of(_settlementRules, world, settlement);
        var conditions = BeliefModel.AssessConditions(_divine, settlement, hazards);
        var war = SocietyModel.War(world, settlement);
        var drift = Math.Min(1f, _rules.DriftPerSecond * seconds);
        society.Safety = Toward(society.Safety, SocietyModel.SafetyTarget(_rules, settlement, conditions.DangerNeed, war), drift);
        society.Happiness = Toward(society.Happiness, SocietyModel.HappinessTarget(_rules, settlement, conditions.FoodNeed, hazards.Thirst), drift);
        society.Stability = Toward(society.Stability, SocietyModel.StabilityTarget(_rules, settlement, war), drift);

        var outlook = SettlementEconomy.Assess(_settlementRules, settlement, world.Regions);
        var stepsPerSecond = 1f / (_settlementRules.StepIntervalTicks * SimulationTime.SecondsPerTick);
        var surplus = (outlook.Production - outlook.Consumption) * stepsPerSecond;
        var full = settlement.Food >= _rules.FullStores * outlook.Storage;
        var neighbours = SocietyModel.Neighbours(_rules, world, settlement).Count;
        society.Wealth = Math.Max(0f, society.Wealth + SocietyModel.WealthChange(_rules, settlement, surplus, full, neighbours, seconds));
    }

    /// <summary>The people learn, and every field of technology grows with learning, faster where the settlement's focus lies, and towards its most advanced neighbour.</summary>
    private void Learn(WorldState world, SettlementState settlement, float seconds, List<WorldEvent> events)
    {
        var society = settlement.Society;
        var change = SocietyModel.EducationChange(_rules, settlement, seconds);
        var learning = change > 0f ? Culture.CultureModel.LearningFactor(_divine.Culture, settlement.Culture) : 1f;
        society.Education = SimMath.Clamp01(society.Education + (change * learning));
        var focus = SocietyModel.Focus(_rules, settlement);
        var neighbours = SocietyModel.Neighbours(_rules, world, settlement);
        foreach (var field in Fields)
        {
            var level = society.Technology.Get(field);
            var gain = _rules.TechnologyPerSecond * society.Education * (field == focus ? _rules.FocusFactor : 1f) * seconds;
            var best = level;
            foreach (var neighbour in neighbours)
            {
                best = Math.Max(best, neighbour.Society.Technology.Get(field));
            }

            gain += _rules.DiffusionPerSecond * (best - level) * seconds;
            var next = Math.Min(_rules.MaxTechnology, level + gain);
            society.Technology.Set(field, next);
            if ((int)MathF.Floor(next) > (int)MathF.Floor(level))
            {
                events.Add(new TechnologyAdvanced { Tick = world.Tick, SettlementId = settlement.Id, Field = field, Level = (int)MathF.Floor(next) });
            }
        }
    }

    /// <summary>Starts the building the priority calls for once the wealth is there, and works on the one under way.</summary>
    private void Build(SettlementState settlement, float seconds, WorldState world, List<WorldEvent> events)
    {
        var society = settlement.Society;
        if (society.Project is null && SocietyModel.NextProject(_rules, world, settlement) is { } next && society.Wealth >= next.Wealth && HasMaterials(settlement, next))
        {
            society.Wealth -= next.Wealth;
            if (_settlementRules.Resources.Enabled)
            {
                (settlement.Wood, settlement.Stone) = (settlement.Wood - next.Wood, settlement.Stone - next.Stone);
            }

            society.Project = next.Kind;
            society.ProjectProgress = 0f;
        }

        if (society.Project is not { } project || SocietyModel.Find(_rules, project) is not { } building)
        {
            return;
        }

        var workers = settlement.Population * _settlementRules.WorkforceShare;
        society.ProjectProgress += workers * _rules.ProjectWorkPerWorker * seconds;
        if (society.ProjectProgress < building.Work)
        {
            return;
        }

        var god = project == BuildingKind.Temple ? society.PriorityGodId : null;
        society.Buildings.Add(new Building { Kind = project, GodId = god });
        society.Project = null;
        society.ProjectProgress = 0f;
        events.Add(new BuildingCompleted { Tick = world.Tick, SettlementId = settlement.Id, Kind = project, GodId = god });
    }

    /// <summary>Whether the stores hold the wood and stone a building takes; always, where nobody gathers (ADR-033).</summary>
    private bool HasMaterials(SettlementState settlement, BuildingDefinition building) =>
        !_settlementRules.Resources.Enabled || (settlement.Wood >= building.Wood && settlement.Stone >= building.Stone);

    /// <summary>A value moved a share of the way to its target.</summary>
    private static float Toward(float value, float target, float share) => SimMath.Clamp01(value + ((target - value) * share));
}
