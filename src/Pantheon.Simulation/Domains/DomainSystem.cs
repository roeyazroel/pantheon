using System;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Weather;

namespace Pantheon.Simulation.Domains;

/// <summary>
/// The lasting boons of the gods' domains (ADR-021), once every belief step. A boon reaches the
/// settlements where a god holding the domain has the majority: Nature keeps their farmland
/// blessed, Life heals them, Storm turns a storm over them into awe, Fire dies down on their
/// land, Ocean feeds them from the waters around them and Justice keeps them orderly (ADR-022).
/// Prosperity raises the god's own power. The boons that change one act (War, Death,
/// Protection, Knowledge, Trickery and Chaos) live with that act.
/// </summary>
public sealed class DomainSystem : ISimulationSystem
{
    private readonly DomainRules _rules;
    private readonly DivineRules _divine;
    private readonly SettlementRules _settlementRules;
    private readonly WeatherRules? _weather;

    /// <summary>Creates the system over validated rules.</summary>
    /// <param name="rules">Domain rules.</param>
    /// <param name="divine">Divine rules, for the power worship raises.</param>
    /// <param name="settlementRules">Settlement rules, for the farmland's radius and the workforce.</param>
    /// <param name="weather">Weather rules, for the storms over a settlement; <c>null</c> where there is no weather.</param>
    public DomainSystem(DomainRules rules, DivineRules divine, SettlementRules settlementRules, WeatherRules? weather)
    {
        DomainRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(settlementRules);
        _rules = rules;
        _divine = divine;
        _settlementRules = settlementRules;
        _weather = weather;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population > 0 && DomainModel.MajorityGodId(settlement) is { } godId && GodStanding.Find(world, godId) is { Domains.Count: > 0 } god)
            {
                Bless(world, settlement, god);
            }
        }

        foreach (var god in world.Gods)
        {
            if (DomainModel.Holds(god, GodDomain.Prosperity))
            {
                god.DivinePower += _rules.Boons.ProsperityPower * GodStanding.ComputePowerPerStep(_divine, _settlementRules, world, god.Id);
            }
        }
    }

    /// <summary>The boons of the majority's god on one settlement and its farmland.</summary>
    private void Bless(WorldState world, SettlementState settlement, GodState god)
    {
        var boons = _rules.Boons;
        if (DomainModel.Holds(god, GodDomain.Nature) || DomainModel.Holds(god, GodDomain.Fire))
        {
            ForEachFarmland(world.Regions, settlement.RegionId, _settlementRules.FarmlandRadius, region =>
            {
                if (DomainModel.Holds(god, GodDomain.Nature))
                {
                    region.Blessing = Math.Max(region.Blessing, boons.NatureBlessing);
                }

                if (DomainModel.Holds(god, GodDomain.Fire) && region.Fire > 0f)
                {
                    region.Fire *= 1f - boons.FireQuenchPerStep;
                }
            });
        }

        if (DomainModel.Holds(god, GodDomain.Life))
        {
            settlement.Health = BeliefModel.ApplyChange(settlement.Health, boons.LifeHealthPerStep);
        }

        if (DomainModel.Holds(god, GodDomain.Ocean))
        {
            var catches = Math.Min(boons.OceanMaxWaterRegions, CountWaters(world.Regions, settlement.RegionId, _settlementRules.FarmlandRadius));
            var storage = Settlements.SettlementEconomy.StorageOf(_settlementRules, settlement);
            settlement.Food = Math.Max(settlement.Food, Math.Min(storage, settlement.Food + (boons.OceanFishPerWaterRegion * catches)));
        }

        if (DomainModel.Holds(god, GodDomain.Justice))
        {
            settlement.Emigration *= 1f - boons.JusticeCalmPerStep;
            if (BeliefModel.Find(settlement, god.Id) is { } follower)
            {
                follower.Doubt = BeliefModel.ApplyChange(follower.Doubt, -boons.JusticeDoubtFadePerStep);
            }
        }

        if (DomainModel.Holds(god, GodDomain.Storm) && _weather is not null && BeliefModel.Find(settlement, god.Id) is { } belief)
        {
            var point = WeatherModel.At(_weather, world.Weather, settlement.Position);
            var storm = Math.Max(point.Lightning, point.Wind);
            if (storm > 0f)
            {
                belief.Faith = BeliefModel.ApplyChange(belief.Faith, boons.StormAwePerStep * storm);
                belief.Fear = BeliefModel.ApplyChange(belief.Fear, boons.StormAwePerStep * storm);
            }
        }
    }

    /// <summary>The sea regions and the regions with a river or lake within a Chebyshev radius of a region: the waters a village fishes (ADR-022).</summary>
    private static int CountWaters(RegionGrid grid, int regionId, int radius)
    {
        var (column, row) = grid.GetCoordinates(regionId);
        var waters = 0;
        for (var r = Math.Max(0, row - radius); r <= Math.Min(grid.Rows - 1, row + radius); r++)
        {
            for (var c = Math.Max(0, column - radius); c <= Math.Min(grid.Columns - 1, column + radius); c++)
            {
                var region = grid.Cells[(r * grid.Columns) + c];
                if (region.Biome == Domain.Ecology.Biome.Sea || region.FreshWater >= 0.5f)
                {
                    waters++;
                }
            }
        }

        return waters;
    }

    /// <summary>Runs an action on every land region within a Chebyshev radius of a region, as the farmland survey counts them.</summary>
    private static void ForEachFarmland(RegionGrid grid, int regionId, int radius, Action<RegionState> action)
    {
        var (column, row) = grid.GetCoordinates(regionId);
        for (var r = Math.Max(0, row - radius); r <= Math.Min(grid.Rows - 1, row + radius); r++)
        {
            for (var c = Math.Max(0, column - radius); c <= Math.Min(grid.Columns - 1, column + radius); c++)
            {
                var region = grid.Cells[(r * grid.Columns) + c];
                if (region.Biome != Domain.Ecology.Biome.Sea)
                {
                    action(region);
                }
            }
        }
    }
}
