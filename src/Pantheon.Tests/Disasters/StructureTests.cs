using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Water;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>The structural integrity of villages' houses and their resistance to floods (spec §94, ADR-048).</summary>
public sealed class StructureTests
{
    private static readonly StructureRules Structure = new() { CrackPerShare = 2f, Weakness = 1f, RepairPerSecond = 0.002f };

    private static readonly DisasterRules Rules = DisasterWorlds.Rules with { EarthquakesPerHour = 0f, HearthFirePerSecond = 0f, Structure = Structure };

    [Fact]
    public void AQuake_CracksTheHousesItLeavesStanding_UnlessHousesNeverCrack()
    {
        var integrity = new List<float>();
        foreach (var rules in new[] { Rules, Rules with { Structure = StructureRules.None } })
        {
            var world = DivineWorlds.Create(0f, 300f);
            var village = world.Settlements[0];
            DisasterModel.StartQuake(rules, world.Disasters, village.Position, 7.5f, world.Tick, null);
            Step(world, rules, steps: 12);
            integrity.Add(village.Integrity);
        }

        Assert.InRange(integrity[0], 0f, 0.95f);
        Assert.Equal(1f, integrity[1]);
    }

    [Fact]
    public void CrackedHouses_FallMoreEasily_InTheNextQuake()
    {
        int Ruins(float integrity)
        {
            var ruins = 0;
            for (var trial = 0; trial < 20; trial++)
            {
                var world = DivineWorlds.Create(0f, 300f);
                world.Tick = trial * 1000;
                var village = world.Settlements[0];
                (village.Housing, village.Population, village.Integrity) = (1000, 900, integrity);
                DisasterModel.StartQuake(Rules, world.Disasters, village.Position, 6f, world.Tick, null);
                Step(world, Rules, steps: 1);
                ruins += village.Ruins;
            }

            return ruins;
        }

        var whole = Ruins(1f);
        var cracked = Ruins(0f);

        Assert.True(cracked > whole * 1.5f, $"{cracked} ruins of cracked houses against {whole} of whole ones");
    }

    [Fact]
    public void Villages_MendTheirCrackedHouses_OverTime()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        village.Integrity = 0.5f;

        Step(world, Rules, steps: 100);

        Assert.Equal(0.5f + (Structure.RepairPerSecond * 100 * Rules.StepIntervalTicks * SimulationTime.SecondsPerTick), village.Integrity, 3);
    }

    [Fact]
    public void FloodResistantHouses_StandUpToAFlood()
    {
        int RuinsAfterFlood(float resistance)
        {
            var world = DivineWorlds.Create(0f, 300f);
            var village = world.Settlements[0];
            (village.Housing, village.Population) = (1000, 900);
            var water = new WaterSystem(WaterWorlds.Rules, WeatherWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Rules, DisasterWorlds.Rules with { HouseFloodResistance = resistance });
            for (var i = 0; i < 60; i++)
            {
                world.Regions.Cells[village.RegionId].Flood = 1f;
                water.Update(new TickContext(world, new SimulationRandom(1), []));
                world.Tick += WaterWorlds.Rules.StepIntervalTicks;
            }

            return village.Ruins;
        }

        var bare = RuinsAfterFlood(0f);
        var resistant = RuinsAfterFlood(0.75f);

        Assert.True(bare > 20, $"{bare} ruins");
        Assert.True(resistant * 2 < bare, $"{resistant} ruins of resistant houses against {bare}");
    }

    /// <summary>Steps the disasters.</summary>
    private static void Step(WorldState world, DisasterRules rules, int steps)
    {
        var system = new DisasterSystem(rules, SettlementWorlds.Rules, DivineWorlds.Rules, WeatherWorlds.Rules);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), new List<WorldEvent>()));
            world.Tick += rules.StepIntervalTicks;
        }
    }
}
