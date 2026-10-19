using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Society;

/// <summary>
/// Checks and carries out a god's request that a settlement put something first (spec §29, §73,
/// ADR-023): the god must be followed by at least <see cref="SocietyRules.PriorityShare"/> of its
/// people, and a priority holds at least <see cref="SocietyRules.PriorityCooldownSeconds"/>.
/// </summary>
public sealed class SocietyAuthority
{
    private readonly SocietyRules _rules;

    /// <summary>Creates the authority over validated rules.</summary>
    /// <param name="rules">Society rules.</param>
    public SocietyAuthority(SocietyRules rules)
    {
        SocietyRulesValidator.Validate(rules);
        _rules = rules;
    }

    /// <summary>Whether a god may set a settlement's priority now.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The request.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <returns>Why not, or <see cref="PriorityRejection.None"/>.</returns>
    public PriorityRejection Check(WorldState world, PriorityCommand command, int? issuerGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        if (GodStanding.Find(world, command.GodId) is null)
        {
            return PriorityRejection.UnknownGod;
        }

        if (world.Settlements.Find(s => s.Id == command.SettlementId) is not { Population: > 0 } settlement || !Enum.IsDefined(command.Priority))
        {
            return PriorityRejection.UnknownSettlement;
        }

        if (issuerGodId is { } issuer && issuer != command.GodId)
        {
            return PriorityRejection.NotAuthorized;
        }

        if ((BeliefModel.Find(settlement, command.GodId)?.Adherence ?? 0f) < _rules.PriorityShare)
        {
            return PriorityRejection.TooFewFollowers;
        }

        var society = settlement.Society;
        if (society.Priority == command.Priority && society.PriorityGodId == command.GodId)
        {
            return PriorityRejection.Unchanged;
        }

        var cooldown = (long)MathF.Round(_rules.PriorityCooldownSeconds * SimulationTime.TicksPerSecond);
        return society.PrioritySinceTick > 0 && world.Tick - society.PrioritySinceTick < cooldown ? PriorityRejection.TooSoon : PriorityRejection.None;
    }

    /// <summary>Checks a request and, when it is allowed, sets the priority.</summary>
    /// <param name="world">World state to change.</param>
    /// <param name="command">The request.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <param name="events">Receives <see cref="PriorityChanged"/>.</param>
    /// <returns>The verdict; the world changed only when it is <see cref="PriorityRejection.None"/>.</returns>
    public PriorityRejection Apply(WorldState world, PriorityCommand command, int? issuerGodId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var verdict = Check(world, command, issuerGodId);
        if (verdict != PriorityRejection.None)
        {
            return verdict;
        }

        var society = world.Settlements.Find(s => s.Id == command.SettlementId)!.Society;
        society.Priority = command.Priority;
        society.PriorityGodId = command.Priority == SettlementPriority.None ? null : command.GodId;
        society.PrioritySinceTick = Math.Max(1, world.Tick);
        events.Add(new PriorityChanged { Tick = world.Tick, SettlementId = command.SettlementId, GodId = command.GodId, Priority = command.Priority });
        return verdict;
    }
}
