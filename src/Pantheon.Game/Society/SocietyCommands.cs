using System;
using Pantheon.Domain.Society;
using Pantheon.Simulation.Society;

namespace Pantheon.Game.Society;

/// <summary>The player asks a settlement to put something first (spec §29, ADR-023). Each command returns a status line.</summary>
public static class SocietyCommands
{
    /// <summary>Sets a settlement's priority for the local player's god; a joined player asks the host.</summary>
    /// <param name="session">The session.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>Status line.</returns>
    public static string SetPriority(GameSession session, int settlementId, SettlementPriority priority)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Client is { } client
            ? client.Request(id => new Networking.Protocol.PriorityIntent(id, settlementId, priority))
            : SetPriorityAs(session, session.LocalGodId ?? 0, settlementId, priority).Text;
    }

    /// <summary>Sets a settlement's priority for a god, as its player's intent.</summary>
    /// <param name="session">The session.</param>
    /// <param name="godId">The issuing god.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>Whether it was set, and the status line.</returns>
    public static CommandOutcome SetPriorityAs(GameSession session, int godId, int settlementId, SettlementPriority priority)
    {
        ArgumentNullException.ThrowIfNull(session);
        var place = session.World.Settlements.Find(s => s.Id == settlementId)?.Name ?? "That village";
        var rejection = session.Command((authorities, events) =>
            authorities.Society?.Apply(session.World, new PriorityCommand(godId, settlementId, priority), godId, events) ?? PriorityRejection.UnknownSettlement);
        return new CommandOutcome(rejection == PriorityRejection.None, SocietyText.Describe(rejection, priority, place));
    }
}
