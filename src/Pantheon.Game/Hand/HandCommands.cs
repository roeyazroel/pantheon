using System;
using Pantheon.Domain.Hand;
using Pantheon.Domain.World;

namespace Pantheon.Game.Hand;

/// <summary>The player's Divine Hand (spec §55, ADR-030): R picks up or sets down at the cursor, Shift+R throws. Each returns a status line.</summary>
public static class HandCommands
{
    /// <summary>What R does now: pick up with an empty hand, set down (or throw) what it holds.</summary>
    /// <param name="session">The session.</param>
    /// <param name="shift">Whether Shift is held, to throw.</param>
    /// <returns>The action.</returns>
    public static HandAction ActionFor(GameSession session, bool shift)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.LocalGod?.Held is null ? HandAction.PickUp : shift ? HandAction.Throw : HandAction.SetDown;
    }

    /// <summary>Uses the local player's hand at a point; a joined player asks the host.</summary>
    /// <param name="session">The session.</param>
    /// <param name="action">What to do.</param>
    /// <param name="target">Where.</param>
    /// <returns>Status line.</returns>
    public static string Use(GameSession session, HandAction action, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Client is { } client
            ? client.Request(id => new Networking.Protocol.HandIntent(id, action, target))
            : UseAs(session, session.LocalGodId ?? 0, action, target).Text;
    }

    /// <summary>A god's use of its hand, as its player's intent.</summary>
    /// <param name="session">The session.</param>
    /// <param name="godId">The god.</param>
    /// <param name="action">What to do.</param>
    /// <param name="target">Where.</param>
    /// <returns>Whether it was done, and the status line.</returns>
    public static CommandOutcome UseAs(GameSession session, int godId, HandAction action, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        var before = world.Gods.Find(g => g.Id == godId)?.Held;
        var rejection = session.Command((authorities, events) => authorities.Hand?.Execute(world, new HandCommand(godId, action, target), events) ?? HandRejection.NoHand);
        return rejection == HandRejection.None
            ? new CommandOutcome(true, HandText.Done(world, godId, action, before ?? world.Gods.Find(g => g.Id == godId)?.Held))
            : new CommandOutcome(false, HandText.Describe(rejection));
    }
}
