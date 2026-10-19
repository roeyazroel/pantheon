using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;

namespace Pantheon.Game;

/// <summary>
/// The events a session's world raised and who still needs them: the local player's alerts and
/// effects, and, on a host, the players it sends them to (ADR-018). Both drain them on their own
/// schedule; nobody's events grow beyond <see cref="MaxPending"/>.
/// </summary>
internal sealed class SessionEvents
{
    /// <summary>Events nobody drains (headless runs) are dropped oldest first beyond this many.</summary>
    public const int MaxPending = 512;

    private readonly List<WorldEvent> _outbox = [];
    private int _forwarded;

    /// <summary>The list the engine and the authorities add to.</summary>
    public List<WorldEvent> Pending { get; } = [];

    /// <summary>Whether events are also kept for the host to send.</summary>
    public bool KeepsOutbox { get; set; }

    /// <summary>Returns the local player's events, oldest first, and forgets them.</summary>
    /// <returns>The events.</returns>
    public IReadOnlyList<WorldEvent> DrainLocal()
    {
        Collect();
        var events = Pending.ToArray();
        Pending.Clear();
        _forwarded = 0;
        return events;
    }

    /// <summary>Returns the events the host has not sent yet, oldest first, and forgets them.</summary>
    /// <returns>The events; none unless <see cref="KeepsOutbox"/> is set.</returns>
    public IReadOnlyList<WorldEvent> DrainOutbox()
    {
        Collect();
        var events = _outbox.ToArray();
        _outbox.Clear();
        return events;
    }

    /// <summary>Adds events raised elsewhere (a host's, on a replica).</summary>
    /// <param name="events">The events, oldest first.</param>
    public void Add(IEnumerable<WorldEvent> events)
    {
        Pending.AddRange(events);
        Trim();
    }

    /// <summary>Drops the oldest events nobody drained beyond <see cref="MaxPending"/>, after keeping them for the host.</summary>
    public void Trim()
    {
        if (Pending.Count > MaxPending)
        {
            Collect();
            var dropped = Pending.Count - MaxPending;
            Pending.RemoveRange(0, dropped);
            _forwarded = Math.Max(0, _forwarded - dropped);
        }
    }

    /// <summary>Forgets every event, for a new world.</summary>
    public void Clear()
    {
        Pending.Clear();
        _outbox.Clear();
        _forwarded = 0;
    }

    /// <summary>Copies events the host has not sent yet into its outbox.</summary>
    private void Collect()
    {
        if (KeepsOutbox)
        {
            for (var i = _forwarded; i < Pending.Count; i++)
            {
                _outbox.Add(Pending[i]);
            }

            if (_outbox.Count > MaxPending)
            {
                _outbox.RemoveRange(0, _outbox.Count - MaxPending);
            }
        }

        _forwarded = Pending.Count;
    }
}
