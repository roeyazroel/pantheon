using System;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Gods;

/// <summary>Runs every AI god's strategic evaluation once every <c>EvaluationIntervalTicks</c> ticks (spec §87).</summary>
public sealed class GodAiSystem : ISimulationSystem
{
    private readonly GodAi _ai;
    private readonly int _interval;
    private readonly RefugeeAdvisor? _refugees;
    private readonly Society.PriorityAdvisor? _priorities;
    private readonly TreatyAdvisor? _treaties;
    private readonly CouncilAdvisor? _council;
    private readonly CovertAdvisor? _covert;

    /// <summary>Creates the system.</summary>
    /// <param name="ai">The god AI.</param>
    /// <param name="intervalTicks">Ticks between evaluations.</param>
    /// <param name="refugees">Sets AI gods' refugee policies (ADR-016); <c>null</c> for none.</param>
    /// <param name="priorities">Sets the priorities of the settlements AI gods lead (ADR-023); <c>null</c> for none.</param>
    /// <param name="treaties">Proposes and breaks AI gods' treaties and calls in their favours (ADR-025); <c>null</c> for none.</param>
    /// <param name="council">AI gods' votes and motions in the Divine Council (ADR-025); <c>null</c> for none.</param>
    /// <param name="covert">AI gods' proxy acts and holy war stances (ADR-026); <c>null</c> for none.</param>
    public GodAiSystem(GodAi ai, int intervalTicks, RefugeeAdvisor? refugees = null, Society.PriorityAdvisor? priorities = null, TreatyAdvisor? treaties = null, CouncilAdvisor? council = null, CovertAdvisor? covert = null)
    {
        _covert = covert;
        _treaties = treaties;
        _council = council;
        ArgumentNullException.ThrowIfNull(ai);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _ai = ai;
        _interval = intervalTicks;
        _refugees = refugees;
        _priorities = priorities;
    }

    /// <summary>The priority advisor, for previews in the debug view; <c>null</c> when there is none.</summary>
    public Society.PriorityAdvisor? Priorities => _priorities;

    /// <summary>The treaty advisor, for previews in the debug view; <c>null</c> when there is none.</summary>
    public TreatyAdvisor? Treaties => _treaties;

    /// <summary>The refugee advisor, for previews in the debug view; <c>null</c> when there is none.</summary>
    public RefugeeAdvisor? Refugees => _refugees;

    /// <summary>The god AI, for previews in the debug view.</summary>
    public GodAi Ai => _ai;

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var turn = 0;
        var thinkers = context.World.Gods.Count(g => g.Controller == GodController.Ai);
        foreach (var god in context.World.Gods)
        {
            if (god.Controller == GodController.Ai && IsTurn(context.World.Tick, turn++, thinkers, _interval))
            {
                _ai.Act(context.World, god, context.Random, context.Events);
                if (_ai.ProfileOf(god) is { } profile)
                {
                    _refugees?.Act(context.World, god, profile, context.Events);
                    _priorities?.Act(context.World, god, profile, context.Events);
                    _treaties?.Act(context.World, god, profile, context.Events);
                    _council?.Act(context.World, god, profile);
                    _covert?.Act(context.World, god, profile, context.Events);
                }
            }
        }
    }

    /// <summary>
    /// Whether an AI god thinks this tick. The AI gods take turns across the interval, each once,
    /// rather than all thinking on one tick, which in a world of eight gods made a stutter (ADR-027);
    /// the first thinks when the interval is due, as a lone AI god always did.
    /// </summary>
    /// <param name="tick">The tick being processed.</param>
    /// <param name="turn">The god's place among the AI gods, from 0.</param>
    /// <param name="thinkers">How many AI gods there are.</param>
    /// <param name="interval">Ticks between one god's thoughts.</param>
    /// <returns><c>true</c> on its turn.</returns>
    public static bool IsTurn(long tick, int turn, int thinkers, int interval) =>
        IntervalSystem.IsDue(tick + ((long)turn * interval / Math.Max(1, thinkers)), interval);
}
