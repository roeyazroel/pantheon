using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Notables;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Navigation;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Notables;

/// <summary>
/// Prophets and elders as pure functions and state changes (spec §80, ADR-032): raising one, where a
/// prophet goes next, what a sermon wins, and what a notable remembers. A notable's name and lifespan
/// come from the world's seed and their id, so raising one draws nothing from any stream.
/// </summary>
public static class NotableModel
{
    /// <summary>The god a village is devout to: most of it follows the god in faith; <c>null</c> otherwise.</summary>
    /// <param name="rules">Notable rules.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>The god, or <c>null</c>.</returns>
    public static int? DevoutTo(NotableRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        return MigrationModel.MainGod(settlement.Beliefs) is { } god && BeliefModel.Find(settlement, god) is { } belief
            && belief.Adherence >= rules.Devotion && belief.Faith >= rules.Devotion ? god : null;
    }

    /// <summary>How many prophets a god has now.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The count.</returns>
    public static int ProphetsOf(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Notables.FindAll(n => n.Kind == NotableKind.Prophet && n.GodId == godId).Count;
    }

    /// <summary>Raises a prophet or an elder in a village.</summary>
    /// <param name="rules">Notable rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="kind">A prophet or an elder.</param>
    /// <param name="godId">Their god.</param>
    /// <param name="settlement">Their village.</param>
    /// <param name="anointed">Whether their god anointed them.</param>
    /// <param name="events">Receives <see cref="NotableRose"/>.</param>
    /// <returns>The notable.</returns>
    public static NotableState Raise(NotableRules rules, WorldState world, NotableKind kind, int godId, SettlementState settlement, bool anointed, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(events);
        var id = world.NextNotableId++;
        var roll = SeedMixer.Derive(world.Seed, "notable", id);
        var (shortest, longest) = kind == NotableKind.Prophet ? (rules.ProphetMinSeconds, rules.ProphetMaxSeconds) : (rules.ElderMinSeconds, rules.ElderMaxSeconds);
        var life = shortest + ((longest - shortest) * ((roll >> 11) % 1000) / 1000f);
        var notable = new NotableState
        {
            Id = id,
            Kind = kind,
            Name = rules.Names.Count == 0 ? "Nameless" : rules.Names[(int)(roll % (ulong)rules.Names.Count)],
            GodId = godId,
            SettlementId = settlement.Id,
            Position = settlement.Position,
            BornTick = world.Tick,
            DiesAtTick = world.Tick + (long)MathF.Round(life * SimulationTime.TicksPerSecond),
            TicksLeft = kind == NotableKind.Prophet ? Ticks(rules.PreachSeconds) : 0,
        };
        Remember(rules, notable, world.Tick, NotableDeed.Rose, settlement.Id, 0);
        world.Notables.Add(notable);
        events.Add(new NotableRose { Tick = world.Tick, NotableId = id, Kind = kind, GodId = godId, SettlementId = settlement.Id, Anointed = anointed });
        return notable;
    }

    /// <summary>
    /// The village a prophet goes to next: within reach, not the one they are in, where most people are
    /// not yet their god's, the larger and nearer the better; <c>null</c> when none.
    /// </summary>
    /// <param name="rules">Notable rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="prophet">The prophet.</param>
    /// <returns>The village, or <c>null</c>.</returns>
    public static SettlementState? NextVillage(NotableRules rules, WorldState world, NotableState prophet)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(prophet);
        SettlementState? best = null;
        var bestScore = 0f;
        foreach (var settlement in world.Settlements)
        {
            var (dx, dz) = (settlement.Position.X - prophet.Position.X, settlement.Position.Z - prophet.Position.Z);
            var distance = MathF.Sqrt((dx * dx) + (dz * dz));
            if (settlement.Id == prophet.SettlementId || settlement.Population <= 0 || distance > rules.ProphetRange)
            {
                continue;
            }

            var unwon = settlement.Population * (1f - (BeliefModel.Find(settlement, prophet.GodId)?.Adherence ?? 0f));
            var score = unwon / (1f + (distance / 500f));
            if (score > bestScore)
            {
                (best, bestScore) = (settlement, score);
            }
        }

        return best;
    }

    /// <summary>Sets a prophet walking to a village, by the open road where there is one (ADR-033), else around the sea where the way can be found.</summary>
    /// <param name="world">World state.</param>
    /// <param name="prophet">The prophet.</param>
    /// <param name="village">The village.</param>
    /// <param name="roads">Road rules; <c>null</c> walks off the roads.</param>
    public static void SetOut(WorldState world, NotableState prophet, SettlementState village, Domain.Roads.RoadRules? roads = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(prophet);
        ArgumentNullException.ThrowIfNull(village);
        var road = roads is null ? null : Roads.RoadModel.Way(roads, world, prophet.SettlementId, village.Id);
        prophet.TargetSettlementId = village.Id;
        prophet.Path.Clear();
        prophet.Path.AddRange(road is { Count: > 1 } ? road.GetRange(1, road.Count - 1)
            : RegionPathfinder.FindPath(world.Regions, prophet.Position, village.Position) is { } way ? way : [village.Position]);
    }

    /// <summary>A prophet's sermon: the village's godless come to their god, and some of its rivals' followers too.</summary>
    /// <param name="rules">Notable rules.</param>
    /// <param name="settlement">The village.</param>
    /// <param name="godId">The prophet's god.</param>
    /// <returns>The people won.</returns>
    public static int Preach(NotableRules rules, SettlementState settlement, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var godless = Math.Max(0f, 1f - BeliefModel.TotalAdherence(settlement.Beliefs));
        var won = rules.PreachConversion * godless;
        foreach (var rival in settlement.Beliefs)
        {
            if (rival.GodId != godId)
            {
                var taken = rules.PreachConversion * 0.5f * rival.Adherence;
                rival.Adherence -= taken;
                won += taken;
            }
        }

        if (BeliefModel.Find(settlement, godId) is not { } belief)
        {
            belief = new Domain.Belief.GodBelief { GodId = godId, Adherence = 0f, Faith = 0f, Love = 0f, Fear = 0f, Doubt = 0f };
            settlement.Beliefs.Add(belief);
            settlement.Beliefs.Sort((a, b) => a.GodId.CompareTo(b.GodId));
        }

        belief.Adherence = SimMath.Clamp01(belief.Adherence + won);
        belief.Faith = BeliefModel.ApplyChange(belief.Faith, rules.PreachFaith);
        return (int)MathF.Round(won * Math.Max(0, settlement.Population));
    }

    /// <summary>A notable remembers something, forgetting the oldest beyond their capacity.</summary>
    /// <param name="rules">Notable rules.</param>
    /// <param name="notable">The notable.</param>
    /// <param name="tick">When.</param>
    /// <param name="deed">What.</param>
    /// <param name="settlementId">Where.</param>
    /// <param name="count">How many people.</param>
    public static void Remember(NotableRules rules, NotableState notable, long tick, NotableDeed deed, int? settlementId, int count)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(notable);
        notable.Memories.Add(new NotableMemory { Tick = tick, Deed = deed, SettlementId = settlementId, Count = count });
        while (notable.Memories.Count > Math.Max(1, rules.MemoryCapacity))
        {
            notable.Memories.RemoveAt(0);
        }
    }

    /// <summary>Seconds as whole ticks, at least one.</summary>
    internal static int Ticks(float seconds) => Math.Max(1, (int)MathF.Round(seconds * SimulationTime.TicksPerSecond));
}
