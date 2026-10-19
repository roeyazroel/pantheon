using System;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Settlements;

/// <summary>
/// One step of the statistical (Tier 1) settlement model (ADR-004, ADR-008): the harvest and
/// meals, health, births and deaths drawn as Poisson counts from the shared random stream, and
/// work on the house under construction. It raises <see cref="ThirstStarted"/> when the wells run dry, and <see cref="FamineStarted"/> in the step in which a
/// settlement's stores run out. <see cref="IntervalSystem"/> runs it once every
/// <see cref="SettlementRules.StepIntervalTicks"/> ticks.
/// </summary>
public sealed class SettlementSystem : ISimulationSystem
{
    private readonly SettlementRules _rules;

    private readonly Domain.Weather.WeatherRules? _weather;
    private readonly Domain.Ecology.EcologyRules? _ecology;

    /// <summary>Creates the system with validated rules.</summary>
    /// <param name="rules">Data-driven settlement tuning.</param>
    /// <param name="weather">Weather rules, for the seasons' and the waves' harvests (ADR-028); <c>null</c> for an even climate.</param>
    /// <param name="ecology">Ecology rules, for the game the people hunt (ADR-028); <c>null</c> for no hunting.</param>
    public SettlementSystem(SettlementRules rules, Domain.Weather.WeatherRules? weather = null, Domain.Ecology.EcologyRules? ecology = null)
    {
        SettlementRulesValidator.Validate(rules);
        _rules = rules;
        _weather = weather;
        _ecology = ecology;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var regions = context.World.Regions;
        var climateYield = _weather is null ? 1f : Weather.ClimateModel.Now(_weather, context.World).YieldFactor;

        // List order is stable, so the random draws happen in the same order on every run.
        foreach (var settlement in context.World.Settlements)
        {
            var hunted = _ecology is null || settlement.Population <= 0 ? 0f : Ecology.EcologyModel.Hunt(_ecology, regions, settlement, _rules.StepIntervalTicks * Domain.World.SimulationTime.SecondsPerTick);
            var outlook = SettlementEconomy.Assess(_rules, settlement, regions, climateYield, hunted);
            if (outlook.IsStarving && settlement.Food > 0f && settlement.Population > 0)
            {
                context.Events.Add(new FamineStarted { Tick = context.World.Tick, SettlementId = settlement.Id });
            }

            if (outlook.IsThirsty && settlement.Water > 0f && settlement.Population > 0)
            {
                context.Events.Add(new ThirstStarted { Tick = context.World.Tick, SettlementId = settlement.Id });
            }

            settlement.Food = outlook.FoodAfter;
            settlement.Water = outlook.WaterAfter;
            settlement.Health = outlook.HealthAfter;
            UpdatePopulation(settlement, outlook, context.Random);
            UpdateConstruction(settlement, outlook.Labour.Builders);
        }
    }

    /// <summary>
    /// Draws births (none while a <see cref="BirthBlocker"/> applies, and never more than the free
    /// housing) and then deaths. Both chances use the health after this step's meals.
    /// </summary>
    private static void UpdatePopulation(SettlementState settlement, SettlementOutlook outlook, SimulationRandom random)
    {
        var population = settlement.Population;
        if (population <= 0)
        {
            return;
        }

        var births = outlook.BirthBlocker == BirthBlocker.None
            ? Math.Min(settlement.Housing - population, random.NextPoisson(population * outlook.BirthChance))
            : 0;
        var deaths = Math.Min(population, random.NextPoisson(population * outlook.DeathChance));
        settlement.Population = population + births - deaths;
    }

    /// <summary>
    /// Adds this step's builder work to the house under construction. A finished house adds
    /// <see cref="SettlementRules.HousingPerHouse"/> (up to the cap), rebuilds a ruin if there is one,
    /// and the next house starts from nothing. Builders never exceed the work left, so a house
    /// finishes exactly when they cover it.
    /// </summary>
    private void UpdateConstruction(SettlementState settlement, float builders)
    {
        var remainingWork = SettlementEconomy.ComputeHouseWork(_rules, settlement) - settlement.ConstructionProgress;
        if (builders <= 0f && remainingWork > 0f)
        {
            return;
        }

        if (builders >= remainingWork)
        {
            settlement.Housing = Math.Min(_rules.MaxHousing, settlement.Housing + _rules.HousingPerHouse);
            settlement.ConstructionProgress = 0f;
            settlement.Ruins = Math.Max(0, settlement.Ruins - 1);
        }
        else
        {
            settlement.ConstructionProgress += builders;
        }
    }
}
