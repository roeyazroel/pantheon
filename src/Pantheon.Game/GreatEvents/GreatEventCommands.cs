using System;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Simulation.GreatEvents;

namespace Pantheon.Game.GreatEvents;

/// <summary>A great event the debug tools can start (ADR-034).</summary>
public enum GreatEventAtStart
{
    /// <summary>The plague breaks out in the village nearest the point.</summary>
    Plague,

    /// <summary>The sun goes dark.</summary>
    Eclipse,

    /// <summary>The player's god's faith reforms.</summary>
    Reformation,
}

/// <summary>Debug drivers for the great events (ADR-034): the ; key and <c>--great-event</c>. Each returns a status line; only the host changes its world.</summary>
public static class GreatEventCommands
{
    /// <summary>The great event after another in the ; cycle.</summary>
    /// <param name="kind">The one started last, or <c>null</c> for the first.</param>
    /// <returns>The next, back to the first after the last.</returns>
    public static GreatEventAtStart Next(GreatEventAtStart? kind) => kind is not { } last ? GreatEventAtStart.Plague : last switch
    {
        GreatEventAtStart.Plague => GreatEventAtStart.Eclipse,
        GreatEventAtStart.Eclipse => GreatEventAtStart.Reformation,
        GreatEventAtStart.Reformation => GreatEventAtStart.Plague,
    };

    /// <summary>Starts a great event.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="kind">The event.</param>
    /// <param name="target">For the plague, the point nearest the village.</param>
    /// <returns>Status line.</returns>
    public static string Start(GameSession session, GreatEventAtStart kind, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        var rules = session.Content.Disasters.GreatEvents;
        return kind switch
        {
            GreatEventAtStart.Plague => Plague(session, world, target),
            GreatEventAtStart.Eclipse => GreatEventModel.Active(world, GreatEventKind.Eclipse) is not null
                ? "The sun is already dark"
                : GreatEventText.Capitalised(session.Command((_, events) => EclipseModel.Darken(rules, world, events)).Name) + ": the sun goes dark",
            GreatEventAtStart.Reformation => session.LocalGodId is { } god
                ? GreatEventText.Capitalised(session.Command((_, events) => GreatEventSystem.Reform(rules, world, god, events)).Name) + ": your faith reforms"
                : "No god to reform",
        };
    }

    /// <summary>The plague breaks out in the village nearest a point.</summary>
    private static string Plague(GameSession session, WorldState world, GroundPosition target)
    {
        var village = world.Settlements.Where(s => s.Population > 0)
            .OrderBy(s => ((s.Position.X - target.X) * (s.Position.X - target.X)) + ((s.Position.Z - target.Z) * (s.Position.Z - target.Z))).FirstOrDefault();
        if (village is null)
        {
            return "No village for the plague";
        }

        session.Command((_, events) =>
        {
            village.Plague = Math.Max(village.Plague, 0.3f);
            events.Add(new PlagueStruck { Tick = world.Tick, SettlementId = village.Id });
            return 0;
        });
        return "The plague breaks out in " + village.Name;
    }
}
