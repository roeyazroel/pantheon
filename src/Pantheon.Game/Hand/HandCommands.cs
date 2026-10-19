using System;
using Pantheon.Domain.Hand;
using Pantheon.Domain.World;

namespace Pantheon.Game.Hand;

/// <summary>The player's Divine Hand (spec §55, ADR-030): R picks up or sets down at the cursor, Shift+R throws, Ctrl+R takes an offering (ADR-039). Each returns a status line.</summary>
public static class HandCommands
{
    /// <summary>What R does now: pick up with an empty hand (take an offering with Ctrl), set down (or throw) what it holds.</summary>
    /// <param name="session">The session.</param>
    /// <param name="shift">Whether Shift is held, to throw.</param>
    /// <param name="ctrl">Whether Ctrl is held, to take an offering.</param>
    /// <returns>The action.</returns>
    public static HandAction ActionFor(GameSession session, bool shift, bool ctrl = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.LocalGod?.Held is null ? (ctrl ? HandAction.TakeOffering : HandAction.PickUp) : shift ? HandAction.Throw : HandAction.SetDown;
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

    /// <summary>
    /// Lifts a rock or a tree from just beyond the hand's reach of a village and throws it at the
    /// village, for captures (<c>--hand=throw</c>, ADR-048). Anything else lifted on the way is set down
    /// again where it was.
    /// </summary>
    /// <param name="session">The session; a joined player cannot.</param>
    /// <param name="target">The village's place.</param>
    /// <returns>Status line.</returns>
    public static string ThrowAt(GameSession session, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Client is not null || session.LocalGodId is not { } god)
        {
            return "Only a world of your own can throw at the start";
        }

        var away = session.Content.DivineRules.Hand.Reach + 20f;
        foreach (var (dx, dz) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
        {
            var from = new GroundPosition(target.X + (dx * away), target.Z + (dz * away));
            UseAs(session, god, HandAction.PickUp, from);
            switch (session.LocalGod?.Held?.Kind)
            {
                case HeldKind.Rock or HeldKind.Tree:
                    return UseAs(session, god, HandAction.Throw, target).Text;
                case not null:
                    UseAs(session, god, HandAction.SetDown, from);
                    break;
            }
        }

        return "There is no rock or tree to throw near here";
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
        Domain.Events.OfferingGiven? given = null;
        var rejection = session.Command((authorities, events) =>
        {
            var verdict = authorities.Hand?.Execute(world, new HandCommand(godId, action, target), events) ?? HandRejection.NoHand;
            given = System.Linq.Enumerable.LastOrDefault(System.Linq.Enumerable.OfType<Domain.Events.OfferingGiven>(events));
            return verdict;
        });
        return rejection != HandRejection.None ? new CommandOutcome(false, HandText.Describe(rejection))
            : given is not null ? new CommandOutcome(true, HandText.Given(world, given))
            : new CommandOutcome(true, HandText.Done(world, godId, action, before ?? world.Gods.Find(g => g.Id == godId)?.Held));
    }
}
