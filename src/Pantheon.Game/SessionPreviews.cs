using System;
using System.Collections.Generic;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Game;

/// <summary>What the AI and the simulation are weighing now, for the debug view (spec §106). Reads the session's engine; changes nothing.</summary>
public static class SessionPreviews
{
    /// <summary>The local Creature's options now, best first.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The options; none without a Creature.</returns>
    public static IReadOnlyList<CreatureOption> PreviewCreatureOptions(this GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.LocalCreature is { } creature ? session.Engine.Creatures!.Brain.ListOptions(session.World, creature) : [];
    }

    /// <summary>The diplomatic moves an AI god is weighing now, best first.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The god.</param>
    /// <returns>Its candidates; none for a player's god.</returns>
    public static IReadOnlyList<Simulation.Gods.DiplomacyCandidate> PreviewGodDiplomacy(this GameSession session, GodState god)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(god);
        return god.Controller == GodController.Ai ? session.Engine.Gods!.Ai.EvaluateDiplomacy(session.World, god) : [];
    }

    /// <summary>What an AI god is weighing now, best first.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The god.</param>
    /// <returns>Its candidate actions; none for a god without an AI personality.</returns>
    public static IReadOnlyList<Simulation.Gods.AiCandidate> PreviewGodAi(this GameSession session, GodState god)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(god);
        return god.Controller == GodController.Ai ? session.Engine.Gods!.Ai.Evaluate(session.World, god) : [];
    }

    /// <summary>The goals an AI god could set itself in its next plan, weightiest first (ADR-036).</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The god.</param>
    /// <returns>Its options; none for a player's god.</returns>
    public static IReadOnlyList<Simulation.Gods.GoalOption> PreviewGodGoals(this GameSession session, GodState god)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(god);
        var ai = session.Engine.Gods!.Ai;
        return god.Controller == GodController.Ai && ai.ProfileOf(god.Personality) is { } profile ? ai.Planner.Options(session.World, god, profile) : [];
    }

    /// <summary>The pressure on a settlement's people to leave now (ADR-016).</summary>
    /// <param name="session">The session.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The pressure and its main cause.</returns>
    public static Simulation.Migration.MigrationPressure MigrationPressureOn(this GameSession session, Domain.Settlements.SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Engine.Migration!.PressureOn(session.World, settlement);
    }

    /// <summary>The refugee policy an AI god wants now and why.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The god.</param>
    /// <returns>The policy and reason; <c>null</c> for a player's god.</returns>
    public static (Domain.Migration.RefugeePolicy Policy, string Reason)? PreviewRefugeePolicy(this GameSession session, GodState god)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(god);
        var gods = session.Engine.Gods!;
        return god.Controller == GodController.Ai && gods.Ai.ProfileOf(god.Personality) is { } profile && gods.Refugees is { } advisor
            ? advisor.Choose(session.World, god, profile)
            : null;
    }
}
