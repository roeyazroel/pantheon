using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Belief;

/// <summary>
/// Tracks which god dominates the world (spec §53, §115, ADR-015, ADR-027) and declares the winner
/// by the victories the world was made with: Divine Ascendancy (a god holds <c>VictoryShare</c> of
/// all followers, alone or with its allies when pantheon victory is on), conquest (every opponent
/// below <c>ConquestShare</c>) or a cultural victory (it leads <c>CulturalShare</c> of the living
/// villages while a rival lives on), each for <c>VictorySeconds</c> without a break. Dominance is
/// tracked whatever the victories, for the council and the AI. The world goes on afterwards. A
/// world with fewer than two gods has no contest and no winner.
/// </summary>
public sealed class VictorySystem : ISimulationSystem
{
    private readonly DivineRules _rules;

    /// <summary>Creates the system.</summary>
    /// <param name="rules">Divine rules.</param>
    public VictorySystem(DivineRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    /// <summary>A god's share of all the world's followers.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The share (0..1); 0 when nobody follows any god.</returns>
    public static float ShareOf(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var total = 0;
        var mine = 0;
        foreach (var god in world.Gods)
        {
            var followers = GodStanding.CountFollowers(world, god.Id);
            total += followers;
            if (god.Id == godId)
            {
                mine = followers;
            }
        }

        return total > 0 ? (float)mine / total : 0f;
    }

    /// <summary>Ticks of unbroken dominance that win.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <returns>The ticks.</returns>
    public static long TicksToWin(DivineRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return (long)MathF.Round(rules.VictorySeconds * SimulationTime.TicksPerSecond);
    }

    /// <summary>
    /// The share of all followers a god holds towards victory, and with whom: its own, or, with
    /// pantheon victory on, together with its strongest direct allies, at most
    /// <c>PantheonMaxGods</c> gods in all, the world's own number if its size sets one (ADR-015,
    /// ADR-020, ADR-027; ties go to the lower id).
    /// </summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The share and the allies it counts with (empty when it counts alone).</returns>
    public static (float Share, IReadOnlyList<int> Allies) StandingOf(DivineRules rules, WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var own = ShareOf(world, godId);
        if (!rules.PantheonVictory || own >= rules.VictoryShare)
        {
            return (own, []);
        }

        var allies = DiplomacyModel.AlliesOf(world, godId)
            .Select(ally => (Id: ally, Share: ShareOf(world, ally)))
            .OrderByDescending(ally => ally.Share)
            .ThenBy(ally => ally.Id)
            .Take(Math.Max(0, (world.Victory.PantheonMaxGods ?? rules.PantheonMaxGods) - 1))
            .ToList();
        var together = own + allies.Sum(ally => ally.Share);
        return allies.Count > 0 && together > own ? (together, allies.Select(ally => ally.Id).Order().ToList()) : (own, []);
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        if (world.Gods.Count < 2)
        {
            return;
        }

        foreach (var god in world.Gods)
        {
            var (share, allies) = StandingOf(_rules, world, god.Id);
            if (share < _rules.VictoryShare)
            {
                if (god.DominantSinceTick is not null)
                {
                    god.DominantSinceTick = null;
                    context.Events.Add(new DominanceChanged { Tick = world.Tick, GodId = god.Id, Started = false, Allies = allies });
                }

                continue;
            }

            if (god.DominantSinceTick is null)
            {
                god.DominantSinceTick = world.Tick;
                context.Events.Add(new DominanceChanged { Tick = world.Tick, GodId = god.Id, Started = true, Allies = allies });
            }

            if (world.Victory.Ascendancy && world.Tick - god.DominantSinceTick.Value >= TicksToWin(_rules))
            {
                Win(world, god.Id, allies, allies.Count > 0 ? VictoryKind.Pantheon : VictoryKind.Ascendancy, context.Events);
            }
        }

        foreach (var god in world.Gods)
        {
            god.ConquestSinceTick = Track(world, god.ConquestSinceTick, world.Victory.Conquest && Conquers(world, god.Id));
            god.CulturalSinceTick = Track(world, god.CulturalSinceTick, world.Victory.Cultural && LeadsCulture(world, god.Id));
            if (god.ConquestSinceTick is { } conquest && world.Tick - conquest >= TicksToWin(_rules))
            {
                Win(world, god.Id, DiplomacyModel.AlliesOf(world, god.Id), VictoryKind.Conquest, context.Events);
            }

            if (god.CulturalSinceTick is { } culture && world.Tick - culture >= TicksToWin(_rules))
            {
                Win(world, god.Id, [], VictoryKind.Cultural, context.Events);
            }
        }
    }

    /// <summary>Whether every god outside a god's alliances holds less than the conquest share while it has followers (spec §53: conquest).</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns><c>true</c> when its opponents are all but gone.</returns>
    public bool Conquers(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (ShareOf(world, godId) <= 0f)
        {
            return false;
        }

        foreach (var other in world.Gods)
        {
            if (other.Id != godId && !DiplomacyModel.AreAllied(world, godId, other.Id) && ShareOf(world, other.Id) >= _rules.ConquestShare)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a god leads the cultural share of the living villages while some rival keeps at
    /// least the conquest share (spec §53: dominant without eliminating rivals).
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns><c>true</c> when it leads the world's culture.</returns>
    public bool LeadsCulture(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var (living, led) = CultureCount(world, godId);
        return living > 0 && led >= _rules.CulturalShare * living && world.Gods.Exists(g => g.Id != godId && ShareOf(world, g.Id) >= _rules.ConquestShare);
    }

    /// <summary>The living villages, and how many a god leads (the largest share follows it).</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The counts.</returns>
    public static (int Living, int Led) CultureCount(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var living = 0;
        var led = 0;
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population <= 0)
            {
                continue;
            }

            living++;
            Domain.Belief.GodBelief? main = null;
            foreach (var belief in settlement.Beliefs)
            {
                if (belief.Adherence > 0.05f && (main is null || belief.Adherence > main.Adherence))
                {
                    main = belief;
                }
            }

            led += main?.GodId == godId ? 1 : 0;
        }

        return (living, led);
    }

    /// <summary>Starts, keeps or clears a victory's clock.</summary>
    private static long? Track(WorldState world, long? since, bool holds) => holds ? since ?? world.Tick : null;

    /// <summary>Declares the winners, once.</summary>
    private static void Win(WorldState world, int godId, IReadOnlyList<int> allies, VictoryKind kind, List<WorldEvent> events)
    {
        if (world.WinnerGodIds.Count > 0)
        {
            return;
        }

        world.WinnerGodIds.Add(godId);
        world.WinnerGodIds.AddRange(allies);
        world.WinnerGodIds.Sort();
        events.Add(new VictoryAchieved { Tick = world.Tick, GodIds = [.. world.WinnerGodIds], Kind = kind });
    }
}
