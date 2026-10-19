using System;
using System.Linq;
using Pantheon.Domain.Notables;
using Pantheon.Domain.World;
using Pantheon.Simulation.Notables;

namespace Pantheon.Game.Notables;

/// <summary>Debug drivers for prophets and elders (ADR-032): <c>--prophet</c> raises one of the player's god. Each returns a status line; only the host changes its world.</summary>
public static class NotableCommands
{
    /// <summary>Raises a prophet of the local player's god in the village nearest a point.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="near">The point.</param>
    /// <returns>Status line.</returns>
    public static string RaiseProphet(GameSession session, GroundPosition near)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        var rules = session.Content.DivineRules.Notables;
        if (session.LocalGodId is not { } godId || rules.Names.Count == 0)
        {
            return "No god to raise a prophet for";
        }

        var village = world.Settlements.Where(s => s.Population > 0)
            .OrderBy(s => ((s.Position.X - near.X) * (s.Position.X - near.X)) + ((s.Position.Z - near.Z) * (s.Position.Z - near.Z)))
            .FirstOrDefault();
        if (village is null)
        {
            return "No village to raise a prophet in";
        }

        var prophet = session.Command((_, events) => NotableModel.Raise(rules, world, NotableKind.Prophet, godId, village, anointed: false, events));
        return NotableText.Title(prophet, world) + " rises in " + village.Name;
    }
}
