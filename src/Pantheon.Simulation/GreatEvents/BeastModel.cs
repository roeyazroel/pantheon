using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>
/// The ancient beast as state changes (spec §52's ancient monster, ADR-035): its lair, found the first
/// time it is needed, far from the villages on the highest land; waking; the harm gods do it; and its
/// fall, whose glory each god shares by its harm.
/// </summary>
public static class BeastModel
{
    /// <summary>Purpose name of the beast's seed.</summary>
    public const string Purpose = "beast";

    // Regions a lair keeps from every village, where the land allows.
    private const int LairDistance = 4;

    /// <summary>The world's beast, found and put to sleep in its lair the first time it is needed; <c>null</c> without beast rules or land.</summary>
    /// <param name="rules">Beast rules.</param>
    /// <param name="world">World state.</param>
    /// <returns>The beast, or <c>null</c>.</returns>
    public static BeastState? Find(BeastRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        if (world.GreatEvents.Beast is { } beast || !rules.Enabled)
        {
            return world.GreatEvents.Beast;
        }

        var grid = world.Regions;
        var land = grid.Cells.Where(r => r.Biome != Biome.Sea).ToList();
        if (land.Count == 0)
        {
            return null;
        }

        var far = land.Where(r => world.Settlements.All(s => Apart(grid, r.Id, s.RegionId) >= LairDistance)).ToList();
        var lair = (far.Count > 0 ? far : land).OrderByDescending(r => r.Elevation).ThenBy(r => r.Id).First();
        world.GreatEvents.Beast = new BeastState
        {
            Name = rules.Names[(int)(SeedMixer.Derive(world.Seed, Purpose) % (ulong)rules.Names.Count)],
            LairRegionId = lair.Id,
            Phase = BeastPhase.Sleeping,
            Position = grid.GetRegionCenter(lair.Id),
            Health = rules.Health,
            MaxHealth = rules.Health,
        };
        return world.GreatEvents.Beast;
    }

    /// <summary>Whether something near its lair wakes it: a great enough quake, or the volcano erupting.</summary>
    /// <param name="rules">Beast rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="beast">The beast.</param>
    /// <returns><c>true</c> when it wakes.</returns>
    public static bool Disturbed(BeastRules rules, WorldState world, BeastState beast)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(beast);
        var lair = world.Regions.GetRegionCenter(beast.LairRegionId);
        var quake = world.Disasters.Earthquakes.Exists(q => q.Magnitude >= rules.WakeMagnitude && Distance(new GroundPosition(q.X, q.Z), lair) <= rules.WakeRange);
        var eruption = world.Disasters.Volcano is { Phase: Domain.Disasters.VolcanoPhase.Erupting } volcano
            && Distance(world.Regions.GetRegionCenter(volcano.RegionId), lair) <= rules.WakeRange;
        return quake || eruption;
    }

    /// <summary>Wakes the beast: it is whole, the glory starts anew, and its waking is a great event.</summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="beast">The beast.</param>
    /// <param name="events">Receives the great event's beginning.</param>
    public static void Wake(GreatEventRules rules, WorldState world, BeastState beast, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(beast);
        (beast.Phase, beast.WokeTick, beast.Health, beast.TargetSettlementId, beast.RavagedTicks) = (BeastPhase.Awake, world.Tick, rules.Beast.Health, null, 0);
        beast.Path.Clear();
        beast.HarmByGod.Clear();
        GreatEventModel.Begin(rules, world, GreatEventKind.Beast, null, null, 0, events, beast.Name);
    }

    /// <summary>A god's harm to the awake beast, counted towards its share of the glory.</summary>
    /// <param name="beast">The beast.</param>
    /// <param name="godId">The god, or <c>null</c> for harm of no god's (a fire).</param>
    /// <param name="harm">The harm.</param>
    public static void Harm(BeastState beast, int? godId, float harm)
    {
        ArgumentNullException.ThrowIfNull(beast);
        if (beast.Phase is not (BeastPhase.Awake or BeastPhase.Returning) || harm <= 0f)
        {
            return;
        }

        beast.Health = Math.Max(0f, beast.Health - harm);
        if (godId is { } god)
        {
            beast.HarmByGod[god] = beast.HarmByGod.GetValueOrDefault(god) + harm;
        }
    }

    /// <summary>A smiting miracle's harm to the beast, when it stands within the miracle's reach.</summary>
    /// <param name="rules">Beast rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The caster.</param>
    /// <param name="target">Where the miracle strikes.</param>
    /// <param name="reach">How far it reaches, in metres.</param>
    /// <param name="smiting">Its smiting.</param>
    public static void Smite(BeastRules rules, WorldState world, int godId, GroundPosition target, float reach, float smiting)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        if (world.GreatEvents.Beast is { } beast && Distance(beast.Position, target) <= reach + rules.FightRange)
        {
            Harm(beast, godId, smiting * rules.SmiteHarm);
        }
    }

    /// <summary>
    /// The beast falls: slain for good, its waking over, and every god that harmed it shares the
    /// glory: its followers feel the renown by its share of the harm.
    /// </summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="beast">The beast.</param>
    /// <param name="events">Receives <see cref="BeastSlain"/> and the great event's end.</param>
    public static void Fall(GreatEventRules rules, WorldState world, BeastState beast, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(beast);
        beast.Phase = BeastPhase.Slain;
        beast.Path.Clear();
        beast.TargetSettlementId = null;
        var total = beast.HarmByGod.Values.Sum();
        var slayers = beast.HarmByGod.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Select(p => p.Key).ToList();
        foreach (var settlement in world.Settlements)
        {
            foreach (var belief in settlement.Beliefs)
            {
                if (total > 0f && beast.HarmByGod.TryGetValue(belief.GodId, out var harm))
                {
                    BeliefModel.ApplyImpulse(belief, rules.Beast.Renown, harm / total, 1f);
                }
            }
        }

        events.Add(new BeastSlain { Tick = world.Tick, Name = beast.Name, Position = beast.Position, SlayerGodIds = slayers });
        if (GreatEventModel.Active(world, GreatEventKind.Beast) is { } waking)
        {
            GreatEventModel.End(world, waking, events);
        }
    }

    /// <summary>The distance between two points.</summary>
    internal static float Distance(GroundPosition a, GroundPosition b)
    {
        var (dx, dz) = (a.X - b.X, a.Z - b.Z);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>The distance between two regions in regions, the larger of the column and row gaps.</summary>
    private static int Apart(RegionGrid grid, int a, int b)
    {
        var (ca, ra) = grid.GetCoordinates(a);
        var (cb, rb) = grid.GetCoordinates(b);
        return Math.Max(Math.Abs(ca - cb), Math.Abs(ra - rb));
    }
}
