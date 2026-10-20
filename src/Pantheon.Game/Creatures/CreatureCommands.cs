using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Game.Creatures;

/// <summary>Creatures dealing with each other in the session (ADR-017): seeking another god's (ADR-068), the debug meeting and the previews of the debug view. Engine-agnostic.</summary>
public static class CreatureCommands
{
    /// <summary>Brings another Creature beside the player's, so they meet at once (K, <c>--meet</c>). Host only.</summary>
    /// <param name="session">Game session.</param>
    /// <param name="name">The Creature to bring, by name; the nearest when <c>null</c> or empty.</param>
    /// <returns>A status line.</returns>
    public static string BringOther(GameSession session, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        if (session.LocalCreature is not { } mine)
        {
            return "You have no Creature";
        }

        return Bring(session, name) is { } other
            ? $"{other.Name} comes to {mine.Name}"
            : string.IsNullOrEmpty(name) ? $"No other Creature can come to {mine.Name} now" : $"{name} cannot come to {mine.Name} now";
    }

    /// <summary>
    /// Sends the player's Creature to another god's (ADR-068): the order Send gives, aimed where their
    /// Creature is. The two meet when they come within reach of each other, and so do their gods, as
    /// any two Creatures do. How a god seeks out a god it has not met.
    /// </summary>
    /// <param name="session">Game session.</param>
    /// <param name="godId">The god whose Creature to meet.</param>
    /// <returns>A status line.</returns>
    public static string Seek(GameSession session, int godId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        var god = world.Gods.Find(g => g.Id == godId)?.Name ?? "That god";
        if (session.LocalCreature is not { Activity.Phase: not CreaturePhase.Banished } mine)
        {
            return session.LocalCreature is null ? "You have no Creature to send" : $"{session.LocalCreature.Name} is banished and cannot go";
        }

        if (world.Creatures.Find(c => c.GodId == godId && c.Id != mine.Id && c.Activity.Phase != CreaturePhase.Banished) is not { } theirs)
        {
            return $"{god} has no Creature to meet";
        }

        if (session.Client is not null)
        {
            return SessionCommands.CommandCreature(session, CreatureCommandKind.GoTo, theirs.Position);
        }

        var sent = SessionCommands.CommandCreatureAs(session, session.LocalGodId ?? 0, CreatureCommandKind.GoTo, theirs.Position);
        return sent.Accepted ? $"{mine.Name} sets out to meet {theirs.Name}, the Creature of {god}" : sent.Text;
    }

    /// <summary>
    /// Slows the world to ×1 when the player's Creature starts a fight while it runs faster (ADR-070), so
    /// the player sees the fight; the host's or a lone player's choice only.
    /// </summary>
    /// <param name="session">Game session.</param>
    /// <param name="events">A frame's events.</param>
    /// <returns>A status line when it slowed the world; <c>null</c> otherwise.</returns>
    public static string? SlowForFight(GameSession session, IReadOnlyList<Domain.Events.WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(events);
        if (session.IsReplica || session.Scheduler.TimeScale <= 1.0 || session.LocalCreature is not { } mine)
        {
            return null;
        }

        foreach (var worldEvent in events)
        {
            if (worldEvent is Domain.Events.CreaturesMet { Outcome: Domain.Events.MeetingOutcome.Fought } met && (met.CreatureId == mine.Id || met.OtherCreatureId == mine.Id))
            {
                SessionCommands.SetSpeedPreset(session, 0);
                return $"{mine.Name} is fighting: time slows to ×1 so you can watch";
            }
        }

        return null;
    }

    /// <summary>Sets the player's Creature down beside a village, as the hand would, so the village's god answers it (<c>--visit</c>, ADR-070). Host only.</summary>
    /// <param name="session">Game session.</param>
    /// <param name="village">The village, by its number from 1.</param>
    /// <returns>A status line.</returns>
    public static string Visit(GameSession session, int village)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        if (session.LocalCreature is not { Activity.Phase: not CreaturePhase.Banished } mine || village < 1 || village > world.Settlements.Count)
        {
            return "Your Creature cannot visit there";
        }

        var place = world.Settlements[village - 1];
        mine.Position = CreatureFounder.Beside(world.Regions, place.Position) ?? place.Position;
        (mine.Activity.Phase, mine.Activity.TicksLeft) = (CreaturePhase.Deciding, 0);
        mine.Activity.Path.Clear();
        return $"{mine.Name} visits {place.Name}";
    }

    /// <summary>Gives the player's Creature the traits named, strongly (<c>--temperament</c>, ADR-037). Host only.</summary>
    /// <param name="session">Game session.</param>
    /// <param name="words">Words from <see cref="TemperamentText.Words"/>.</param>
    /// <returns>A status line.</returns>
    public static string SetTemperament(GameSession session, IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(words);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        if (session.LocalCreature is not { } mine)
        {
            return "You have no Creature";
        }

        session.Command((_, _) =>
        {
            foreach (var word in words)
            {
                TemperamentText.Give(mine, word);
            }

            return true;
        });
        return $"{mine.Name} is {TemperamentText.Describe(mine)}";
    }

    /// <summary>The Creature nearest the local Creature that is not banished, if any.</summary>
    /// <param name="session">Game session.</param>
    /// <returns>The Creature, or <c>null</c>.</returns>
    public static CreatureState? NearestOther(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.LocalCreature is not { } mine)
        {
            return null;
        }

        CreatureState? nearest = null;
        var best = float.MaxValue;
        foreach (var other in session.World.Creatures)
        {
            var dx = other.Position.X - mine.Position.X;
            var dz = other.Position.Z - mine.Position.Z;
            var distance = (dx * dx) + (dz * dz);
            if (other.Id != mine.Id && other.Activity.Phase != CreaturePhase.Banished && distance < best)
            {
                (nearest, best) = (other, distance);
            }
        }

        return nearest;
    }

    /// <summary>How the local Creature would answer the nearest other Creature if they met now, best first, for the debug view.</summary>
    /// <param name="session">Game session.</param>
    /// <returns>The answers; none without two Creatures.</returns>
    public static IReadOnlyList<CreatureOption> PreviewResponses(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.LocalCreature is { } mine && NearestOther(session) is { } other ? session.CreatureBrain.ListResponses(session.World, mine, other) : [];
    }

    /// <summary>
    /// Moves another Creature beside the local one, free to meet it at once, as the M key brings
    /// migrants. The meeting itself happens in the simulation.
    /// </summary>
    private static CreatureState? Bring(GameSession session, string? name)
    {
        var world = session.World;
        var other = string.IsNullOrEmpty(name)
            ? NearestOther(session)
            : world.Creatures.Find(c => c.Id != session.LocalCreature?.Id && c.Activity.Phase != CreaturePhase.Banished && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (session.LocalCreature is not { Activity.Phase: not CreaturePhase.Banished } mine || other is null
            || CreatureFounder.Beside(world.Regions, mine.Position) is not { } spot)
        {
            return null;
        }

        other.Position = spot;
        other.Activity.Path.Clear();
        other.Activity.Phase = CreaturePhase.Deciding;
        other.Activity.TicksLeft = 0;
        foreach (var view in new[] { CreatureBonds.Find(mine, other.Id), CreatureBonds.Find(other, mine.Id) })
        {
            if (view is not null)
            {
                view.LastMetTick = -1;
            }
        }

        return other;
    }
}
