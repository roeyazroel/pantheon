using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Disasters;

/// <summary>
/// One hazard step (spec §19–20, §64, ADR-029), after the disaster step: earthquakes that began crack
/// the land and, near the coast, raise tsunamis; the volcano rumbles and erupts; soaked or shaken
/// slopes slide, burying what lies below and blocking the way; meteors strike; tornadoes, locusts and
/// blight run their course (<see cref="LivingHazards"/>); and the marks on the land fade.
/// </summary>
/// <remarks>Like the other disasters, it draws from a stream derived from the world seed and the tick.</remarks>
public sealed class HazardSystem : ISimulationSystem
{
    /// <summary>Purpose name of the hazards' random streams.</summary>
    public const string RandomStream = "hazards";

    private readonly DisasterRules _rules;
    private readonly HazardRules _hazards;
    private readonly DisasterDamage _damage;
    private readonly LivingHazards _living;
    private readonly int _intervalTicks;

    /// <summary>Creates the system.</summary>
    /// <param name="rules">Disaster rules, with the hazards and the preparations.</param>
    /// <param name="settlementRules">Settlement rules, for houses and farmland.</param>
    /// <param name="divine">Divine rules, for how disasters shake belief.</param>
    /// <param name="weather">Weather rules, for thunderstorms, rain and the season's warmth.</param>
    /// <param name="intervalTicks">Ticks between its steps.</param>
    /// <param name="domains">Domain rules, for the houses the Protection domain saves; <c>null</c> for none.</param>
    /// <param name="callings">Creatures' paths, for the Guardians that shield their villages (ADR-030); <c>null</c> for none.</param>
    public HazardSystem(DisasterRules rules, SettlementRules settlementRules, DivineRules divine, WeatherRules weather, int intervalTicks, Domain.Gods.DomainRules? domains = null, Domain.Creatures.CallingRules? callings = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(weather);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _rules = rules;
        _hazards = rules.Hazards;
        _damage = new DisasterDamage(rules, settlementRules, divine, domains, callings);
        _living = new LivingHazards(rules, settlementRules, weather, _damage);
        _intervalTicks = intervalTicks;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, RandomStream, world.Tick));
        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        NewQuakes(world, random, context.Events);
        Volcano(world, random, seconds, context.Events);
        Landslides(world, random, seconds, context.Events);
        Meteors(world, random, seconds, context.Events);
        _living.Step(world, random, seconds, context.Events);
        var oldest = world.Tick - HazardModel.Ticks(_hazards.MarkSeconds);
        world.Disasters.Marks.RemoveAll(mark => mark.Tick < oldest);
    }

    /// <summary>Every earthquake that began since the last step cracks the land, and one near the coast raises a wave.</summary>
    private void NewQuakes(WorldState world, SimulationRandom random, List<WorldEvent> events)
    {
        var shaking = HazardModel.Ticks(_rules.ShakingSeconds);
        foreach (var quake in world.Disasters.Earthquakes.ToArray())
        {
            if (quake.EndsAtTick - shaking <= world.Tick - _intervalTicks)
            {
                continue;
            }

            var epicentre = new GroundPosition(quake.X, quake.Z);
            HazardModel.AddMark(world, TerrainMarkKind.Crack, epicentre, Math.Max(20f, quake.Radius * 0.25f));
            if (HazardModel.RaiseTsunami(_hazards.Tsunami, world, epicentre, quake.Magnitude) is not { } wave)
            {
                continue;
            }

            events.Add(new TsunamiStruck { Tick = world.Tick, Shore = wave.Shore, RegionId = wave.RegionId });
            foreach (var settlement in world.Settlements)
            {
                var flood = world.Regions.Cells[settlement.RegionId].Flood * HazardModel.Falloff(settlement.Position, wave.Shore, wave.Radius * 1.5f);
                _damage.Strike(world, settlement, DisasterKind.Tsunami, _hazards.Tsunami.HousingLoss, _hazards.Tsunami.Deaths, flood, random, events);
            }
        }
    }

    /// <summary>The volcano sleeps, wakes by chance, rumbles with tremors, then erupts: lava down its slope, ash around it.</summary>
    private void Volcano(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        var rules = _hazards.Volcano;
        if (rules.EruptionsPerHour <= 0f && world.Disasters.Volcano is not { Phase: not VolcanoPhase.Dormant })
        {
            return;
        }

        if (HazardModel.Find(rules, world) is not { } volcano)
        {
            return;
        }

        var crater = world.Regions.GetRegionCenter(volcano.RegionId);
        switch (volcano.Phase)
        {
            case VolcanoPhase.Dormant:
                if (world.Tick >= volcano.PhaseEndsTick && random.NextDouble() < rules.EruptionsPerHour / 3600.0 * seconds)
                {
                    HazardModel.Wake(rules, world);
                    events.Add(new VolcanoChanged { Tick = world.Tick, RegionId = volcano.RegionId, Phase = VolcanoPhase.Rumbling });
                }

                break;
            case VolcanoPhase.Rumbling:
                if (world.Tick >= volcano.NextTremorTick)
                {
                    var tremor = DisasterModel.StartQuake(_rules, world.Disasters, crater, rules.TremorMagnitude, world.Tick, godId: null);
                    events.Add(new EarthquakeStruck { Tick = world.Tick, QuakeId = tremor.Id, Epicentre = crater, Magnitude = tremor.Magnitude, GodId = null });
                    volcano.NextTremorTick = world.Tick + HazardModel.Ticks(rules.TremorSeconds);
                }

                if (world.Tick >= volcano.PhaseEndsTick)
                {
                    (volcano.Phase, volcano.PhaseEndsTick) = (VolcanoPhase.Erupting, world.Tick + HazardModel.Ticks(rules.EruptSeconds));
                    events.Add(new VolcanoChanged { Tick = world.Tick, RegionId = volcano.RegionId, Phase = VolcanoPhase.Erupting });
                }

                break;
            case VolcanoPhase.Erupting:
                Erupt(world, volcano, rules, crater, seconds);
                if (world.Tick >= volcano.PhaseEndsTick)
                {
                    (volcano.Phase, volcano.PhaseEndsTick) = (VolcanoPhase.Dormant, world.Tick + HazardModel.Ticks(rules.CooldownSeconds));
                    events.Add(new VolcanoChanged { Tick = world.Tick, RegionId = volcano.RegionId, Phase = VolcanoPhase.Dormant });
                }

                break;
        }
    }

    /// <summary>Lava burns everything down the steepest slope and kills the land's life; ash harms the land, sickens the people and spoils their stores.</summary>
    private void Erupt(WorldState world, Volcano volcano, VolcanoRules rules, GroundPosition crater, float seconds)
    {
        foreach (var regionId in HazardModel.LavaPath(world.Regions, volcano.RegionId, rules.LavaRegions))
        {
            var region = world.Regions.Cells[regionId];
            (region.Fire, region.Scorch, region.Snow, region.Wildlife, region.Damage) = (1f, 1f, 0f, 0f, 1f);
        }

        foreach (var region in world.Regions.Cells)
        {
            var ash = HazardModel.Falloff(world.Regions.GetRegionCenter(region.Id), crater, rules.AshRadius);
            if (ash > 0f && region.Biome != Biome.Sea)
            {
                region.Damage = SimMath.Clamp01(region.Damage + (rules.AshDamagePerSecond * ash * seconds));
            }
        }

        foreach (var settlement in world.Settlements)
        {
            var ash = HazardModel.Falloff(settlement.Position, crater, rules.AshRadius) * _damage.Protection(world, settlement);
            if (ash > 0f && settlement.Population > 0)
            {
                settlement.Health = Math.Max(0f, settlement.Health - (rules.AshHealthLossPerSecond * ash * seconds));
                settlement.Food *= Math.Max(0f, 1f - (rules.AshFoodLossPerSecond * ash * seconds * _damage.FoodLoss(settlement)));
                _damage.Frighten(settlement, ash * seconds * 0.1f);
            }
        }
    }

    /// <summary>Rubble clears; a steep slope gives way when soaked, or when an earthquake shakes it, burying what lies below.</summary>
    private void Landslides(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        var rules = _hazards.Landslide;
        var grid = world.Regions;
        foreach (var region in grid.Cells)
        {
            if (region.Rubble > 0f)
            {
                var rubble = region.Rubble - (rules.RubbleClearPerSecond * seconds);
                region.Rubble = rubble < 0.01f ? 0f : rubble;
            }
        }

        if (rules.SlidePerSecond <= 0f && rules.ShakeChance <= 0f)
        {
            return;
        }

        var shaken = world.Disasters.Earthquakes.Count > 0;
        foreach (var region in grid.Cells)
        {
            if (region.Biome == Biome.Sea || region.Rubble > 0f)
            {
                continue;
            }

            // The slope is looked at only where the soil is soaked or the ground shakes, the few regions that could slide.
            var shaking = shaken ? DisasterModel.ShakingAt(world.Disasters, grid.GetRegionCenter(region.Id)) : 0f;
            var soaked = region.SoilMoisture >= rules.WetSoil ? rules.SlidePerSecond * seconds : 0f;
            var chance = soaked + (rules.ShakeChance * shaking * seconds);
            if (chance <= 0f || HazardModel.Downhill(grid, region.Id) is not { } below || below.Drop < rules.MinDrop || random.NextDouble() >= chance)
            {
                continue;
            }

            HazardModel.Slide(rules, world, region.Id, below.RegionId);
            events.Add(new LandslideStruck { Tick = world.Tick, RegionId = region.Id, ToRegionId = below.RegionId });
            foreach (var settlement in world.Settlements)
            {
                var severity = settlement.RegionId == below.RegionId ? 1f : settlement.RegionId == region.Id ? 0.5f : 0f;
                _damage.Strike(world, settlement, DisasterKind.Landslide, rules.HousingLoss, rules.Deaths, severity, random, events);
            }
        }
    }

    /// <summary>Now and then a meteor strikes the land: a crater, fire, a quake, and ruin around it.</summary>
    private void Meteors(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        var rules = _hazards.Meteor;
        if (rules.PerHour <= 0f || random.NextDouble() >= rules.PerHour / 3600.0 * seconds)
        {
            return;
        }

        var land = world.Regions.Cells.FindAll(region => region.Biome != Biome.Sea);
        if (land.Count == 0)
        {
            return;
        }

        var centre = world.Regions.GetRegionCenter(land[random.NextInt(land.Count)].Id);
        var point = new GroundPosition(centre.X + ((random.NextFloat() - 0.5f) * world.Regions.RegionSize), centre.Z + ((random.NextFloat() - 0.5f) * world.Regions.RegionSize));
        var quake = HazardModel.Impact(_rules, world, point);
        events.Add(new MeteorStruck { Tick = world.Tick, Position = point });
        events.Add(new EarthquakeStruck { Tick = world.Tick, QuakeId = quake.Id, Epicentre = point, Magnitude = quake.Magnitude, GodId = null });
        foreach (var settlement in world.Settlements)
        {
            _damage.Strike(world, settlement, DisasterKind.Meteor, rules.HousingLoss, rules.Deaths, HazardModel.Falloff(settlement.Position, point, rules.Radius), random, events);
        }
    }
}
