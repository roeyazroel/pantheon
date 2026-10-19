using System;
using Pantheon.Domain.Roads;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Navigation;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Villagers;

/// <summary>What followed villagers have in common (spec §80, ADR-043): their age, memories, danger and ways.</summary>
public static class VillagerModel
{
    /// <summary>Purpose name of the villagers' random streams.</summary>
    public const string RandomStream = "villagers";

    /// <summary>A villager's age now, in years.</summary>
    /// <param name="rules">Villager rules.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="tick">The current tick.</param>
    /// <returns>Their age.</returns>
    public static float Age(VillagerRules rules, VillagerState villager, long tick)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(villager);
        return villager.AgeYears + ((tick - villager.FollowedTick) * SimulationTime.SecondsPerTick / Math.Max(1f, rules.SecondsPerYear));
    }

    /// <summary>Whether a villager remembers one of these deeds from after a tick.</summary>
    /// <param name="villager">The villager.</param>
    /// <param name="since">The earliest tick that counts.</param>
    /// <param name="deeds">The deeds.</param>
    /// <returns><c>true</c> when they do.</returns>
    public static bool Remembers(VillagerState villager, long since, params VillagerDeed[] deeds)
    {
        ArgumentNullException.ThrowIfNull(villager);
        return villager.Memories.Exists(m => m.Tick >= since && Array.IndexOf(deeds, m.Deed) >= 0);
    }

    /// <summary>A villager remembers something, forgetting the oldest beyond their capacity.</summary>
    /// <param name="rules">Villager rules.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="memory">What.</param>
    public static void Remember(VillagerRules rules, VillagerState villager, VillagerMemory memory)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(villager);
        villager.Memories.Add(memory);
        while (villager.Memories.Count > Math.Max(1, rules.MemoryCapacity))
        {
            villager.Memories.RemoveAt(0);
        }
    }

    /// <summary>
    /// A villager remembers something unless they remember the same thing from the last while; then
    /// the people it concerned are added to that memory.
    /// </summary>
    /// <param name="rules">Villager rules.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="memory">What.</param>
    /// <param name="withinSeconds">How long the same thing is one memory.</param>
    public static void RememberOnce(VillagerRules rules, VillagerState villager, VillagerMemory memory, float withinSeconds)
    {
        ArgumentNullException.ThrowIfNull(villager);
        ArgumentNullException.ThrowIfNull(memory);
        var since = memory.Tick - (long)(withinSeconds * SimulationTime.TicksPerSecond);
        var same = villager.Memories.FindLast(m => m.Tick >= since && m.Deed == memory.Deed && m.OtherId == memory.OtherId
            && string.Equals(m.Detail, memory.Detail, StringComparison.Ordinal) && m.SettlementId == memory.SettlementId);
        if (same is not null)
        {
            same.Count += memory.Count;
            return;
        }

        Remember(rules, villager, memory);
    }

    /// <summary>Whether a villager is in their village.</summary>
    /// <param name="rules">Villager rules.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="home">Their village, if it is there.</param>
    /// <returns><c>true</c> within <see cref="VillagerRules.VillageRadius"/> of its centre.</returns>
    public static bool InVillage(VillagerRules rules, VillagerState villager, SettlementState? home)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(villager);
        return home is not null && Distance(villager.Position, home.Position) <= rules.VillageRadius;
    }

    /// <summary>
    /// The danger where a villager stands and what it is: their village's fire, shaking or flood
    /// while they are in it, else the fire, flood or shaking of the land underfoot.
    /// </summary>
    /// <param name="rules">Settlement rules, with the villager rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="home">Their village, if it is there.</param>
    /// <returns>The danger (0..1) and its kind.</returns>
    public static (float Danger, VillagerFate Cause) DangerTo(SettlementRules rules, WorldState world, VillagerState villager, SettlementState? home)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(villager);
        if (InVillage(rules.Villagers, villager, home))
        {
            return Worst(
                DisasterModel.FireThreat(rules, world.Regions, home!),
                SimMath.Clamp01(world.Regions.GetRegion(home!.RegionId).Flood),
                DisasterModel.ShakingAt(world.Disasters, home.Position));
        }

        var region = world.Regions.TryGetRegionIdAt(villager.Position.X, villager.Position.Z, out var id) ? world.Regions.Cells[id] : null;
        return Worst(SimMath.Clamp01(region?.Fire ?? 0f), SimMath.Clamp01(region?.Flood ?? 0f), DisasterModel.ShakingAt(world.Disasters, villager.Position));
    }

    /// <summary>The danger at which a villager flees: less for one who remembers danger lived through or fled.</summary>
    /// <param name="rules">Villager rules.</param>
    /// <param name="villager">The villager.</param>
    /// <returns>The danger.</returns>
    public static float FleeAt(VillagerRules rules, VillagerState villager)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Wary(villager) ? rules.FleeDanger * rules.WaryFlee : rules.FleeDanger;
    }

    /// <summary>Whether a villager remembers danger lived through or fled, and so flees it sooner.</summary>
    /// <param name="villager">The villager.</param>
    /// <returns><c>true</c> when they do.</returns>
    public static bool Wary(VillagerState villager) => Remembers(villager, long.MinValue, VillagerDeed.Survived, VillagerDeed.Fled);

    /// <summary>Moves a villager along their way by a distance.</summary>
    /// <param name="villager">The villager.</param>
    /// <param name="distance">Metres.</param>
    public static void Walk(VillagerState villager, float distance)
    {
        ArgumentNullException.ThrowIfNull(villager);
        while (distance > 0f && villager.Path.Count > 0)
        {
            var next = villager.Path[0];
            var length = Distance(villager.Position, next);
            if (length <= distance)
            {
                villager.Position = next;
                villager.Path.RemoveAt(0);
                distance -= length;
            }
            else
            {
                villager.Position = new GroundPosition(
                    villager.Position.X + ((next.X - villager.Position.X) / length * distance),
                    villager.Position.Z + ((next.Z - villager.Position.Z) / length * distance));
                distance = 0f;
            }
        }
    }

    /// <summary>Sets a villager on their way to a point: by the open road between two villages where there is one, else across the land.</summary>
    /// <param name="world">World state.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="to">Where.</param>
    /// <param name="roads">Road rules.</param>
    /// <param name="fromId">The village they leave, if any.</param>
    /// <param name="toId">The village they go to, if any.</param>
    /// <returns>Whether a way was found; without one they stay.</returns>
    public static bool SetOut(WorldState world, VillagerState villager, GroundPosition to, RoadRules roads, int? fromId, int? toId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(villager);
        villager.Path.Clear();
        var road = fromId is { } from && toId is { } target && from != target ? Roads.RoadModel.Way(roads, world, from, target) : null;
        if (road is { Count: > 1 })
        {
            villager.Path.AddRange(road);
            villager.Path.Add(to);
            return true;
        }

        if (RegionPathfinder.FindPath(world.Regions, villager.Position, to) is not { } way)
        {
            return false;
        }

        villager.Path.AddRange(way);
        return true;
    }

    /// <summary>A villager's home, work and shrine in a village they move to, the same each time for the same villager and village.</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="village">The village.</param>
    /// <returns>The three places.</returns>
    public static (GroundPosition Home, GroundPosition Work, GroundPosition Shrine) Spots(WorldSeed seed, VillagerState villager, SettlementState village)
    {
        ArgumentNullException.ThrowIfNull(villager);
        ArgumentNullException.ThrowIfNull(village);
        var random = new SimulationRandom(SeedMixer.Derive(seed, RandomStream, ((long)villager.Id << 24) + village.Id));
        var (near, far) = villager.Work switch
        {
            VillagerWork.Farming => (22f, 36f),
            VillagerWork.Building => (6f, 12f),
            VillagerWork.Chores => (8f, 16f),
        };
        return (Around(village.Position, 8f, 18f, random), Around(village.Position, near, far, random), Around(village.Position, 3f, 5f, random));
    }

    /// <summary>Metres between two points on the ground.</summary>
    /// <param name="a">One point.</param>
    /// <param name="b">The other.</param>
    /// <returns>The distance.</returns>
    public static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    /// <summary>Seconds as whole ticks, at least one.</summary>
    internal static int Ticks(float seconds) => Math.Max(1, (int)MathF.Round(seconds * SimulationTime.TicksPerSecond));

    /// <summary>The worst of fire, flood and shaking.</summary>
    private static (float, VillagerFate) Worst(float fire, float flood, float shaking) =>
        fire >= flood && fire >= shaking ? (fire, VillagerFate.Fire) : flood >= shaking ? (flood, VillagerFate.Flood) : (shaking, VillagerFate.Earthquake);

    /// <summary>A point at a random distance between two radii from a centre.</summary>
    private static GroundPosition Around(GroundPosition centre, float near, float far, SimulationRandom random)
    {
        var angle = random.NextFloat() * MathF.Tau;
        var radius = near + ((far - near) * random.NextFloat());
        return new GroundPosition(centre.X + (MathF.Cos(angle) * radius), centre.Z + (MathF.Sin(angle) * radius));
    }
}
