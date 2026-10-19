using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Water;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Water;

/// <summary>
/// One water step (spec §15, §19, ADR-022): every river rises towards the level the rain and the
/// wet soil along it set and falls back when they ease; a river over its banks floods the low land
/// downstream; floods soak the soil, put out fires, leave silt that blesses the land and drown
/// houses, people and stores; rivers and lakes keep the land beside them moist. It raises
/// <see cref="FloodStarted"/> and <see cref="SettlementDamaged"/>.
/// </summary>
/// <remarks>
/// Like the disasters, it draws from a stream derived from the world seed and the tick, so it never
/// shifts the shared stream and needs no random state in the save.
/// </remarks>
public sealed class WaterSystem : ISimulationSystem
{
    /// <summary>Purpose name of the water's random streams.</summary>
    public const string RandomStream = "water";

    private readonly WaterRules _rules;
    private readonly WeatherRules _weather;
    private readonly DisasterDamage _damage;

    /// <summary>Creates the system over validated rules.</summary>
    /// <param name="rules">Water rules.</param>
    /// <param name="weather">Weather rules, for the rain over the rivers.</param>
    /// <param name="settlementRules">Settlement rules, for houses.</param>
    /// <param name="divine">Divine rules, for how floods shake belief.</param>
    /// <param name="disasters">Disaster rules, for what a Shield spares.</param>
    /// <param name="domains">Domain rules, for the houses Protection saves; <c>null</c> for none.</param>
    public WaterSystem(WaterRules rules, WeatherRules weather, SettlementRules settlementRules, DivineRules divine, DisasterRules disasters, DomainRules? domains = null)
    {
        WaterRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(weather);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(disasters);
        _rules = rules;
        _weather = weather;
        _damage = new DisasterDamage(disasters, settlementRules, divine, domains);
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var seconds = _rules.StepIntervalTicks * SimulationTime.SecondsPerTick;
        var depths = RiseAndFall(world, seconds, context.Events);
        Soak(world, depths, seconds);
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, RandomStream, world.Tick));
        Drown(world, random, seconds, context.Events);
    }

    /// <summary>Each river moves towards the level the weather sets; those over their banks spread their floods.</summary>
    /// <returns>The flood each region lies under from the rivers now.</returns>
    private Dictionary<int, float> RiseAndFall(WorldState world, float seconds, List<WorldEvent> events)
    {
        var depths = new Dictionary<int, float>();
        var response = Math.Min(1f, _rules.LevelResponsePerSecond * seconds);
        foreach (var river in world.Water.Rivers)
        {
            var before = WaterModel.Overflow(_rules, river.Level);
            var target = WaterModel.TargetLevel(_rules, _weather, world, river);
            river.Level = Math.Max(0f, river.Level + ((target - river.Level) * response));
            var overflow = WaterModel.Overflow(_rules, river.Level);
            WaterModel.Spread(_rules, world.Regions, river, overflow, depths);
            if (before <= 0f && overflow > 0f && river.Regions.Count > 0)
            {
                events.Add(new FloodStarted { Tick = world.Tick, RiverId = river.Id, GodId = null, RegionId = river.Regions[^1] });
            }
        }

        return depths;
    }

    /// <summary>
    /// Floods rise to the rivers' or drain away; flooded land is soaked, its fires are put out and
    /// its silt blesses it; rivers and lakes water the land beside them.
    /// </summary>
    private void Soak(WorldState world, Dictionary<int, float> depths, float seconds)
    {
        foreach (var region in world.Regions.Cells)
        {
            if (region.Biome == Biome.Sea)
            {
                region.Flood = 0f;
                continue;
            }

            var river = depths.TryGetValue(region.Id, out var depth) ? depth : 0f;
            region.Flood = SimMath.Clamp01(Math.Max(river, region.Flood - (_rules.FloodRecedePerSecond * seconds)));
            if (region.Flood > 0f)
            {
                region.SoilMoisture = Math.Max(region.SoilMoisture, region.Flood);
                region.Fire *= 1f - region.Flood;
                region.Blessing = Math.Max(region.Blessing, _rules.SiltBlessing * region.Flood);
            }

            if (region.FreshWater > 0f)
            {
                var soil = SimMath.Clamp01(region.SoilMoisture);
                region.SoilMoisture = SimMath.Clamp01(soil + (_rules.IrrigationPerSecond * region.FreshWater * (1f - soil) * seconds));
            }
        }
    }

    /// <summary>A flood in a settlement's own region drowns its houses, people and stores and frightens the survivors.</summary>
    private void Drown(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        foreach (var settlement in world.Settlements)
        {
            var flood = world.Regions.Cells[settlement.RegionId].Flood * _damage.Protection(settlement, world.Tick);
            if (flood <= 0f || settlement.Population <= 0)
            {
                continue;
            }

            var severity = flood * seconds;
            var houses = _damage.Destroy(settlement, random, _rules.FloodHousingLossPerSecond * severity * _damage.Fragility(world, settlement));
            var deaths = DisasterDamage.Kill(settlement, random, _rules.FloodDeathsPerSecond * severity);
            settlement.Food *= Math.Max(0f, 1f - (_rules.FloodFoodLossPerSecond * severity));
            _damage.Frighten(settlement, _rules.FloodDreadPerSecond * severity);
            DisasterDamage.Report(world, settlement, DisasterKind.Flood, houses, deaths, events);
        }
    }
}
