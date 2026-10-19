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
/// Tracks which god dominates the world's belief (spec §53 Divine Ascendancy and Pantheon victory,
/// §115): a god that holds <c>VictoryShare</c> of all followers for <c>VictorySeconds</c> without a
/// break wins, alone or, with pantheon victory on, together with the allies whose followers it
/// counts with (ADR-015). The world goes on afterwards. A world with fewer than two gods has no
/// contest and no winner.
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
    /// <c>PantheonMaxGods</c> gods in all (ADR-015, ADR-020; ties go to the lower id).
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
            .Take(Math.Max(0, rules.PantheonMaxGods - 1))
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

            if (world.WinnerGodIds.Count == 0 && world.Tick - god.DominantSinceTick.Value >= TicksToWin(_rules))
            {
                world.WinnerGodIds.Add(god.Id);
                world.WinnerGodIds.AddRange(allies);
                world.WinnerGodIds.Sort();
                context.Events.Add(new VictoryAchieved { Tick = world.Tick, GodIds = [.. world.WinnerGodIds] });
            }
        }
    }
}
