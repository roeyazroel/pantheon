using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Ecology;

/// <summary>
/// One ecology step (spec §18, ADR-028): fires and floods harm the land and kill its animals; harm
/// heals, but repeated harm scars the land for good, and only a god of nature heals scars; wild
/// animals return as far as the land allows (villages hunt them in their own step,
/// <see cref="EcologyModel.Hunt"/>); crowded villages pollute their land, which clears slowly. It
/// raises <see cref="LandScarred"/> when a village's land is scarred.
/// </summary>
public sealed class EcologySystem : ISimulationSystem
{
    private readonly EcologyRules _rules;
    private readonly SettlementRules _settlementRules;

    /// <summary>Creates the system.</summary>
    /// <param name="rules">Ecology rules.</param>
    /// <param name="settlementRules">Settlement rules, for the land a village lives from.</param>
    public EcologySystem(EcologyRules rules, SettlementRules settlementRules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentOutOfRangeException.ThrowIfLessThan(rules.StepIntervalTicks, 1);
        _rules = rules;
        _settlementRules = settlementRules;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var seconds = _rules.StepIntervalTicks * SimulationTime.SecondsPerTick;
        var scarsBefore = Scars(world);
        var nature = NatureLand(world);
        foreach (var region in world.Regions.Cells)
        {
            if (region.Biome != Biome.Sea)
            {
                Live(region, seconds, nature.Contains(region.Id));
            }
        }

        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population > 0)
            {
                Pollute(world.Regions.Cells[settlement.RegionId], settlement.Population, seconds);
            }
        }

        ReportScars(world, scarsBefore, context.Events);
    }

    /// <summary>The mean scar of the land a village lives from.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="settlement">The village.</param>
    /// <param name="radius">Its farmland radius in regions.</param>
    /// <returns>Mean scar (0..1) of its land regions.</returns>
    public static float ScarOf(RegionGrid grid, SettlementState settlement, int radius)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settlement);
        var total = 0f;
        var count = 0;
        foreach (var region in EcologyModel.Around(grid, settlement.RegionId, radius))
        {
            if (region.Biome != Biome.Sea)
            {
                total += region.Scar;
                count++;
            }
        }

        return count > 0 ? total / count : 0f;
    }

    /// <summary>A region's step: fire and flood harm it, harm heals or scars, animals return or die, pollution clears.</summary>
    private void Live(RegionState region, float seconds, bool nature)
    {
        var fire = SimMath.Clamp01(region.Fire);
        region.Damage = SimMath.Clamp01(region.Damage + (((_rules.FireDamagePerSecond * fire) + (_rules.FloodDamagePerSecond * SimMath.Clamp01(region.Flood))) * seconds));
        (region.Damage, region.Scar) = EcologyModel.Heal(_rules, region.Damage, region.Scar, seconds, nature);
        var capacity = EcologyModel.WildlifeCapacity(region) * (1f - (_settlementRules.Resources.ClearedWildlife * Resources.ResourceModel.Cleared(region)));
        var wildlife = SimMath.Clamp01(region.Wildlife) - (_rules.FireWildlifeLossPerSecond * fire * seconds);
        wildlife += (capacity - wildlife) * Math.Min(1f, _rules.WildlifeGrowthPerSecond * seconds);
        region.Wildlife = Math.Abs(capacity - wildlife) < EcologyModel.Settled ? capacity : Math.Clamp(wildlife, 0f, Math.Max(capacity, 0f));
        var pollution = region.Pollution * (1f - Math.Min(1f, _rules.PollutionClearPerSecond * seconds));
        region.Pollution = pollution < EcologyModel.Settled ? 0f : pollution;
    }

    /// <summary>A crowded village pollutes its own region.</summary>
    private void Pollute(RegionState region, int population, float seconds)
    {
        var crowd = Math.Max(0, population - _rules.PollutionFreePeople) / 1000f;
        region.Pollution = SimMath.Clamp01(region.Pollution + (_rules.PollutionPerThousandPerSecond * crowd * seconds));
    }

    /// <summary>The regions around a village whose majority follows a god of nature.</summary>
    private HashSet<int> NatureLand(WorldState world)
    {
        var land = new HashSet<int>();
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population > 0 && DomainModel.MajorityHolds(world, settlement, GodDomain.Nature))
            {
                foreach (var region in EcologyModel.Around(world.Regions, settlement.RegionId, _settlementRules.FarmlandRadius))
                {
                    land.Add(region.Id);
                }
            }
        }

        return land;
    }

    /// <summary>The mean scar of every village's land, in list order.</summary>
    private float[] Scars(WorldState world)
    {
        var scars = new float[world.Settlements.Count];
        for (var i = 0; i < scars.Length; i++)
        {
            scars[i] = ScarOf(world.Regions, world.Settlements[i], _settlementRules.FarmlandRadius);
        }

        return scars;
    }

    /// <summary>Reports every village whose land's scar rose past the notice.</summary>
    private void ReportScars(WorldState world, float[] before, List<WorldEvent> events)
    {
        for (var i = 0; i < before.Length; i++)
        {
            var settlement = world.Settlements[i];
            var after = ScarOf(world.Regions, settlement, _settlementRules.FarmlandRadius);
            if (before[i] < _rules.ScarNotice && after >= _rules.ScarNotice)
            {
                events.Add(new LandScarred { Tick = world.Tick, SettlementId = settlement.Id, Scar = after });
            }
        }
    }
}
