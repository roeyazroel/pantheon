using System;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Belief;

/// <summary>
/// Tracks which god dominates the world's belief (spec §53 Divine Ascendancy, §115): a god that
/// holds <c>VictoryShare</c> of all followers for <c>VictorySeconds</c> without a break wins. The
/// world goes on afterwards. A world with fewer than two gods has no contest and no winner.
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
            if (ShareOf(world, god.Id) < _rules.VictoryShare)
            {
                if (god.DominantSinceTick is not null)
                {
                    god.DominantSinceTick = null;
                    context.Events.Add(new DominanceChanged { Tick = world.Tick, GodId = god.Id, Started = false });
                }

                continue;
            }

            if (god.DominantSinceTick is null)
            {
                god.DominantSinceTick = world.Tick;
                context.Events.Add(new DominanceChanged { Tick = world.Tick, GodId = god.Id, Started = true });
            }

            if (world.WinnerGodId is null && world.Tick - god.DominantSinceTick.Value >= TicksToWin(_rules))
            {
                world.WinnerGodId = god.Id;
                context.Events.Add(new VictoryAchieved { Tick = world.Tick, GodId = god.Id });
            }
        }
    }
}
