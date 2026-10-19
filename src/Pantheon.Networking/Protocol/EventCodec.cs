using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Pantheon.Domain.Events;

namespace Pantheon.Networking.Protocol;

/// <summary>
/// Turns world events into <see cref="EventEnvelope"/>s and back (ADR-018). Every concrete
/// <see cref="WorldEvent"/> type in the domain is known by its name, so a new event type travels
/// without changes here. A kind the receiver does not know is skipped.
/// </summary>
public static class EventCodec
{
    private static readonly Dictionary<string, Type> Kinds = typeof(WorldEvent).Assembly.GetTypes()
        .Where(type => type.IsSubclassOf(typeof(WorldEvent)) && !type.IsAbstract)
        .ToDictionary(type => type.Name, StringComparer.Ordinal);

    /// <summary>Names of every event type that can travel, in name order.</summary>
    public static IReadOnlyList<string> KnownKinds { get; } = [.. Kinds.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Wraps events for sending.</summary>
    /// <param name="events">The events, in order.</param>
    /// <returns>Their envelopes.</returns>
    public static IReadOnlyList<EventEnvelope> Wrap(IEnumerable<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return [.. events.Select(e => new EventEnvelope(e.GetType().Name, JsonSerializer.SerializeToElement(e, e.GetType(), MessageCodec.Options)))];
    }

    /// <summary>Unwraps received events, skipping kinds this build does not know.</summary>
    /// <param name="envelopes">The envelopes, in order.</param>
    /// <returns>The events, in order.</returns>
    public static IReadOnlyList<WorldEvent> Unwrap(IEnumerable<EventEnvelope> envelopes)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        var events = new List<WorldEvent>();
        foreach (var envelope in envelopes)
        {
            if (Kinds.TryGetValue(envelope.Kind, out var type) && envelope.Data.Deserialize(type, MessageCodec.Options) is WorldEvent worldEvent)
            {
                events.Add(worldEvent);
            }
        }

        return events;
    }
}
