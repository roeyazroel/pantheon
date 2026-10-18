using System;
using Pantheon.Domain.Settlements;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Settlements;

/// <summary>
/// Statistical-tier settlement model (ADR-004): food production and consumption, then births and
/// deaths drawn as Poisson counts from the shared random stream.
/// </summary>
public sealed class SettlementSystem : ISimulationSystem
{
    private readonly SettlementRules _rules;

    /// <summary>Creates the system with validated rules.</summary>
    /// <param name="rules">Data-driven settlement tuning.</param>
    public SettlementSystem(SettlementRules rules)
    {
        SettlementRulesValidator.Validate(rules);
        _rules = rules;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var regions = context.World.Regions;

        // List order is stable, so the random draws happen in the same order on every run.
        foreach (var settlement in context.World.Settlements)
        {
            var fertility = regions.GetRegion(settlement.RegionId).Fertility;
            UpdateFood(settlement, fertility);
            UpdatePopulation(settlement, context.Random);
        }
    }

    /// <summary>Net food change of one tick: field production minus consumption.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="population">Current population.</param>
    /// <param name="fertility">Fertility of the settlement's region (0..1).</param>
    /// <returns>Food units gained (positive) or lost (negative) this tick.</returns>
    public static float ComputeFoodBalance(SettlementRules rules, int population, float fertility)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var production = population * rules.WorkforceShare * rules.FoodPerWorkerPerTick * SimMath.Clamp01(fertility);
        var consumption = population * rules.FoodPerPersonPerTick;
        return production - consumption;
    }

    /// <summary>Maximum food a settlement can store.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="housing">Housing capacity in people.</param>
    /// <returns>Storage capacity in food units.</returns>
    public static float ComputeFoodStorage(SettlementRules rules, int housing)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Math.Max(0, housing) * rules.FoodStoragePerHousing;
    }

    /// <summary>Applies the food balance, clamped between empty and full storage.</summary>
    private void UpdateFood(SettlementState settlement, float fertility)
    {
        var balance = ComputeFoodBalance(_rules, settlement.Population, fertility);
        settlement.Food = Math.Clamp(settlement.Food + balance, 0f, ComputeFoodStorage(_rules, settlement.Housing));
    }

    /// <summary>
    /// Draws births (only with a food surplus and free housing) and deaths (higher while the store
    /// is empty). The sum of two Poisson draws is Poisson, so the starvation risk is folded into a
    /// single death draw.
    /// </summary>
    private void UpdatePopulation(SettlementState settlement, SimulationRandom random)
    {
        var population = settlement.Population;
        if (population <= 0)
        {
            return;
        }

        var freeHousing = Math.Max(0, settlement.Housing - population);
        var hasSurplus = settlement.Food >= population * _rules.SurplusFoodPerPerson;
        var births = hasSurplus && freeHousing > 0
            ? Math.Min(freeHousing, random.NextPoisson(population * _rules.BirthChancePerPersonPerTick))
            : 0;

        var deathChance = _rules.DeathChancePerPersonPerTick +
            (settlement.Food <= 0f ? _rules.StarvationDeathChancePerPersonPerTick : 0.0);
        var deaths = Math.Min(population, random.NextPoisson(population * deathChance));

        settlement.Population = population + births - deaths;
    }
}
