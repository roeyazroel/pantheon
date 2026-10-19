using System.Collections.Generic;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Tests.Support;

/// <summary>Pinned hazard rules and hand-shaped land for the hazard tests (ADR-029).</summary>
internal static class HazardWorlds
{
    /// <summary>
    /// The hazards shipped with Milestone 15 part 9, every chance off, so each test turns on only the
    /// hazard it is about and keeps its meaning when <c>disasters.json</c> is retuned.
    /// </summary>
    public static HazardRules Hazards { get; } = new()
    {
        Tsunami = new TsunamiRules { MinMagnitude = 6f, CoastDistance = 250f, RadiusPerMagnitude = 180f, HeightPerMagnitude = 2.5f, HousingLoss = 0.35f, Deaths = 0.06f },
        Landslide = new LandslideRules { SlidePerSecond = 0f, MinDrop = 12f, WetSoil = 0.85f, ShakeChance = 0f, HousingLoss = 0.4f, Deaths = 0.05f, Damage = 0.5f, RubbleClearPerSecond = 0.004f },
        Volcano = new VolcanoRules
        {
            EruptionsPerHour = 0f, MinElevation = 60f, RumbleSeconds = 40f, EruptSeconds = 60f, TremorSeconds = 8f, TremorMagnitude = 4.8f, LavaRegions = 6, AshRadius = 350f,
            AshDamagePerSecond = 0.004f, AshHealthLossPerSecond = 0.002f, AshFoodLossPerSecond = 0.004f, CooldownSeconds = 1200f,
        },
        Tornado = new TornadoRules { PerStormPerSecond = 0f, MinTemperature = 0.6f, Radius = 45f, MinSeconds = 20f, MaxSeconds = 45f, Speed = 14f, HousingLossPerSecond = 0.08f, DeathsPerSecond = 0.004f, DamagePerSecond = 0.05f },
        Meteor = new MeteorRules { PerHour = 0f, Radius = 160f, Magnitude = 5.8f, HousingLoss = 0.7f, Deaths = 0.2f, Scar = 0.5f },
        Blight = new BlightRules { EmergencePerSecond = 0f, MinTemperature = 0.6f, WetSoil = 0.6f, GrowthPerSecond = 0.006f, SpreadPerSecond = 0.05f, DeclinePerSecond = 0.02f, BlessingCurePerSecond = 0.1f, Notice = 0.25f },
        Locusts = new LocustRules { SwarmsPerHour = 0f, MinTemperature = 0.65f, MaxSoil = 0.3f, Radius = 220f, Speed = 5f, MinSeconds = 90f, MaxSeconds = 180f, FoodEatenPerSecond = 0.004f, RainLossPerSecond = 0.05f, DamagePerSecond = 0.002f },
        MarkSeconds = 600f,
    };

    /// <summary>Disaster rules with no earthquakes or fires of their own and the pinned hazards, the preparations shipped with part 9.</summary>
    public static DisasterRules Rules { get; } = DisasterWorlds.Rules with
    {
        EarthquakesPerHour = 0f,
        HearthFirePerSecond = 0f,
        LightningIgnitionPerSecond = 0f,
        Hazards = Hazards,
        Preparedness = Preparedness.None with { SeaWallDamage = 0.6f, DamDamage = 0.3f, EvacuationDeaths = 0.5f },
    };

    /// <summary>Runs hazard steps, one second apart.</summary>
    /// <param name="world">The world.</param>
    /// <param name="steps">How many.</param>
    /// <param name="rules">Rules; <see cref="Rules"/> when <c>null</c>.</param>
    /// <param name="events">Where the events go; dropped when <c>null</c>.</param>
    public static void Step(WorldState world, int steps = 1, DisasterRules? rules = null, List<WorldEvent>? events = null)
    {
        var system = new HazardSystem(rules ?? Rules, SettlementWorlds.Rules, DivineWorlds.Rules, WeatherWorlds.Rules, Rules.StepIntervalTicks);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events ?? []));
            world.Tick += Rules.StepIntervalTicks;
        }
    }

    /// <summary>Raises a mountain: the land climbs from the west edge to a peak in the east.</summary>
    /// <param name="world">A world from <see cref="DivineWorlds.Create"/>.</param>
    /// <param name="peak">The peak's height in metres.</param>
    public static void Mountain(WorldState world, float peak)
    {
        var grid = world.Regions;
        foreach (var region in grid.Cells)
        {
            var (column, _) = grid.GetCoordinates(region.Id);
            region.Elevation = 10f + ((peak - 10f) * column / (grid.Columns - 1));
        }
    }

    /// <summary>Turns the westmost columns into sea, a metre below its surface.</summary>
    /// <param name="world">The world.</param>
    /// <param name="columns">How many columns.</param>
    public static void Coast(WorldState world, int columns)
    {
        var grid = world.Regions;
        foreach (var region in grid.Cells)
        {
            var (column, _) = grid.GetCoordinates(region.Id);
            if (column < columns)
            {
                (region.Biome, region.Elevation) = (Biome.Sea, -1f);
            }
            else
            {
                region.Elevation = 1f + (column - columns);
            }
        }
    }

    /// <summary>Raises a building in a settlement.</summary>
    /// <param name="world">The world.</param>
    /// <param name="index">The settlement's place in the list.</param>
    /// <param name="kind">The building.</param>
    public static void Build(WorldState world, int index, BuildingKind kind) =>
        world.Settlements[index].Society.Buildings.Add(new Building { Kind = kind, GodId = null });
}
