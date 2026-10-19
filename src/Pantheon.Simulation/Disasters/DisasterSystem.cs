using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Disasters;

/// <summary>
/// One disaster step (spec §19–21 and §94, ADR-014): natural earthquakes strike, earthquakes shake
/// the settlements around them, wildfires burn and spread, and both destroy houses (which become
/// ruins), kill people, ruin stores and frighten the people out of their trust in their gods.
/// Shielded settlements take a fraction of the damage. Hurricanes tear houses down, and earthquakes
/// harm the land's life (ADR-028). It raises <see cref="EarthquakeStruck"/>,
/// <see cref="WildfireStarted"/> and <see cref="SettlementDamaged"/>.
/// </summary>
/// <remarks>
/// Like the weather, it draws from a stream derived from the world seed and the tick, so it never
/// shifts the shared stream and needs no random state in the save.
/// </remarks>
public sealed class DisasterSystem : ISimulationSystem
{
    /// <summary>Purpose name of the disasters' random streams.</summary>
    public const string RandomStream = "disasters";

    private readonly DisasterRules _rules;
    private readonly WeatherRules _weather;
    private readonly EcologyRules? _ecology;
    private readonly Wildfires _fires;
    private readonly DisasterDamage _damage;

    /// <summary>Creates the system with validated rules.</summary>
    /// <param name="rules">Disaster rules.</param>
    /// <param name="settlementRules">Settlement rules, for houses and farmland.</param>
    /// <param name="divine">Divine rules, for how disasters shake belief.</param>
    /// <param name="weather">Weather rules, for lightning, rain and wind.</param>
    /// <param name="domains">Domain rules, for the houses the Protection domain saves (ADR-021); <c>null</c> for none.</param>
    /// <param name="ecology">The land's life, which earthquakes harm (ADR-028); <c>null</c> for land that never changes.</param>
    /// <param name="callings">Creatures' paths, for the Guardians that shield their villages (ADR-030); <c>null</c> for none.</param>
    public DisasterSystem(DisasterRules rules, SettlementRules settlementRules, DivineRules divine, WeatherRules weather, Domain.Gods.DomainRules? domains = null, EcologyRules? ecology = null, Domain.Creatures.CallingRules? callings = null)
    {
        DisasterRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(weather);
        _rules = rules;
        _weather = weather;
        _ecology = ecology;
        _fires = new Wildfires(rules, weather);
        _damage = new DisasterDamage(rules, settlementRules, divine, domains, callings);
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, RandomStream, world.Tick));
        var seconds = _rules.StepIntervalTicks * SimulationTime.SecondsPerTick;
        NaturalQuake(world, random, seconds, context.Events);
        Shake(world, random, seconds, context.Events);
        HarmLand(world, seconds);
        _fires.Step(world, random, seconds, context.Events);
        BurnSettlements(world, random, seconds, context.Events);
        Hurricanes(world, random, seconds, context.Events);
    }

    /// <summary>Every earthquake under way harms the life of the land it shakes, by its shaking (ADR-028).</summary>
    private void HarmLand(WorldState world, float seconds)
    {
        if (_ecology is not { QuakeDamage: > 0f } ecology || world.Disasters.Earthquakes.Count == 0)
        {
            return;
        }

        var share = seconds / _rules.ShakingSeconds;
        foreach (var quake in world.Disasters.Earthquakes)
        {
            foreach (var region in world.Regions.Cells)
            {
                var shaking = region.Biome == Biome.Sea ? 0f : DisasterModel.Shaking(quake, world.Regions.GetRegionCenter(region.Id));
                if (shaking > 0f)
                {
                    region.Damage = SimMath.Clamp01(region.Damage + (ecology.QuakeDamage * shaking * share));
                }
            }
        }
    }

    /// <summary>A hurricane over a settlement tears its houses down and frightens its people (ADR-028).</summary>
    private void Hurricanes(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        var extremes = _weather.Extremes;
        if (extremes.HurricaneWind >= 1f)
        {
            return;
        }

        foreach (var settlement in world.Settlements)
        {
            var wind = Weather.WeatherModel.At(_weather, world.Weather, settlement.Position).Wind;
            if (wind < extremes.HurricaneWind || settlement.Population <= 0)
            {
                continue;
            }

            var severity = (0.5f + (0.5f * (wind - extremes.HurricaneWind) / (1f - extremes.HurricaneWind))) * _damage.Protection(world, settlement);
            var houses = _damage.Destroy(settlement, random, extremes.HurricaneHouseLossPerSecond * severity * seconds * _damage.Fragility(world, settlement) * _damage.Exposure(settlement, DisasterKind.Hurricane));
            _damage.Frighten(settlement, severity * seconds * _rules.FireDreadPerSecond);
            DisasterDamage.Report(world, settlement, DisasterKind.Hurricane, houses, 0, events);
        }
    }

    /// <summary>Perhaps an earthquake strikes under a random land region, usually a small one.</summary>
    private void NaturalQuake(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        if (random.NextDouble() >= _rules.EarthquakesPerHour / 3600.0 * seconds)
        {
            return;
        }

        var land = world.Regions.Cells.FindAll(region => region.Biome != Biome.Sea);
        if (land.Count == 0)
        {
            return;
        }

        var epicentre = world.Regions.GetRegionCenter(land[random.NextInt(land.Count)].Id);
        var skew = random.NextFloat();
        var magnitude = _rules.MinMagnitude + ((_rules.MaxMagnitude - _rules.MinMagnitude) * skew * skew);
        var quake = DisasterModel.StartQuake(_rules, world.Disasters, epicentre, magnitude, world.Tick, godId: null);
        events.Add(new EarthquakeStruck { Tick = world.Tick, QuakeId = quake.Id, Epicentre = epicentre, Magnitude = magnitude, GodId = null });
    }

    /// <summary>Earthquakes whose time is up end; every other one shakes the settlements in its reach for one share of its damage.</summary>
    private void Shake(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        world.Disasters.Earthquakes.RemoveAll(quake => quake.EndsAtTick <= world.Tick);
        var share = seconds / _rules.ShakingSeconds;
        foreach (var quake in world.Disasters.Earthquakes)
        {
            foreach (var settlement in world.Settlements)
            {
                var shaking = DisasterModel.Shaking(quake, settlement.Position) * _damage.Protection(world, settlement);
                if (shaking <= 0f)
                {
                    continue;
                }

                var severity = shaking * share;
                var houses = _damage.Destroy(settlement, random, _rules.QuakeHousingLoss * (1f - _rules.HouseQuakeResistance) * severity * _damage.Fragility(world, settlement));
                var deaths = DisasterDamage.Kill(settlement, random, _rules.QuakeDeaths * severity * _damage.Deadliness(settlement, DisasterKind.Earthquake));
                settlement.Food *= 1f - (_rules.QuakeFoodLoss * severity * _damage.FoodLoss(settlement));
                settlement.ConstructionProgress *= 1f - severity;
                _damage.Frighten(settlement, severity);
                DisasterDamage.Report(world, settlement, DisasterKind.Earthquake, houses, deaths, events);
                if (random.NextDouble() < _rules.QuakeFireChance * severity
                    && DisasterModel.Ignite(_rules, world.Regions.Cells[settlement.RegionId], _rules.IgnitionIntensity))
                {
                    events.Add(new WildfireStarted { Tick = world.Tick, RegionId = settlement.RegionId, Cause = FireCause.Earthquake });
                }
            }
        }
    }

    /// <summary>Fire in a settlement's own region burns its houses, people and stores.</summary>
    private void BurnSettlements(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        foreach (var settlement in world.Settlements)
        {
            var fire = world.Regions.Cells[settlement.RegionId].Fire * _damage.Protection(world, settlement);
            if (fire <= 0f)
            {
                continue;
            }

            var severity = fire * seconds * _damage.Exposure(settlement, DisasterKind.Fire);
            var houses = _damage.Destroy(settlement, random, _rules.FireHousingLossPerSecond * (1f - _rules.HouseFireResistance) * severity * _damage.Fragility(world, settlement));
            var deaths = DisasterDamage.Kill(settlement, random, _rules.FireDeathsPerSecond * severity);
            settlement.Food *= Math.Max(0f, 1f - (_rules.FireFoodLossPerSecond * severity * _damage.FoodLoss(settlement)));
            _damage.Frighten(settlement, _rules.FireDreadPerSecond * severity);
            DisasterDamage.Report(world, settlement, DisasterKind.Fire, houses, deaths, events);
        }
    }
}
